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

        if (!ConfirmedBy(settings))
        {
            NotificationReport.LeftAlone(console);

            // Nothing went wrong: the user was asked, and answered. A script cannot reach this at all — see below.
            return (int)ExitCode.Success;
        }

        await notifications.Clear(profile, cancellationToken);

        NotificationReport.Cleared(console);

        return (int)ExitCode.Success;
    }

    /// <summary>
    ///     Whether to go ahead. A person at a terminal is asked, because everything waiting goes at once and none of it
    ///     comes back. A script is not: there is nothing to prompt at and nobody to read the prompt, and stopping to ask
    ///     would make this command unusable in the automation the CLI exists for. Typing the command is that
    ///     invocation's consent, and <c>--yes</c> is how a person says the same thing.
    /// </summary>
    private bool ConfirmedBy(Settings settings)
    {
        if (settings.Yes || !console.Profile.Capabilities.Interactive)
        {
            return true;
        }

        return console.Confirm("Clear every notification? This cannot be undone.", defaultValue: false);
    }
}
