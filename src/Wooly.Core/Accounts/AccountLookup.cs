using Mastonet;
using Wooly.Core.Errors;
using WireAccount = Mastonet.Entities.Account;

namespace Wooly.Core.Accounts;

/// <summary>
///     The crossing from the address a user types to the id Mastodon's endpoints take. Mastonet 3.1.3 has no lookup
///     endpoint, so it is done by asking the instance to resolve a search — which is what finds an account this
///     instance has never met (ADR-0011) — and taking only an exact match.
///     <para>
///         Shared rather than written where it is needed, because it is now needed in two places that must not answer
///         differently: a tie is put on the account an address names, and an account's own posts are read for the same
///         address. Two copies of "which candidate did they mean" is how a client comes to block one account and show
///         another.
///     </para>
/// </summary>
internal static class AccountLookup
{
    /// <summary>
    ///     How many candidates a lookup asks for. More than one, because an instance answers a search with everything
    ///     that resembles the query — <c>alice@hachyderm.io</c> brings back <c>alicia</c> and <c>alice@other.social</c>
    ///     too — and the wanted one is not reliably first. Few, because only an exact match is ever taken.
    /// </summary>
    private const int Candidates = 10;

    /// <summary>
    ///     The account <paramref name="account" /> names, found by asking the instance to resolve it. Only an exact
    ///     match on the full address is taken: a search for <c>alice@hachyderm.io</c> that turns up only
    ///     <c>alicia@hachyderm.io</c> has found somebody else, and acting on the wrong account on a near miss is not a
    ///     mistake worth being helpful about.
    /// </summary>
    /// <param name="instance">The instance being asked, which is the one a bare username belongs to.</param>
    /// <exception cref="UnknownAccountException">The instance knows no account by that address.</exception>
    public static async Task<WireAccount> Resolve(
        IMastodonClient client,
        AccountAddress account,
        string instance,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var wanted = account.On(instance);

        var found = await client.SearchAccounts(account.Text, Candidates, resolveNonLocalAccouns: true);

        return found.FirstOrDefault(
                   candidate => string.Equals(
                       MastodonWire.Qualify(candidate, instance),
                       wanted,
                       StringComparison.OrdinalIgnoreCase))
               ?? throw new UnknownAccountException(account, instance);
    }

    /// <summary>
    ///     The instance's own id for the account <paramref name="account" /> names — free where whoever asked has
    ///     already paid for the crossing and handed the resolution on, and <see cref="Resolve" /> where they have not.
    /// </summary>
    /// <remarks>
    ///     Here rather than at each read that takes a <see cref="NamedAccount" />, for the reason this class exists at
    ///     all: two copies of "when is a lookup owed" is how one read comes to spend a call the other saves, or worse,
    ///     to trust an id where the other would not (ADR-0012's second amendment).
    /// </remarks>
    /// <param name="instance">The instance being asked, which is the one a bare username belongs to.</param>
    /// <exception cref="UnknownAccountException">
    ///     The instance knows no account by that address. Only reachable where <paramref name="account" /> carries no
    ///     id: an account already resolved is one the instance has already named.
    /// </exception>
    public static async Task<string> IdOf(
        IMastodonClient client,
        NamedAccount account,
        string instance,
        CancellationToken cancellationToken) =>
        account.Id ?? (await Resolve(client, account.Address, instance, cancellationToken)).Id;
}
