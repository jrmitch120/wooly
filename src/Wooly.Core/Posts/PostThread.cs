namespace Wooly.Core.Posts;

/// <summary>
///     What stands either side of one post on its instance: the chain it answers, back to the root of the thread, and
///     everything said in answer to it. One thing rather than two lists a caller carries about separately, because
///     Mastodon serves both halves on one call and a screen showing a post draws both (#86).
/// </summary>
/// <remarks>
///     The post itself is not on it. Whoever asks for a thread already holds the post they asked about — that is how
///     they came to name it — and putting a copy in the middle would give a screen two answers to which post it is
///     about, one of which is a round trip older than the other.
/// </remarks>
/// <param name="Ancestors">
///     What the post answers, root first and its immediate parent last, uncapped. Empty where it answers nothing, or
///     where what it answered is no longer there for the instance to send.
/// </param>
/// <param name="Replies">
///     What has been said in answer to it, oldest first — the whole subtree flattened, which is the shape Mastodon
///     serves and the shape a thread reads in.
/// </param>
public sealed record PostThread(IReadOnlyList<Post> Ancestors, IReadOnlyList<Post> Replies)
{
    /// <summary>A post standing on its own: nothing above it and nothing under it.</summary>
    public static PostThread Alone { get; } = new([], []);
}
