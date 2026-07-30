using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;
using Wooly.Cli.Output;
using Wooly.Core.Notifications;
using Wooly.Core.Profiles;

namespace Wooly.Cli.Commands;

/// <summary>
///     Empties the whole inbox in one call. Unlike dismissing one, this takes away a list nobody has necessarily read
///     yet and nothing brings it back — so it asks first, on the same terms <c>post delete</c> does.
/// </summary>
internal sealed class NotificationClearCommand(
    IAnsiConsole console,
    IProfileRegistry profiles,
    INotificationInbox notifications) : AsyncCommand<NotificationClearCommand.Settings>
{
    internal sealed class Settings : ProfileScopedSettings
    {
        [CommandOption("--yes")]
        [Description("Clear without asking. Implied where there is no terminal to ask at.")]
        public bool Yes { get; init; }
    }

    protected override async Task<int> ExecuteAsync(
        CommandContext context,
        Settings settings,
        CancellationToken cancellationToken)
    {
        var profile = profiles.Resolve(settings.Profile);

        // Everything waiting goes at once and none of it comes back, so a person at a terminal is asked first.
        if (!Consent.Given(console, settings.Yes, "Clear every notification? This cannot be undone."))
        {
            NotificationReport.LeftAlone(console);

            // Nothing went wrong: the user was asked, and answered. A script cannot reach this at all — see below.
            return (int)ExitCode.Success;
        }

        await notifications.Clear(profile, cancellationToken);

        NotificationReport.Cleared(console);

        return (int)ExitCode.Success;
    }
}
