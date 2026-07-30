using Wooly.Core.Paging;
using Wooly.Core.Profiles;

namespace Wooly.Core.Notifications;

/// <summary>
///     Reads and clears notifications through Mastonet. Reading pages through <see cref="PagedReading" />, the same loop
///     a timeline reads down, so an inbox and a timeline cannot come to disagree about where a list ends.
///     <para>
///         Nothing is filtered on the way through. Mastodon offers to leave kinds out of what it sends, and this asks for
///         all of them: a kind this client has no word for is still a notification the account has, and one it never sees
///         is one it can neither read nor dismiss.
///     </para>
///     Nothing here retries and nothing here waits. A dismissal the instance answered is never sent again, because
///     ADR-0006 resends nothing an instance has already taken, and a rate limit is reported rather than slept off.
/// </summary>
public sealed class NotificationInbox(IMastodonClientFactory clientFactory) : INotificationInbox
{
    /// <inheritdoc />
    public async Task<NotificationFetch> Read(ActiveProfile profile, int limit, CancellationToken cancellationToken)
    {
        var client = clientFactory.CreateClient(profile.Instance, profile.AccessToken);

        var read = await PagedReading.Collect(
            limit,
            options => client.GetNotifications(options),
            notification => NotificationWire.ToNotification(notification, profile.Instance),
            notification => notification.Id,
            cancellationToken);

        return read.StoppedBy is null
            ? NotificationFetch.Complete(read.Items)
            : NotificationFetch.StoppedShort(read.Items, read.StoppedBy);
    }

    /// <inheritdoc />
    public async Task Dismiss(ActiveProfile profile, string notificationId, CancellationToken cancellationToken)
    {
        // Mastonet's own calls take no cancellation token, so a Ctrl-C lands before the call rather than during it.
        cancellationToken.ThrowIfCancellationRequested();

        var client = clientFactory.CreateClient(profile.Instance, profile.AccessToken);

        await client.DismissNotification(notificationId);
    }

    /// <inheritdoc />
    public async Task Clear(ActiveProfile profile, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var client = clientFactory.CreateClient(profile.Instance, profile.AccessToken);

        await client.ClearNotifications();
    }
}
