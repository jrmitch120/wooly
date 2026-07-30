using System.ComponentModel;
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
    internal sealed class Settings : ProfileScopedSettings
    {
        /// <summary>
        ///     A screen's worth, matching a timeline's default for the same reasons: quick to answer, and — being under
        ///     one page — a single call to the instance for the ordinary invocation.
        /// </summary>
        private const int DefaultLimit = 20;

        /// <remarks>
        ///     The default is stated once, as the attribute Spectre both applies to an invocation that omits the option
        ///     and shows in the help. An initializer as well would be the same fact written twice, able to disagree.
        /// </remarks>
        [CommandOption("--limit <COUNT>")]
        [Description("How many notifications to fetch. More than a page's worth is fetched by asking for further pages.")]
        [DefaultValue(DefaultLimit)]
        public int Limit { get; init; }

        [CommandOption("--json")]
        [Description("Write the notifications as JSON, for another program to read.")]
        public bool Json { get; init; }

        public override ValidationResult Validate() =>
            Limit > 0
                ? ValidationResult.Success()
                : ValidationResult.Error("--limit needs to be at least one notification.");
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
