using Spectre.Console;
using Spectre.Console.Cli;
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
    INotificationInbox notifications) : AsyncCommand<NotificationListCommand.Settings>
{
    internal sealed class Settings : PagedListSettings
    {
        /// <inheritdoc />
        protected override string Counted => "notification";
    }

    protected override async Task<int> ExecuteAsync(
        CommandContext context,
        Settings settings,
        CancellationToken cancellationToken)
    {
        var profile = profiles.Resolve(settings.Profile);
        var fetch = await notifications.Read(profile, settings.Limit, cancellationToken);

        if (settings.Json)
        {
            NotificationJson.Write(console, fetch);
        }
        else
        {
            NotificationReport.Write(console, fetch);
        }

        // What did arrive is worth having, so it is written before the limit that stopped the rest is reported at all.
        // Reporting it is ADR-0006's one handler's job — hence throwing rather than printing, which is also what puts
        // the rate-limited exit code on the process.
        if (fetch.StoppedBy is not null)
        {
            throw fetch.StoppedBy;
        }

        return (int)ExitCode.Success;
    }
}
