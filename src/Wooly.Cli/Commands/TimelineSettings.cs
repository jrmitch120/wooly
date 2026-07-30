using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Wooly.Cli.Commands;

/// <summary>
///     What every timeline command takes, whichever timeline it reads: which profile to read as, how many posts to
///     ask for, and whether the answer is for a person or for another program.
/// </summary>
internal class TimelineSettings : ProfileScopedSettings
{
    /// <summary>
    ///     A screen's worth. Small enough that the command answers quickly, and — being under one page — small enough
    ///     that the ordinary invocation costs the instance a single call.
    /// </summary>
    private const int DefaultLimit = 20;

    /// <remarks>
    ///     The default is stated once, as the attribute Spectre both applies to an invocation that omits the option and
    ///     shows in the help. An initializer as well would be the same fact written twice, able to disagree.
    /// </remarks>
    [CommandOption("--limit <COUNT>")]
    [Description("How many posts to fetch. More than a page's worth is fetched by asking for further pages.")]
    [DefaultValue(DefaultLimit)]
    public int Limit { get; init; }

    [CommandOption("--json")]
    [Description("Write the timeline as JSON, for another program to read.")]
    public bool Json { get; init; }

    public override ValidationResult Validate() =>
        Limit > 0
            ? ValidationResult.Success()
            : ValidationResult.Error("--limit needs to be at least one post.");
}
