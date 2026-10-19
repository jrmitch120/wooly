using Spectre.Console;
using Spectre.Console.Cli;
using Wooly.Cli.Infrastructure;
using Wooly.Cli.Output;
using Wooly.Core.Accounts;
using Wooly.Core.Profiles;

namespace Wooly.Cli.Commands;

/// <summary>
///     Edits the public profile of the account a profile signs in as (#404). Under <c>account</c> rather than
///     <c>profile</c>, because the <c>profile</c> branch is the local entry (ADR-0028).
/// </summary>
/// <remarks>
///     Reads the public profile as written first and sends only what differs from it, so that a script can say
///     everything it wants and change only what is not already so — and a run asking for nothing new sends nothing.
///     It asks nothing either way, and says the account the way <c>account show</c> does.
/// </remarks>
internal sealed class AccountEditCommand(
    IAnsiConsole console,
    IProfileRegistry profiles,
    IPublicProfileEditor editor,
    StandardInput standardInput) : AsyncCommand<AccountEditSettings>
{
    protected override async Task<int> ExecuteAsync(
        CommandContext context,
        AccountEditSettings settings,
        CancellationToken cancellationToken)
    {
        var profile = profiles.Resolve(settings.Profile);

        // Before anything is asked of the instance: a pipe that cannot be read is no reason to have spent a call.
        var wanted = settings.ToChange(PipedBio);

        var read = await editor.Read(profile, cancellationToken);
        var change = wanted.Against(read);

        var account = change.IsEmpty ? read.Account : await editor.Save(profile, change, cancellationToken);

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

    /// <summary>
    ///     The bio piped in. Nothing piped in is refused rather than sent: it is far likelier a pipe that never got
    ///     connected than a wish to remove the bio, which <c>--bio ""</c> says plainly — and a bio removed by mistake
    ///     cannot be got back.
    /// </summary>
    private string PipedBio() =>
        standardInput.ReadAll() is { Length: > 0 } piped
            ? piped
            : throw new UsageException("Nothing was piped in for --bio=-. To remove the bio, give --bio \"\".");
}
