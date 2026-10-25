using Wooly.Core.Paging;
using Wooly.Core.Posts;
using Wooly.Core.Profiles;

namespace Wooly.Core.Timelines;

/// <summary>
///     Reads posts from a timeline. The narrow port ADR-0005 asks for over Mastonet's whole REST surface: front ends
///     depend on this, and their tests fake this rather than the network.
/// </summary>
public interface ITimelineReader
{
    /// <summary>Reads up to <paramref name="limit" /> posts from <paramref name="timeline" />, newest first.</summary>
    /// <param name="profile">The profile to read as — which instance to ask, and the token to ask with.</param>
    /// <param name="timeline">Which timeline to read.</param>
    /// <param name="limit">
    ///     How many posts are wanted. More than one page's worth is fetched by paging, which the caller never sees.
    /// </param>
    /// <returns>
    ///     The posts, and whether the fetch ran to the end of what was asked for — a rate limit part way through stops
    ///     it and is reported alongside what had already arrived, rather than losing it.
    /// </returns>
    Task<Fetch<Post>> Read(ActiveProfile profile, Timeline timeline, int limit, CancellationToken cancellationToken);

    /// <summary>
    ///     Reads on: up to <paramref name="limit" /> more posts from <paramref name="timeline" />, from where a fetch of
    ///     it said to read on from — the posts behind the last one it held, never anything newer (#460).
    /// </summary>
    /// <param name="profile">The profile to read as.</param>
    /// <param name="timeline">The timeline <paramref name="from" /> was handed out for.</param>
    /// <param name="from">Where to read on from, as an earlier fetch of <paramref name="timeline" /> said.</param>
    /// <param name="limit">How many more posts are wanted, paged as <see cref="Read" /> pages.</param>
    /// <returns>
    ///     The posts, and where to read on from next unless the timeline is now whole — reported as <see cref="Read" />
    ///     reports, a rate limit included.
    /// </returns>
    Task<Fetch<Post>> ReadOn(
        ActiveProfile profile,
        Timeline timeline,
        Onward from,
        int limit,
        CancellationToken cancellationToken);
}
