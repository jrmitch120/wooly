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
        IReadOnlyList<PostMedia>? media = null,
        IReadOnlyList<string>? mentions = null,
        string? avatarUrl = null,
        PostReplyTarget? inReplyTo = null,
        PostPoll? poll = null) => new()
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
        Mentions = mentions ?? [],
        Boosted = boosted,
        Url = $"https://mastodon.social/@jeff/{id}",
        AvatarUrl = avatarUrl,
        InReplyTo = inReplyTo,
        Poll = poll,
    };

    /// <summary>A picture attached to a post, with the description its author gave it.</summary>
    public static PostMedia APicture(string id = "m1", string? description = "A cartoon sheep") =>
        Attached(MediaKind.Image, id, description);

    /// <summary>An attachment of any kind, for a test that is about the kind rather than about a picture.</summary>
    public static PostMedia Attached(
        MediaKind kind,
        string id = "m1",
        string? description = "A cartoon sheep") => new()
    {
        Id = id,
        Kind = kind,
        Url = $"https://files.mastodon.social/{id}/original.png",
        Preview = $"https://files.mastodon.social/{id}/small.png",
        Description = description,
    };

    /// <summary>The three marks, said one at a time, for a test that is about one of them.</summary>
    public static PostMarks Marked(bool boosted = false, bool favorited = false, bool pinned = false) =>
        new() { Boosted = boosted, Favorited = favorited, Pinned = pinned };

    /// <summary>A poll with two answers, for a test that is about the poll rather than about composing one.</summary>
    public static PostPoll APoll(
        IReadOnlyList<PostPollOption>? options = null,
        long votes = 10,
        long? voters = null,
        bool multipleChoice = false,
        bool closed = false,
        DateTimeOffset? expiresAt = null,
        bool voted = false,
        string id = "7") => new()
    {
        Id = id,
        Options = options ?? [AnAnswer("Cats", 4), AnAnswer("Dogs", 6)],
        Votes = votes,
        Voters = voters,
        MultipleChoice = multipleChoice,
        Closed = closed,
        ExpiresAt = expiresAt,
        Voted = voted,
    };

    /// <summary>One answer on a poll, with its vote count — or none, for an answer whose count is withheld.</summary>
    public static PostPollOption AnAnswer(string text, long? votes, bool picked = false) =>
        new() { Text = text, Votes = votes, Picked = picked };
}
