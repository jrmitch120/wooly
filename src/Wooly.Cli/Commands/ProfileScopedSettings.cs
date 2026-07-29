using System.ComponentModel;
using Spectre.Console.Cli;

namespace Wooly.Cli.Commands;

/// <summary>
///     What every command that acts as an account takes: which profile to act as, for this invocation only. Inherited
///     rather than declared per command, because Spectre.Console.Cli has no global options of its own — this is what
///     makes <c>--profile</c> mean the same thing, and appear in the help, everywhere it is offered.
/// </summary>
internal class ProfileScopedSettings : CommandSettings
{
    [CommandOption("--profile <NAME>")]
    [Description("Act as the named profile for this command only, instead of the current one.")]
    public string? Profile { get; init; }
}
