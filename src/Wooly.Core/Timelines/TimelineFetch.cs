using Wooly.Core.Errors;
using Wooly.Core.Posts;

namespace Wooly.Core.Timelines;

/// <summary>
///     What a read of a timeline came back with, and whether that is all of what was asked for. The distinction is the
///     point: a fetch stopped by a rate limit part way through may hold no posts at all, and a caller that could not
///     tell that from a timeline with nothing on it would report an empty timeline to a user who has one.
/// </summary>
public sealed record TimelineFetch
{
    /// <summary>The posts that arrived, newest first.</summary>
    public required IReadOnlyList<Post> Posts { get; init; }

    /// <summary>
    ///     The rate limit that cut the fetch short, or <see langword="null" /> if nothing did. Held as the exception
    ///     itself so a front end that treats this as a failure — the CLI does, per ADR-0006 — can throw the instance's
    ///     own answer rather than a second-hand copy of it.
    /// </summary>
    public required RateLimitedException? StoppedBy { get; init; }

    /// <summary>Whether this is everything the caller asked for, as far as the timeline goes.</summary>
    public bool IsComplete => StoppedBy is null;

    /// <summary>A fetch that ran to the end of what was asked for.</summary>
    public static TimelineFetch Complete(IReadOnlyList<Post> posts) => new() { Posts = posts, StoppedBy = null };

    /// <summary>A fetch the instance's rate limit stopped, holding whatever had already arrived.</summary>
    public static TimelineFetch StoppedShort(IReadOnlyList<Post> posts, RateLimitedException rateLimit) =>
        new() { Posts = posts, StoppedBy = rateLimit };
}
