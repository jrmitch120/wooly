namespace Wooly.Core.Posts;

/// <summary>
///     What a mark just put on or taken off is called, in this project's vocabulary. Shared by the CLI's marking
///     commands and the TUI's status row for the reason <see cref="Profiles.ProfileWords" /> is: the same act described
///     two ways reads as two acts.
/// </summary>
public static class PostMarkWords
{
    /// <summary>
    ///     What just happened, as one word: "Boosted", "Unbookmarked". One table, so that eight commands and a key
    ///     cannot come to describe four marks in more than eight ways.
    /// </summary>
    public static string Did(PostMark mark, bool wanted) => (mark, wanted) switch
    {
        (PostMark.Boost, true) => "Boosted",
        (PostMark.Boost, false) => "Unboosted",
        (PostMark.Favorite, true) => "Favorited",
        (PostMark.Favorite, false) => "Unfavorited",
        (PostMark.Pin, true) => "Pinned",
        (PostMark.Pin, false) => "Unpinned",
        (PostMark.Bookmark, true) => "Bookmarked",
        (PostMark.Bookmark, false) => "Unbookmarked",
        _ => throw new ArgumentOutOfRangeException(nameof(mark), mark, "Not a mark this client puts on a post."),
    };
}
