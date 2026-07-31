using Mastonet;
using Mastonet.Entities;
using Wooly.Core.Accounts;
using Wooly.Core.Errors;
using Wooly.Core.Paging;
using Wooly.Core.Profiles;

// Mastodon's account and this project's are both called what they are. Naming both here rather than qualifying each
// use keeps the wire shape visibly separate from the domain one at every line that touches either.
using Account = Wooly.Core.Accounts.Account;
using WireAccount = Mastonet.Entities.Account;

namespace Wooly.Core.Relationships;

/// <summary>
///     Manages relationships through Mastonet. Two things happen here that nowhere above has to know about.
///     <para>
///         The first is that Mastodon's relationship endpoints all take an account id, and a user types an address. So
///         an address is looked up first, through <see cref="AccountLookup" />. That costs a call before every follow,
///         block and mute, and it is the only way to spend it: Mastonet 3.1.3 has no lookup endpoint, and an address is
///         the only name for an account that means the same thing on two instances.
///     </para>
///     <para>
///         The second is that the lists are paged by <see cref="PagedReading" />, the same loop a timeline and an inbox
///         are read down, so three lists cannot come to disagree about where a list ends.
///     </para>
///     Nothing here retries and nothing here waits. A tie the instance answered is never sent again, because ADR-0006
///     resends nothing an instance has already taken, and a rate limit is reported rather than slept off.
/// </summary>
public sealed class AccountRelationships(IMastodonClientFactory clientFactory) : IAccountRelationships
{
    /// <summary>
    ///     The most accounts Mastodon serves from a list of them in one call — twice a timeline's page, which these
    ///     endpoints allow. Asking for more than an endpoint gives makes every full page look short, which is the one
    ///     thing the paging loop reads as the end of a list.
    /// </summary>
    private const int PageSize = 80;

    /// <inheritdoc />
    public async Task<Account> Set(
        ActiveProfile profile,
        AccountAddress account,
        AccountTie tie,
        bool wanted,
        CancellationToken cancellationToken)
    {
        var client = clientFactory.CreateClient(profile.Instance, profile.AccessToken);
        var found = await AccountLookup.Resolve(client, account, profile.Instance, cancellationToken);

        // Nothing is read first to find out whether the tie is already there. The instance settles that, the same way
        // ADR-0009 leaves it to settle whether a post is already boosted.
        cancellationToken.ThrowIfCancellationRequested();

        var standing = await Apply(client, found.Id, tie, wanted);

        return AccountWire.ToAccount(found, profile.Instance, standing);
    }

    /// <inheritdoc />
    public async Task<Account> Show(
        ActiveProfile profile,
        AccountAddress account,
        CancellationToken cancellationToken)
    {
        var client = clientFactory.CreateClient(profile.Instance, profile.AccessToken);
        var found = await AccountLookup.Resolve(client, account, profile.Instance, cancellationToken);

        cancellationToken.ThrowIfCancellationRequested();

        // A second call, because a search answers with accounts and never with where the profile stands with them.
        // Asked for one id rather than the many this endpoint takes: this is one account being read, and a caller
        // that wanted a list would be asking for a list.
        var standing = (await client.GetAccountRelationships(found.Id)).FirstOrDefault();

        return AccountWire.ToAccount(found, profile.Instance, standing);
    }

    /// <inheritdoc />
    public async Task<AccountFetch> List(
        ActiveProfile profile,
        FollowSide side,
        AccountAddress? account,
        int limit,
        CancellationToken cancellationToken)
    {
        var client = clientFactory.CreateClient(profile.Instance, profile.AccessToken);
        var accountId = account is null
            ? await Own(client, cancellationToken)
            : (await AccountLookup.Resolve(client, account, profile.Instance, cancellationToken)).Id;

        return await Collect(
            options => side switch
            {
                FollowSide.Followers => client.GetAccountFollowers(accountId, options),
                FollowSide.Following => client.GetAccountFollowing(accountId, options),
                _ => throw new ArgumentOutOfRangeException(nameof(side), side, "Not a side of a follow this client lists."),
            },
            profile.Instance,
            limit,
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task<AccountFetch> PendingRequests(
        ActiveProfile profile,
        int limit,
        CancellationToken cancellationToken)
    {
        var client = clientFactory.CreateClient(profile.Instance, profile.AccessToken);

        return await Collect(client.GetFollowRequests, profile.Instance, limit, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Account> Answer(
        ActiveProfile profile,
        string accountId,
        bool accepted,
        CancellationToken cancellationToken)
    {
        // Mastonet's own calls take no cancellation token, so a Ctrl-C lands between the calls rather than during one.
        cancellationToken.ThrowIfCancellationRequested();

        var client = clientFactory.CreateClient(profile.Instance, profile.AccessToken);

        // Who asked is read before the request is answered rather than after, for two reasons: an id that names nobody
        // fails before anything has been let in or turned away, and Mastonet's authorize and reject hand back nothing
        // at all — so read afterwards, a request that had just been accepted would have nobody to name.
        var found = await client.GetAccount(accountId);

        cancellationToken.ThrowIfCancellationRequested();

        if (accepted)
        {
            await client.AuthorizeRequest(accountId);
        }
        else
        {
            await client.RejectRequest(accountId);
        }

        return AccountWire.ToAccount(found, profile.Instance);
    }

    /// <summary>
    ///     The one crossing between this client's three ties and Mastodon's six endpoints. Written out rather than
    ///     built from the tie's name, so that a tie renamed here cannot quietly start calling an endpoint that is not
    ///     there — or, worse, one that is.
    /// </summary>
    /// <remarks>
    ///     Following names <c>reblogs</c> explicitly for a reason the compiler will not warn about: Mastonet has a
    ///     second <c>Follow</c> overload taking a URI, which a single-argument call binds to instead — a different
    ///     endpoint, answering with an account rather than a relationship.
    /// </remarks>
    private static Task<Relationship> Apply(IMastodonClient client, string accountId, AccountTie tie, bool wanted) =>
        (tie, wanted) switch
        {
            (AccountTie.Follow, true) => client.Follow(accountId, reblogs: true),
            (AccountTie.Follow, false) => client.Unfollow(accountId),
            (AccountTie.Block, true) => client.Block(accountId),
            (AccountTie.Block, false) => client.Unblock(accountId),
            (AccountTie.Mute, true) => client.Mute(accountId, notifications: true),
            (AccountTie.Mute, false) => client.Unmute(accountId),
            _ => throw new ArgumentOutOfRangeException(nameof(tie), tie, "Not a tie this client keeps with an account."),
        };

    /// <summary>Collects a list of accounts a page at a time, however many pages the caller's limit takes.</summary>
    private static async Task<AccountFetch> Collect(
        Func<ArrayOptions, Task<MastodonList<WireAccount>>> readPage,
        string instance,
        int limit,
        CancellationToken cancellationToken)
    {
        var read = await PagedReading.Collect(
            limit,
            PageSize,
            readPage,
            account => AccountWire.ToAccount(account, instance),

            // No fallback cursor: Mastodon pages these lists by the id of the follow, not of the account followed,
            // and an account's id is a value in another id space altogether. An instance that names no next page has
            // ended the list, and guessing one would silently skip or repeat accounts.
            idOf: null,
            cancellationToken);

        return read.StoppedBy is null
            ? AccountFetch.Complete(read.Items)
            : AccountFetch.StoppedShort(read.Items, read.StoppedBy);
    }

    /// <summary>The id of the account the profile signs in as, which is whose lists a user who named nobody meant.</summary>
    /// <remarks>
    ///     Asked of the instance rather than taken from the profile's recorded address, because the address would have
    ///     to be looked up to become an id anyway — and this call is the shorter, exacter way round.
    /// </remarks>
    private static async Task<string> Own(IMastodonClient client, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return (await client.GetCurrentUser()).Id;
    }
}
