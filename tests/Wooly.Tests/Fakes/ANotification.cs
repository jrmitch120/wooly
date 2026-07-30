using Wooly.Core.Notifications;
using Wooly.Core.Posts;

namespace Wooly.Tests.Fakes;

/// <summary>
///     A notification with everything filled in, so a test only says the part it is about — <see cref="APost" /> for the
///     inbox, and for the same reason.
/// </summary>
internal static class ANotification
{
    /// <param name="post">
    ///     The post it is about. Defaults to one, because all but a follow have one; <see cref="Follow" /> is the
    ///     notification that has none.
    /// </param>
    public static Notification With(
        string id = "34",
        NotificationKind? kind = null,
        string account = "alice@hachyderm.io",
        string author = "Alice",
        Post? post = null) => new()
    {
        Id = id,
        Kind = kind ?? NotificationKind.Mention,
        ReceivedAt = new DateTimeOffset(2026, 7, 29, 12, 4, 0, TimeSpan.Zero),
        Account = account,
        Author = author,
        Post = post ?? APost.With(account: account, author: author),
    };

    /// <summary>Somebody arriving rather than something they wrote, which is the notification with no post behind it.</summary>
    public static Notification Follow(string id = "35", string account = "bob@mastodon.social") => new()
    {
        Id = id,
        Kind = NotificationKind.Follow,
        ReceivedAt = new DateTimeOffset(2026, 7, 29, 12, 4, 0, TimeSpan.Zero),
        Account = account,
        Author = "Bob",
        Post = null,
    };
}
