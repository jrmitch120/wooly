using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;
using Wooly.Cli.Output;
using Wooly.Core.Profiles;
using Wooly.Core.Trends;

namespace Wooly.Cli.Commands;

/// <summary>
///     Lists what the instance ranks as trending — hashtags, posts and trending links — in one command, mirroring
///     <see cref="SearchCommand" /> (#601). <c>--type</c> asks for one kind; <c>--limit</c> and <c>--offset</c> page any
///     kind, since every trends endpoint pages by offset, so unlike search's <c>--offset</c> this one needs no
///     <c>--type</c> and applies to each kind asked.
/// </summary>
internal sealed class TrendsCommand(IAnsiConsole console, IProfileRegistry profiles, ITrends trends)
    : AsyncCommand<TrendsCommand.Settings>
{
    internal sealed class Settings : ProfileScopedSettings
    {
        [CommandOption("--type <KIND>")]
        [Description("Read only trending tags, posts or links, instead of all three.")]
        public string? Type { get; init; }

        [CommandOption("--limit <COUNT>")]
        [Description("How many of each kind to read: at most 20 tags or links, or 40 posts. The instance's own default "
                     + "(10 tags, 20 posts, 10 links) when not given.")]
        public int? Limit { get; init; }

        [CommandOption("--offset <N>")]
        [Description("How many of each kind to pass over first, to page through the trends.")]
        [DefaultValue(0)]
        public int Offset { get; init; }

        [CommandOption("--json")]
        [Description("Write what is trending as JSON, for another program to read.")]
        public bool Json { get; init; }

        /// <summary>
        ///     The kinds asked for, in the order they are written: all three unless <c>--type</c> named one. A word that
        ///     names none cannot reach here — <see cref="Validate" /> has already turned the invocation down.
        /// </summary>
        public IReadOnlyList<TrendKind> Kinds =>
            TrendKindName.Parse(Type) is { } kind ? [kind] : TrendKindName.All;

        public override ValidationResult Validate()
        {
            // Turned down here, against what was typed and before anything is asked, by the domain's own rules — so a
            // limit the instance would quietly cut short is a usage error rather than a short page a script reads as
            // the end of the ranking (ADR-0006).
            if (Type is not null && TrendKindName.Parse(Type) is null)
            {
                return ValidationResult.Error(TrendKindName.Rejection(Type));
            }

            return TrendPage.Rejection(Limit, Offset, Kinds.ToList()) is { } rejection
                ? ValidationResult.Error(rejection)
                : ValidationResult.Success();
        }
    }

    public override async Task<int> ExecuteAsync(
        CommandContext context,
        Settings settings,
        CancellationToken cancellationToken)
    {
        var profile = profiles.Resolve(settings.Profile);
        var page = settings.Limit is null && settings.Offset == 0
            ? TrendPage.First
            : TrendPage.Of(settings.Limit, settings.Offset);
        var kinds = settings.Kinds;

        // Nothing is caught here. Each kind is one call, so a rate limit leaves nothing worth printing — an ordinary
        // failure for ADR-0006's one handler, as a search's is (ADR-0011). Asked one after another rather than at once,
        // so a refusal of the first does not leave two more calls spending the quota it just ran out of.
        var trending = new TrendsReading(
            kinds.Contains(TrendKind.Hashtags) ? await trends.Hashtags(profile, page, cancellationToken) : null,
            kinds.Contains(TrendKind.Posts) ? await trends.Posts(profile, page, cancellationToken) : null,
            kinds.Contains(TrendKind.Links) ? await trends.Links(profile, page, cancellationToken) : null);

        if (settings.Json)
        {
            TrendsJson.Write(console, trending);
        }
        else
        {
            TrendsReport.Write(console, profile.Instance, kinds, trending);
        }

        return (int)ExitCode.Success;
    }
}
