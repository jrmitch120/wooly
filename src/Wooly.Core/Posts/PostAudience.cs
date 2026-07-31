namespace Wooly.Core.Posts;

/// <summary>
///     Comparing who two posts reach. <see cref="PostVisibility" /> lists its four from the widest audience to the
///     narrowest, and this is the one place that ordering is relied on — so a member inserted in the middle of that
///     enum is a change to make here, rather than a reply that quietly starts going out wider than it should.
/// </summary>
public static class PostAudience
{
    /// <summary>Whether <paramref name="visibility" /> reaches more accounts than <paramref name="other" /> does.</summary>
    public static bool IsWiderThan(PostVisibility visibility, PostVisibility other) => visibility < other;

    /// <summary>
    ///     The narrower of the two — which, for a reply and the post it answers, is the widest the reply may go out at.
    /// </summary>
    public static PostVisibility Narrower(PostVisibility visibility, PostVisibility other) =>
        IsWiderThan(visibility, other) ? other : visibility;
}
