using System.Text.Json;
using Mastonet;
using Mastonet.Entities;
using Wooly.Core.Accounts;
using Wooly.Core.Errors;
using Wooly.Core.Http;
using Wooly.Core.Paging;
using Wooly.Core.Profiles;

// Mastodon's account and this project's are both called what they are. Naming both here rather than qualifying each
// use keeps the wire shape visibly separate from the domain one at every line that touches either.
using Account = Wooly.Core.Accounts.Account;
using WireAccount = Mastonet.Entities.Account;

namespace Wooly.Core.Relationships;

/// <summary>
///     Manages relationships through Mastonet. Three things happen here that nowhere above has to know about.
///     <para>
///         The first is that Mastodon's relationship endpoints all take an account id, and a user types an address. So
///         an address is looked up first, through <see cref="AccountLookup" />. That costs a call before every follow,
///         block and mute, and it is the only way to spend it: Mastonet 3.1.3 has no lookup endpoint, and an address is
///         the only name for an account that means the same thing on two instances. The one exception is
///         <see cref="List" />, which is a read and so may be handed a resolution somebody has already paid for
///         (ADR-0012's second amendment); every write on this port still resolves the address itself.
///     </para>
///     <para>
///         The second is that the lists are paged by <see cref="PagedReading" />, the same loop a timeline and an inbox
///         are read down, so three lists cannot come to disagree about where a list ends.
///     </para>
///     <para>
///         The third is that one of the five endpoints is not in Mastonet 3.1.3 at all. Familiar followers is read by
///         <see cref="RawMastodonCall" /> over the same <see cref="HttpClient" /> the library's own calls go through,
///         so it is retried and rate-limit-checked like the rest, and a caller cannot tell it apart from the four that
///         went through the library.
///     </para>
///     Nothing here retries and nothing here waits. A tie the instance answered is never sent again, because ADR-0006
///     resends nothing an instance has already taken, and a rate limit is reported rather than slept off.
/// </summary>
public sealed class AccountRelationships(IMastodonClientFactory clientFactory, IHttpClientFactory httpClientFactory)
    : IAccountRelationships
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
    public async Task<Fetch<Account>> List(
        ActiveProfile profile,
        FollowSide side,
        NamedAccount? account,
        int limit,
        CancellationToken cancellationToken)
    {
        var client = clientFactory.CreateClient(profile.Instance, profile.AccessToken);

        // Nobody named is the profile's own list, which the current-user call answers. Anybody else costs whatever
        // AccountLookup.IdOf says they do — nothing where the caller handed on a resolution it had already paid for,
        // and the lookup every write pays for where it did not.
        var accountId = account is null
            ? await Own(client, cancellationToken)
            : await AccountLookup.IdOf(client, account, profile.Instance, cancellationToken);

        var readPage = side.Either<Func<ArrayOptions, Task<MastodonList<WireAccount>>>>(
            options => client.GetAccountFollowers(accountId, options),
            options => client.GetAccountFollowing(accountId, options));

        return await Collect(readPage, profile.Instance, limit, cancellationToken);
    }

    /// <inheritdoc />
    /// <remarks>
    ///     One call for the whole page, which is what this endpoint takes many ids for — and the reason a list of
    ///     people can say where the reader stands with each of them at all. Refused or rate-limited answers nothing:
    ///     the rows are worth drawing without it.
    /// </remarks>
    public async Task<IReadOnlyList<Account>?> Standing(
        ActiveProfile profile,
        IReadOnlyList<Account> accounts,
        CancellationToken cancellationToken)
    {
        if (accounts.Count == 0)
        {
            return accounts;
        }

        try
        {
            var client = clientFactory.CreateClient(profile.Instance, profile.AccessToken);

            cancellationToken.ThrowIfCancellationRequested();

            var answered = (await client.GetAccountRelationships(accounts.Select(account => account.Id)))
                .ToDictionary(relationship => relationship.Id, AccountWire.ToStanding, StringComparer.Ordinal);

            return
            [
                .. accounts.Select(account => answered.TryGetValue(account.Id, out var standing)
                    ? account with { Standing = standing }
                    : account),
            ];
        }
        // FamiliarFollowers' four, and for the same reason: each of them means the instance did not answer the
        // question, and silence rather than a failure is what the port promises. Anything else is this client's own
        // bug and is not a row's to swallow — a row that says there is no tie because the asking broke would be the
        // one dishonest thing on the screen.
        //
        // A fifth here that is not on FamiliarFollowers, and it is the call path rather than the promise that differs:
        // this one goes through Mastonet, which turns an instance's refusal into a ServerErrorException, where the raw
        // GET beside it sees the same refusal as an HttpRequestException. A refusal is the instance declining to
        // answer however it is spelled, so both screens it decorates stay standing and silent (#204).
        catch (Exception unanswered) when (unanswered is RateLimitedException
                                               or TransientNetworkException
                                               or HttpRequestException
                                               or ServerErrorException
                                               or JsonException)
        {
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<Fetch<Account>> PendingRequests(
        ActiveProfile profile,
        int limit,
        CancellationToken cancellationToken)
    {
        var client = clientFactory.CreateClient(profile.Instance, profile.AccessToken);

        return await Collect(client.GetFollowRequests, profile.Instance, limit, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Account>?> FamiliarFollowers(
        ActiveProfile profile,
        string accountId,
        CancellationToken cancellationToken)
    {
        try
        {
            // The endpoint takes many ids at once and this asks about one, because one account is what a screen is
            // showing. The array form is sent all the same: it is the form the endpoint takes, and a single id is a
            // list of one.
            var answered = await RawMastodonCall.Get<IReadOnlyList<FamiliarFollowersWire>>(
                httpClientFactory,
                profile,
                "api/v1/accounts/familiar_followers",
                [new KeyValuePair<string, string>("id[]", accountId)],
                cancellationToken);

            // One id asked about is one entry answered, and an instance that named no entry has said that nobody is
            // in common — which is an answer, and so an empty list rather than the null that means it never answered.
            // A body that is nothing at all is the other way round: nothing was said, so nothing is what is reported.
            return answered is null
                ? null
                : answered.FirstOrDefault()?
                      .Accounts
                      .Select(account => AccountWire.ToAccount(account, profile.Instance))
                      .ToList()
                  ?? [];
        }

        // The one call on this port that swallows a failure, because it is the one whose failure must not cost
        // anything but itself: it is asked last of the four an account screen makes and it decorates a single row, so
        // a rate limit here is the difference between a row missing and a screen missing. Everything caught means the
        // same thing — the instance did not answer the question — and null is how the caller is told so. A
        // cancellation is deliberately not among them: a reader who left is not a reader owed a row.
        catch (Exception unanswered) when (unanswered is RateLimitedException
                                               or TransientNetworkException
                                               or HttpRequestException
                                               or JsonException)
        {
            return null;
        }
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
    private static Task<Fetch<Account>> Collect(
        Func<ArrayOptions, Task<MastodonList<WireAccount>>> readPage,
        string instance,
        int limit,
        CancellationToken cancellationToken) =>
        PagedReading.Collect(
            limit,
            PageSize,
            readPage,
            account => AccountWire.ToAccount(account, instance),

            // No fallback cursor: Mastodon pages these lists by the id of the follow, not of the account followed,
            // and an account's id is a value in another id space altogether. An instance that names no next page has
            // ended the list, and guessing one would silently skip or repeat accounts.
            idOf: null,
            cancellationToken);

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
