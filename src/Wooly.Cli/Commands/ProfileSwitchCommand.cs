using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;
using Wooly.Core.Profiles;

namespace Wooly.Cli.Commands;

/// <summary>
///     Changes which profile commands act as when they are not told. The persistent half of the multi-account model —
///     <c>--profile</c> is the other half, and changes nothing beyond its own invocation.
/// </summary>
internal sealed class ProfileSwitchCommand(IAnsiConsole console, IProfileRegistry profiles)
    : Command<ProfileSwitchCommand.Settings>
{
    internal sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<NAME>")]
        [Description("The profile to act as from now on.")]
        public string Name { get; init; } = string.Empty;
    }

    protected override int Execute(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        profiles.Switch(settings.Name);

        CurrentProfileNotice.Write(console, settings.Name);

        return (int)ExitCode.Success;
    }
}
