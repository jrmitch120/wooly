using Wooly.Core.Posts;

namespace Wooly.Tests.Fakes;

/// <summary>
///     A post with everything filled in, so a test only says the part it is about. Shared by every test that needs one
///     to have come back from an instance — a timeline read, a post just published — because a post is the same thing
///     however it arrived, and two builders would let a test's idea of one drift from another's.
/// </summary>
internal static class APost
{
    public static Post With(
        string id = "110",
        string account = "jeff@mastodon.social",
        string author = "Jeff",
        string content = "Hello world",
        string? contentWarning = null,
        PostVisibility visibility = PostVisibility.Public,
        Post? boosted = null,
        PostMarks? marks = null,
        IReadOnlyList<PostMedia>? media = null) => new()
    {
        Id = id,
        Account = account,
        Author = author,
        PostedAt = new DateTimeOffset(2026, 7, 29, 12, 0, 0, TimeSpan.Zero),
        Content = content,
        ContentWarning = contentWarning,
        Visibility = visibility,
        Boosts = 3,
        Favorites = 5,
        Replies = 1,
        Marks = marks ?? PostMarks.None,
        Media = media ?? [],
        Boosted = boosted,
        Url = $"https://mastodon.social/@jeff/{id}",
    };

    /// <summary>A picture attached to a post, with the description its author gave it.</summary>
    public static PostMedia APicture(string id = "m1", string? description = "A cartoon sheep") => new()
    {
        Id = id,
        Kind = MediaKind.Image,
        Url = $"https://files.mastodon.social/{id}/original.png",
        Preview = $"https://files.mastodon.social/{id}/small.png",
        Description = description,
    };

    /// <summary>The three marks, said one at a time, for a test that is about one of them.</summary>
    public static PostMarks Marked(bool boosted = false, bool favorited = false, bool pinned = false) =>
        new() { Boosted = boosted, Favorited = favorited, Pinned = pinned };
}
