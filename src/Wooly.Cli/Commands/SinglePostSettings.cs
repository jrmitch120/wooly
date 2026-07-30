using System.ComponentModel;
using Spectre.Console.Cli;

namespace Wooly.Cli.Commands;

/// <summary>
///     What every command that names one post takes: the post's id, and whether the answer is for a person or for
///     another program. Declared once because seven commands take exactly this — the six marks and <c>post show</c> —
///     and seven copies would be seven chances for <c>--json</c> to mean something slightly different.
/// </summary>
internal class SinglePostSettings : ProfileScopedSettings
{
    [CommandArgument(0, "<ID>")]
    [Description("The id of the post, as shown by a timeline.")]
    public string PostId { get; init; } = string.Empty;

    [CommandOption("--json")]
    [Description("Write the post as JSON, for another program to read.")]
    public bool Json { get; init; }
}
