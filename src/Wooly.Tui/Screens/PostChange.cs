using Wooly.Core.Posts;

namespace Wooly.Tui.Screens;

/// <summary>
///     What a change to one post means for a list that may be holding a boost of it instead. Said once, because the
///     five screens holding a list of posts all have to unwrap a boost the same way.
/// </summary>
/// <remarks>
///     A boost is the same post as far as a mark or a deletion goes and a different one as far as the row goes, which
///     is the whole of the rule: who boosted a post, and whether this profile did, is a fact about the boost and not
///     about the post inside it (CONTEXT.md).
/// </remarks>
public static class PostChange
{
    /// <summary>Whether <paramref name="held" /> is the post <paramref name="postId" /> names, or a boost of it.</summary>
    public static bool Names(Post held, string postId) => held.Id == postId || held.Boosted?.Id == postId;

    /// <summary>
    ///     <paramref name="held" /> with <paramref name="post" /> put in place of the copy of it being held, or
    ///     <paramref name="held" /> itself where it is some other post. What stops a star lighting up only once the
    ///     whole timeline has been fetched again.
    /// </summary>
    public static Post Replaced(Post held, Post post) => held.Id == post.Id
        ? post
        : held.Boosted?.Id == post.Id
            ? held with { Boosted = post }
            : held;
}
