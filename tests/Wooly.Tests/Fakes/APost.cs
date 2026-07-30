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
        Post? boosted = null) => new()
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
        Boosted = boosted,
        Url = $"https://mastodon.social/@jeff/{id}",
    };
}
