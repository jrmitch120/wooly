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
        Post? post = null,
        string actorId = "2") => new()
    {
        Id = id,
        Kind = kind ?? NotificationKind.Mention,
        ReceivedAt = new DateTimeOffset(2026, 7, 29, 12, 4, 0, TimeSpan.Zero),
        Actor = AnAccount.With(address: account, author: author, id: actorId),
        Post = post ?? APost.With(account: account, author: author),
    };

    /// <summary>
    ///     <paramref name="notification" /> as a group of one, the way an instance too old to group has every
    ///     notification read (ADR-0032).
    /// </summary>
    public static NotificationGroup Alone(Notification notification) => NotificationGroup.Alone(notification);

    /// <summary>
    ///     A group of <paramref name="count" /> notifications with <paramref name="actors" /> sent along — a favorite
    ///     of the reader's own post by default, the commonest group there is.
    /// </summary>
    /// <param name="actors">Who did it, as display names; each gets an address and id of its own.</param>
    public static NotificationGroup Group(
        string key = "favourite-110-493000",
        NotificationKind? kind = null,
        int? count = null,
        string newestId = "34",
        IReadOnlyList<string>? actors = null,
        Post? post = null,
        bool noPost = false)
    {
        var named = actors ?? ["Alice"];

        return new NotificationGroup
        {
            Key = key,
            Kind = kind ?? NotificationKind.Favorite,
            Count = count ?? named.Count,
            NewestId = newestId,
            NewestAt = new DateTimeOffset(2026, 7, 29, 12, 4, 0, TimeSpan.Zero),
            Actors =
            [
                .. named.Select((name, at) => AnAccount.With(
                    address: $"{name.ToLowerInvariant().Replace(' ', '_')}@hachyderm.io",
                    author: name,
                    id: $"{100 + at}")),
            ],
            Post = noPost ? null : post ?? APost.With(id: "110", account: "jeff@mastodon.social", author: "Jeff", isMine: true),
        };
    }

    /// <summary>Somebody arriving rather than something they wrote, which is the notification with no post behind it.</summary>
    public static Notification Follow(string id = "35", string account = "bob@mastodon.social", string actorId = "3") => new()
    {
        Id = id,
        Kind = NotificationKind.Follow,
        ReceivedAt = new DateTimeOffset(2026, 7, 29, 12, 4, 0, TimeSpan.Zero),
        Actor = AnAccount.With(address: account, author: "Bob", id: actorId),
        Post = null,
    };
}
