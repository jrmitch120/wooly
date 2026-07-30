using Wooly.Core.Posts;
using WireNotification = Mastonet.Entities.Notification;

namespace Wooly.Core.Notifications;

/// <summary>
///     The one crossing between Mastodon's notification and this project's <see cref="Notification" />, alongside
///     <see cref="PostWire" /> and for the same reason: one mapping, so a notification looks the same however it arrived.
/// </summary>
internal static class NotificationWire
{
    /// <param name="instance">
    ///     The instance being read, needed because it names its own accounts by bare username and everyone else's in
    ///     full.
    /// </param>
    public static Notification ToNotification(WireNotification notification, string instance) => new()
    {
        Id = notification.Id,
        Kind = ToKind(notification.Type),
        ReceivedAt = MastodonWire.AsUtc(notification.CreatedAt),
        Account = MastodonWire.Qualify(notification.Account, instance),
        Author = MastodonWire.DisplayName(notification.Account),

        // Absent on a follow, which is somebody arriving rather than something they wrote.
        Post = notification.Status is null ? null : PostWire.ToPost(notification.Status, instance),
    };

    /// <summary>
    ///     How this project spells the kind the wire reported. The four it knows are written out — the wire's
    ///     <c>reblog</c> and <c>favourite</c> reach anything above this layer as a boost and a favorite (CONTEXT.md) —
    ///     and anything else keeps the instance's own word, per <see cref="NotificationKind.Reported" />.
    /// </summary>
    private static NotificationKind ToKind(string type) => type switch
    {
        "mention" => NotificationKind.Mention,
        "follow" => NotificationKind.Follow,
        "reblog" => NotificationKind.Boost,
        "favourite" => NotificationKind.Favorite,

        // Including "follow_request", which is somebody asking to follow rather than following: reporting it as a
        // follow would tell an account it has a follower it does not yet have.
        _ => NotificationKind.Reported(type),
    };
}
