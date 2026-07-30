using Wooly.Core.Accounts;
using Wooly.Core.Profiles;

namespace Wooly.Core.Relationships;

/// <summary>
///     Manages who the profile follows, blocks and mutes, who follows it, and the follows waiting to be answered. The
///     narrow port ADR-0005 asks for over Mastonet's whole REST surface, alongside
///     <see cref="Timelines.ITimelineReader" />, <see cref="Notifications.INotificationInbox" /> and
///     <see cref="Search.IInstanceSearch" /> — front ends depend on this, and their tests fake this rather than the
///     network.
/// </summary>
/// <remarks>
///     One port rather than three, because the four calls are one subject reached through one family of endpoints: a
///     screen showing an account shows what it is to the profile, who follows it, and — for a locked account — who is
///     waiting, and splitting that across three ports would be three fakes to write for the one screen.
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

    /// <summary>Lists one side of an account's follows, newest follow first.</summary>
    /// <param name="account">
    ///     Whose follows to list, or <see langword="null" /> for the profile's own — which is what a user asking for
    ///     "followers" with nobody named means, and the one account they never have to name.
    /// </param>
    /// <param name="limit">How many accounts to collect, across as many pages as it takes.</param>
    /// <exception cref="Errors.UnknownAccountException">The instance knows no account by that address.</exception>
    Task<AccountFetch> List(
        ActiveProfile profile,
        FollowSide side,
        AccountAddress? account,
        int limit,
        CancellationToken cancellationToken);

    /// <summary>
    ///     Lists the accounts waiting to be let in, which only a locked account ever has any of: an unlocked one is
    ///     followed rather than asked.
    /// </summary>
    Task<AccountFetch> PendingRequests(ActiveProfile profile, int limit, CancellationToken cancellationToken);

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
