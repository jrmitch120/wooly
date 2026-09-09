using Spectre.Console;
using Spectre.Console.Cli;
using Wooly.Cli.Output;
using Wooly.Core.Profiles;
using Wooly.Core.Relationships;

namespace Wooly.Cli.Commands;

/// <summary>
///     Shows one account, named by its address and read on its own: who somebody is, and where the profile stands with
///     them. The read the <c>account</c> branch never had — the rest of it is follow, block, mute, the two lists and
///     requests, and <c>profile show</c> is the local credential entry, a different thing entirely (CONTEXT.md's
///     <em>profile</em> against its <em>account</em>).
/// </summary>
/// <remarks>
///     One call and no more. The account screen this borrows its content from also asks who the reader and the account
///     have in common, and that stays off the CLI deliberately: it is a call spent to say something only a screen
///     benefits from (ADR-0019).
/// </remarks>
internal sealed class AccountShowCommand(
    IAnsiConsole console,
    IProfileRegistry profiles,
    IAccountRelationships relationships) : AsyncCommand<SingleAccountSettings>
{
    protected override async Task<int> ExecuteAsync(
        CommandContext context,
        SingleAccountSettings settings,
        CancellationToken cancellationToken)
    {
        var profile = profiles.Resolve(settings.Profile);
        var account = await relationships.Show(profile, settings.Address, cancellationToken);

        if (settings.Json)
        {
            JsonOutput.Write(console, AccountDocument.Of(account));
        }
        else
        {
            AccountReport.Shown(console, account);
        }

        return (int)ExitCode.Success;
    }
}
