using Spectre.Console;
using Spectre.Console.Cli;
using Wooly.Cli.Output;
using Wooly.Core.Conversations;
using Wooly.Core.Profiles;

namespace Wooly.Cli.Commands;

/// <summary>
///     Lists the direct conversations the profile is in, most recently spoken in first: who each is with, whether
///     anything in it is unread, and the last thing said.
/// </summary>
internal sealed class DirectMessageListCommand(
    IAnsiConsole console,
    IProfileRegistry profiles,
    IDirectMessages messages) : AsyncCommand<DirectMessageListCommand.Settings>
{
    protected override async Task<int> ExecuteAsync(
        CommandContext context,
        Settings settings,
        CancellationToken cancellationToken)
    {
        var profile = profiles.Resolve(settings.Profile);
        var fetch = await messages.List(profile, settings.Limit, cancellationToken);

        if (settings.Json)
        {
            ConversationJson.Write(console, fetch);
        }
        else
        {
            ConversationReport.Write(console, fetch);
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

    internal sealed class Settings : PagedListSettings
    {
        /// <inheritdoc />
        protected override string Counted => "conversation";
    }
}
