using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;
using Wooly.Cli.Output;
using Wooly.Core.Notifications;
using Wooly.Core.Profiles;

namespace Wooly.Cli.Commands;

/// <summary>
///     Clears one notification, named by the id <c>notification list</c> showed against it. Nothing is asked first: one
///     notification dismissed by mistake costs a user the line they had just read, and the post behind it is still
///     there to be found.
/// </summary>
internal sealed class NotificationDismissCommand(
    IAnsiConsole console,
    IProfileRegistry profiles,
    INotificationInbox notifications) : AsyncCommand<NotificationDismissCommand.Settings>
{
    internal sealed class Settings : ProfileScopedSettings
    {
        [CommandArgument(0, "<ID>")]
        [Description("The id of the notification to clear, as shown by notification list.")]
        public string NotificationId { get; init; } = string.Empty;
    }

    protected override async Task<int> ExecuteAsync(
        CommandContext context,
        Settings settings,
        CancellationToken cancellationToken)
    {
        var profile = profiles.Resolve(settings.Profile);

        await notifications.Dismiss(profile, settings.NotificationId, cancellationToken);

        NotificationReport.Dismissed(console, settings.NotificationId);

        return (int)ExitCode.Success;
    }
}
