using Spectre.Console;
using Wooly.Cli.Output;
using Wooly.Core.Notifications;
using Wooly.Core.Profiles;

namespace Wooly.Cli.Commands;

/// <summary>
///     Reads what is waiting for the profile: who mentioned it, followed it, boosted or favorited one of its posts —
///     and anything else the instance thought worth saying, under the instance's own word for it.
/// </summary>
internal sealed class NotificationListCommand(
    IAnsiConsole console,
    IProfileRegistry profiles,
    INotificationInbox notifications)
    : PagedListCommand<NotificationListCommand.Settings, Notification>(profiles)
{
    internal sealed class Settings : PagedListSettings
    {
        /// <inheritdoc />
        protected override string Counted => "notification";
    }

    protected override PagedList<Notification> Listing(ActiveProfile profile, Settings settings) =>
        new(
            token => notifications.Read(profile, settings.Limit, token),
            fetch => NotificationJson.Write(console, fetch),
            fetch => NotificationReport.Write(console, fetch));
}
