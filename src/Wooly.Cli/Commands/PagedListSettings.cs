using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Wooly.Cli.Commands;

/// <summary>
///     What every command that reads a paged list takes: how many things to ask for, and whether the answer is for a
///     person or for another program. The collapse ADR-0010 and ADR-0011 both deferred until there was a third genuinely
///     paged list to collapse — timelines, notifications and now an account's followers — because three copies of a
///     <c>--limit</c> is three chances for one of them to accept a zero, or to stop paging where the others keep going.
/// </summary>
/// <remarks>
///     What a subclass still says for itself is what is being counted, which is the one part that cannot be shared: the
///     option's own description is an attribute, and an attribute is fixed for every command that inherits it.
/// </remarks>
internal abstract class PagedListSettings : ProfileScopedSettings
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
    [Description("How many to fetch. More than a page's worth is fetched by asking for further pages.")]
    [DefaultValue(DefaultLimit)]
    public int Limit { get; init; }

    [CommandOption("--json")]
    [Description("Write what was read as JSON, for another program to read.")]
    public bool Json { get; init; }

    /// <summary>What this list holds, singular, as the message turning down a limit of none names it.</summary>
    protected abstract string Counted { get; }

    public override ValidationResult Validate() =>
        Limit > 0
            ? ValidationResult.Success()
            : ValidationResult.Error($"--limit needs to be at least one {Counted}.");
}
