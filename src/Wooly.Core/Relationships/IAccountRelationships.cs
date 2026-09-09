using Wooly.Core.Accounts;
using Wooly.Core.Paging;
using Wooly.Core.Profiles;

namespace Wooly.Core.Relationships;

/// <summary>
///     Manages who the profile follows, blocks and mutes, who follows it, the follows waiting to be answered, and who
///     it has in common with another account. The narrow port ADR-0005 asks for over Mastonet's whole REST surface,
///     alongside
///     <see cref="Timelines.ITimelineReader" />, <see cref="Notifications.INotificationInbox" /> and
///     <see cref="Search.IInstanceSearch" /> — front ends depend on this, and their tests fake this rather than the
///     network.
/// </summary>
/// <remarks>
///     One port rather than three, because the calls are one subject reached through one family of endpoints: a
///     screen showing an account shows what it is to the profile, who follows it, who it is followed by that the
///     profile follows too, and — for a locked account — who is waiting, and splitting that across three ports would
///     be three fakes to write for the one screen. Familiar followers arrived later and landed here by that same test
///     (ADR-0012's amendment), rather than on a port of its own.
/// </remarks>
public interface IAccountRelationships
{
    /// <summary>Puts a tie on an account, or takes it off.</summary>
    /// <param name="account">
    ///     The account as the user named it. Turning that into the id Mastodon's endpoints take is this port's business:
    ///     a caller has an address because that is what a user types.
    /// </param>
    /// <param name="wanted">Whether the tie should end up in place: <see langword="false" /> is the <c>un-</c> verb.</param>
    /// <returns>
    ///     The account, carrying the standing the instance answered with — which is how following a locked account is
    ///     told from following anybody else, the one leaving a request behind rather than a follow.
    /// </returns>
    /// <exception cref="Errors.UnknownAccountException">The instance knows no account by that address.</exception>
    Task<Account> Set(
        ActiveProfile profile,
        AccountAddress account,
        AccountTie tie,
        bool wanted,
        CancellationToken cancellationToken);

    /// <summary>Reads one account, and where the profile's own account stands with it.</summary>
    /// <param name="account">The account as the user named it, the same way <see cref="Set" /> takes one.</param>
    /// <remarks>
    ///     Reading and setting sit on one port because the standing a screen shows is the standing a tie changes: the
    ///     TUI's account screen draws what this answers and then asks <see cref="Set" /> to change it, and a second
    ///     port over the same two endpoints would be a seam with no decision behind it.
    /// </remarks>
    /// <returns>
    ///     The account, carrying the standing the instance answered with — never <see langword="null" /> standing,
    ///     because this is one of the few places Mastodon is asked for it.
    /// </returns>
    /// <exception cref="Errors.UnknownAccountException">The instance knows no account by that address.</exception>
    Task<Account> Show(ActiveProfile profile, AccountAddress account, CancellationToken cancellationToken);

    /// <summary>Lists one side of an account's follows, newest follow first.</summary>
    /// <param name="account">
    ///     Whose follows to list, or <see langword="null" /> for the profile's own — which is what a user asking for
    ///     "followers" with nobody named means, and the one account they never have to name. Named rather than
    ///     addressed because this is a read, and a read may be handed a resolution somebody has already paid for
    ///     (ADR-0012's second amendment): a browser opened off an account screen is holding the id it would otherwise
    ///     spend a call learning again. Null still costs the current-user call, which is the shorter route to an id
    ///     rather than a lookup worth avoiding.
    /// </param>
    /// <param name="limit">How many accounts to collect, across as many pages as it takes.</param>
    /// <exception cref="Errors.UnknownAccountException">
    ///     The instance knows no account by that address — only reachable where the account named carries no id, an
    ///     account already resolved being one the instance has already named.
    /// </exception>
    Task<Fetch<Account>> List(
        ActiveProfile profile,
        FollowSide side,
        NamedAccount? account,
        int limit,
        CancellationToken cancellationToken);

    /// <summary>
    ///     Where the profile stands with each of <paramref name="accounts" />, all in the one call the endpoint takes
    ///     many ids for.
    /// </summary>
    /// <remarks>
    ///     Neither side of <see cref="List" /> carries a standing — Mastodon sends one only from the relationship
    ///     endpoints — so a screen listing people has to ask, and asking per row would be one call per person. Given
    ///     the accounts rather than their ids, and answering with them, because the standing is only ever wanted
    ///     attached to the person it is about: a caller matching ids back up itself would be the second place this
    ///     mapping could be got wrong.
    /// </remarks>
    /// <returns>
    ///     The same accounts, each carrying the standing the instance answered with — or <see langword="null" /> where
    ///     it never answered, which this reports rather than throws for the reason
    ///     <see cref="FamiliarFollowers" /> does: it decorates rows that are worth drawing without it, so a rate limit
    ///     reached here leaves the list standing and silent rather than taking it down (ADR-0012's amendment).
    /// </returns>
    Task<IReadOnlyList<Account>?> Standing(
        ActiveProfile profile,
        IReadOnlyList<Account> accounts,
        CancellationToken cancellationToken);

    /// <summary>
    ///     Lists the accounts waiting to be let in, which only a locked account ever has any of: an unlocked one is
    ///     followed rather than asked.
    /// </summary>
    Task<Fetch<Account>> PendingRequests(ActiveProfile profile, int limit, CancellationToken cancellationToken);

    /// <summary>
    ///     Lists the accounts this profile follows that also follow the account named — the people a reader already
    ///     knows, which is what makes a stranger's profile worth reading.
    /// </summary>
    /// <param name="accountId">
    ///     Whose familiar followers to read, as <see cref="Show" /> answers with. An id rather than an address for the
    ///     same reason <see cref="Answer" /> takes one: this is asked from a screen that has just read the account, so
    ///     the id is in hand and exact, and an address would cost a second lookup to arrive back at it.
    /// </param>
    /// <returns>
    ///     The accounts in common — empty where that is nobody — or <see langword="null" /> where the instance never
    ///     answered the question, which is the only call on this port that says so rather than throwing. It is asked
    ///     last of the four an account screen makes and it decorates one row, so a rate limit or a refusal here leaves
    ///     the screen standing with that row missing (ADR-0012's amendment). Absent is not empty: a null draws nothing
    ///     and an empty list says nobody is in common.
    /// </returns>
    Task<IReadOnlyList<Account>?> FamiliarFollowers(
        ActiveProfile profile,
        string accountId,
        CancellationToken cancellationToken);

    /// <summary>Accepts or rejects one pending follow request.</summary>
    /// <param name="accountId">
    ///     The id of the account that asked, as <see cref="PendingRequests" /> lists it. An id rather than an address
    ///     because a request is answered from a list this client just printed, where the id is in front of the user and
    ///     exact — and because an address would cost a lookup to arrive back at the same id.
    /// </param>
    /// <param name="accepted"><see langword="true" /> lets them follow; <see langword="false" /> turns them away.</param>
    /// <returns>The account that asked, so a caller can say who was let in or turned away rather than which id was.</returns>
    Task<Account> Answer(
        ActiveProfile profile,
        string accountId,
        bool accepted,
        CancellationToken cancellationToken);
}
