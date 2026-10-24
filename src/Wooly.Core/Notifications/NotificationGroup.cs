using Wooly.Core.Accounts;
using Wooly.Core.Posts;

namespace Wooly.Core.Notifications;

/// <summary>
///     One or more notifications the instance counts as one thing that happened — eight favorites of the same post, a
///     burst of follows — under a key of its own (GLOSSARY.md). Which notifications belong together is the instance's
///     call, never this client's: a mention is always a group of one, and favorites of one post that land in different
///     hours are different groups (ADR-0032).
///     <para>
///         On an instance too old to group, every notification is a group of one, so a front end drawing groups draws
///         every instance the same way.
///     </para>
/// </summary>
public sealed record NotificationGroup
{
    /// <summary>
    ///     The instance's own key for the group — <c>favourite-110-493000</c>, <c>ungrouped-34</c> — which tells one
    ///     group from another and is what a whole group is dismissed by.
    /// </summary>
    public required string Key { get; init; }

    /// <summary>What happened, in this project's words where it has any (ADR-0010).</summary>
    public required NotificationKind Kind { get; init; }

    /// <summary>
    ///     How many notifications are in the group, as the instance counts them — past the actors it sent with it, so
    ///     that "and 40 others" is true however few of the forty were loaded.
    /// </summary>
    public required int Count { get; init; }

    /// <summary>
    ///     The id of the newest notification in the group. A group is unread when this is newer than the read marker,
    ///     since anything newer than the marker in it makes its newest newer still.
    /// </summary>
    public required string NewestId { get; init; }

    /// <summary>When the newest notification in the group arrived, as UTC — the group's age.</summary>
    public required DateTimeOffset NewestAt { get; init; }

    /// <summary>
    ///     The <b>Actors</b> the instance sent with the group, in the order it gave them: the most recent first, and no
    ///     more than its sample — which may be fewer than <see cref="Count" />. Each an account, so that it can be
    ///     opened by its id and shown by its face.
    /// </summary>
    public required IReadOnlyList<Account> Actors { get; init; }

    /// <summary>
    ///     The post the group is about, or <see langword="null" /> where there is none — a follow is somebody
    ///     arriving, not something they wrote.
    /// </summary>
    public Post? Post { get; init; }

    /// <summary>
    ///     <paramref name="notification" /> as a group of one, under the key Mastodon gives a notification it does not
    ///     group: what every notification is on an instance too old to group (ADR-0032), and what one arriving live is
    ///     where its group was not read with it (#458).
    /// </summary>
    public static NotificationGroup Alone(Notification notification) => new()
    {
        Key = $"ungrouped-{notification.Id}",
        Kind = notification.Kind,
        Count = 1,
        NewestId = notification.Id,
        NewestAt = notification.ReceivedAt,
        Actors = [notification.Actor],
        Post = notification.Post,
    };
}
