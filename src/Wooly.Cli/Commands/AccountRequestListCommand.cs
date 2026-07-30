using Spectre.Console;
using Spectre.Console.Cli;
using Wooly.Cli.Output;
using Wooly.Core.Profiles;
using Wooly.Core.Relationships;

namespace Wooly.Cli.Commands;

/// <summary>
///     Lists the accounts waiting to be let in, which only a locked account ever has any of — an unlocked one is
///     followed rather than asked. Each is listed with the id <c>accept</c> and <c>reject</c> take.
/// </summary>
internal sealed class AccountRequestListCommand(
    IAnsiConsole console,
    IProfileRegistry profiles,
    IAccountRelationships relationships) : AsyncCommand<AccountRequestListCommand.Settings>
{
    internal sealed class Settings : PagedListSettings
    {
        /// <inheritdoc />
        protected override string Counted => "request";
    }

    protected override async Task<int> ExecuteAsync(
        CommandContext context,
        Settings settings,
        CancellationToken cancellationToken)
    {
        var profile = profiles.Resolve(settings.Profile);
        var fetch = await relationships.PendingRequests(profile, settings.Limit, cancellationToken);

        if (settings.Json)
        {
            AccountJson.WriteRequests(console, fetch);
        }
        else
        {
            AccountReport.WriteRequests(console, fetch);
        }

        // What did arrive is worth having, so it is written before the limit that stopped the rest is reported at all
        // (ADR-0006), which is also what puts the rate-limited exit code on the process.
        if (fetch.StoppedBy is not null)
        {
            throw fetch.StoppedBy;
        }

        return (int)ExitCode.Success;
    }
}
