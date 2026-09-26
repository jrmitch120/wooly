using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;
using Wooly.Core.Profiles;

namespace Wooly.Cli.Commands;

/// <summary>
///     Forgets a profile: where it points and the access token it signed in with. Asks nothing, like the other
///     <c>profile</c> commands — naming the profile is the whole of the intent, and a prompt would stop it running in a
///     script. The token is only this machine's copy; the authorization is still the instance's to revoke.
/// </summary>
internal sealed class ProfileRemoveCommand(IAnsiConsole console, IProfileRegistry profiles)
    : Command<ProfileRemoveCommand.Settings>
{
    internal sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<NAME>")]
        [Description("The profile to remove, along with its access token.")]
        public string Name { get; init; } = string.Empty;
    }

    protected override int Execute(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var removal = profiles.Remove(settings.Name);

        console.MarkupLineInterpolated($"Removed profile [bold]{settings.Name}[/].");

        // Current was cleared rather than moved, so the next command with no --profile has nothing to act as. Said
        // now, with the way out, rather than left to surface as a failure on whatever the user runs next.
        if (removal.WasCurrent)
        {
            console.WriteLine("No profile is current now. Choose one with profile switch <NAME>.");
        }

        return (int)ExitCode.Success;
    }
}
