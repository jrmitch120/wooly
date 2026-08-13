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
    /// <summary>
    ///     The most notifications Mastodon serves in one call. Ten fewer than a timeline's page, which is why the loop
    ///     takes this rather than holding one number for everything: asking for 40 would get 30 back, and a full page
    ///     that looks short is exactly what the loop reads as the end of the list.
    /// </summary>
    private const int PageSize = 30;

    /// <inheritdoc />
    public async Task<Fetch<Notification>> Read(ActiveProfile profile, int limit, CancellationToken cancellationToken)
    {
        var client = clientFactory.CreateClient(profile.Instance, profile.AccessToken);

        return await PagedReading.Collect(
            limit,
            PageSize,
            options => client.GetNotifications(options),
            notification => NotificationWire.ToNotification(notification, profile.Instance),
            notification => notification.Id,
            cancellationToken);
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
