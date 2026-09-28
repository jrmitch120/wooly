using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;
using Wooly.Core.Profiles;

namespace Wooly.Cli.Commands;

/// <summary>
///     Forgets a profile: where it points and the access token it signed in with. Asks nothing, like the other
///     <c>profile</c> commands — naming the profile is the whole of the intent, and a prompt would stop it running in a
///     script. The token is only this machine's copy; the authorization is still the instance's to revoke. The default
///     profile is refused as a usage error, so a script's next command is never left with nothing to act as.
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

        // The account rather than the instance where one is known, as profile add reports it: two profiles on one
        // instance are told apart by who they are.
        var who = removal.Removed.Account ?? removal.Removed.Instance;
        console.MarkupLineInterpolated($"Removed profile [bold]{settings.Name}[/] ({who}) and its access token.");

        return (int)ExitCode.Success;
    }
}
