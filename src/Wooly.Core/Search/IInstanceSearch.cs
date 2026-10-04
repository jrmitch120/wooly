using Wooly.Core.Accounts;
using Wooly.Core.Profiles;

namespace Wooly.Core.Search;

/// <summary>
///     Asks an instance to find accounts, hashtags and posts. The narrow port ADR-0005 asks for over Mastonet's whole
///     REST surface, alongside <see cref="Timelines.ITimelineReader" /> and
///     <see cref="Notifications.INotificationInbox" /> — front ends depend on this, and their tests fake this rather
///     than the network.
/// </summary>
public interface IInstanceSearch
{
    /// <summary>Finds what <paramref name="query" /> asks for, in the kinds it asks for.</summary>
    /// <param name="profile">
    ///     The profile to search as — which instance to ask, and the token to ask with. Who is asking is part of the
    ///     answer: an instance searches posts an account can see, which is not the same set for two accounts.
    /// </param>
    /// <returns>
    ///     The results, holding only the kinds asked for. Nothing here is partial the way a timeline read can be: a
    ///     search is one call to the instance, so a rate limit stops it with nothing in hand and is raised rather than
    ///     reported alongside a half-answer (ADR-0011).
    /// </returns>
    Task<SearchResults> Find(ActiveProfile profile, SearchQuery query, CancellationToken cancellationToken);

    /// <summary>
    ///     A few of the accounts <paramref name="profile" /> follows that match <paramref name="query" />, best first —
    ///     what a post can mention where the follows read so far did not hold enough (#322). Only accounts the instance
    ///     already knows: a suggestion is not worth sending it to look for one it has not met.
    /// </summary>
    /// <param name="profile">Whose follows to search, and the token to ask with.</param>
    /// <param name="query">What has been typed after the <c>@</c>.</param>
    /// <returns>At most <see cref="FollowedFound" />, in the order the instance ranked them.</returns>
    /// <exception cref="Errors.RateLimitedException">
    ///     The rate limit refused the search. One call, as <see cref="Find" /> is, so there is no half-answer.
    /// </exception>
    Task<IReadOnlyList<Account>> FindFollowed(ActiveProfile profile, string query, CancellationToken cancellationToken);

    /// <summary>The most <see cref="FindFollowed" /> asks for: as many as a mention list shows.</summary>
    public const int FollowedFound = 5;
}
