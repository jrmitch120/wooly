using Spectre.Console;
using Spectre.Console.Cli;
using Wooly.Core.Profiles;

namespace Wooly.Cli.Commands;

/// <summary>
///     Lists the profiles this machine has set up, marking the one commands default to. Reads no access tokens, so it
///     answers on a machine whose keyring is locked.
/// </summary>
internal sealed class ProfileListCommand(IAnsiConsole console, IProfileRegistry profiles) : Command
{
    protected override int Execute(CommandContext context, CancellationToken cancellationToken)
    {
        var summaries = profiles.List();

        if (summaries.Count == 0)
        {
            console.WriteLine("No profiles have been set up yet.");

            return (int)ExitCode.Success;
        }

        var grid = new Grid();
        grid.AddColumns(4);

        foreach (var profile in summaries)
        {
            // Rendered as Text rather than markup: a profile name is whatever the user called it, and square
            // brackets in one must not be read as colour tags.
            grid.AddRow(
                new Text(profile.IsCurrent ? "*" : " "),
                new Text(profile.Name),
                new Text(profile.Instance),
                new Text(profile.Account ?? "-"));
        }

        console.Write(grid);

        return (int)ExitCode.Success;
    }
}
