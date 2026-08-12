using Spectre.Console;
using Wooly.Cli.Output;
using Wooly.Core.Accounts;
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
    IAccountRelationships relationships)
    : PagedListCommand<AccountRequestListCommand.Settings, Account>(profiles)
{
    internal sealed class Settings : PagedListSettings
    {
        /// <inheritdoc />
        protected override string Counted => "request";
    }

    protected override PagedList<Account> Listing(ActiveProfile profile, Settings settings) =>
        new(
            token => relationships.PendingRequests(profile, settings.Limit, token),
            fetch => AccountJson.WriteRequests(console, fetch),
            fetch => AccountReport.WriteRequests(console, fetch));
}
