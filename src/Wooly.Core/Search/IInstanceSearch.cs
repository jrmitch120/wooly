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
}
