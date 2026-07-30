using Wooly.Core.Posts;

namespace Wooly.Core.Notifications;

/// <summary>
///     One thing that happened to this account, as the instance reported it: who did it, what they did, when, and — for
///     everything except a follow — the post they did it to or on.
/// </summary>
public sealed record Notification
{
    /// <summary>
    ///     The instance's own id for the notification, which is how it is dismissed. Not the post's id: a mention has
    ///     both, and dismissing one by the other clears nothing.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>What happened, in this project's words where it has any.</summary>
    public required NotificationKind Kind { get; init; }

    /// <summary>When the instance recorded it, as UTC.</summary>
    public required DateTimeOffset ReceivedAt { get; init; }

    /// <summary>Who did it, as <c>username@instance</c>.</summary>
    public required string Account { get; init; }

    /// <summary>The name that account chose to be shown as, which is not unique and may be anything at all.</summary>
    public required string Author { get; init; }

    /// <summary>
    ///     The post the notification is about, or <see langword="null" /> where there is none — a follow is somebody
    ///     arriving, not something they wrote. For a mention this is their post; for a boost or a favorite it is the
    ///     account's own post that was marked, which is what makes the notification worth reading rather than counting.
    /// </summary>
    public Post? Post { get; init; }
}
