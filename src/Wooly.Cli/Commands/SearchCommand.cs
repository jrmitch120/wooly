using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;
using Wooly.Cli.Output;
using Wooly.Core.Profiles;
using Wooly.Core.Search;

namespace Wooly.Cli.Commands;

/// <summary>
///     Finds accounts, hashtags and posts, in one command rather than one per kind — which is what makes a search for a
///     word a user half-remembers worth typing at all, since they rarely know in advance which of the three it will
///     turn out to be. <c>--type</c> narrows it where they do.
/// </summary>
internal sealed class SearchCommand(IAnsiConsole console, IProfileRegistry profiles, IInstanceSearch search)
    : AsyncCommand<SearchCommand.Settings>
{
    internal sealed class Settings : ProfileScopedSettings
    {
        [CommandArgument(0, "<QUERY>")]
        [Description("What to look for: a word, a #hashtag, an @account, or the web address of one of them.")]
        public string Query { get; init; } = string.Empty;

        [CommandOption("--type <KIND>")]
        [Description("Look only for accounts, hashtags or posts, instead of all three.")]
        public string? Type { get; init; }

        [CommandOption("--json")]
        [Description("Write what was found as JSON, for another program to read.")]
        public bool Json { get; init; }

        /// <summary>
        ///     Which kind of result was asked for, which is all of them unless <c>--type</c> named one. A word that
        ///     names none of them cannot reach here: <see cref="Validate" /> has already turned the invocation down.
        /// </summary>
        public SearchKind Kind => SearchKindName.Parse(Type) ?? SearchKind.Everything;

        public override ValidationResult Validate()
        {
            // Both checked here as well as in the domain so that the two commonest ways of getting this wrong — an
            // empty query left by a shell that ate the argument, and a --type spelled some other plausible way — are
            // answered by the argument parser, against the value the user just typed, rather than as a defect further
            // in. Each site asks the same rule, so neither can start saying something the other does not.
            if (!SearchQuery.IsWellFormed(Query))
            {
                return ValidationResult.Error(SearchQuery.Rejection);
            }

            return Type is null || SearchKindName.Parse(Type) is not null
                ? ValidationResult.Success()
                : ValidationResult.Error(SearchKindName.Rejection(Type));
        }
    }

    protected override async Task<int> ExecuteAsync(
        CommandContext context,
        Settings settings,
        CancellationToken cancellationToken)
    {
        var profile = profiles.Resolve(settings.Profile);
        var query = SearchQuery.For(settings.Query, settings.Kind);

        // Nothing is caught here. A search is one call to the instance, so a rate limit leaves nothing to print before
        // it is reported — which makes it an ordinary failure for ADR-0006's one handler, rather than the partial
        // answer a timeline read hands back (ADR-0011).
        var found = await search.Find(profile, query, cancellationToken);

        if (settings.Json)
        {
            SearchJson.Write(console, query, found);
        }
        else
        {
            SearchReport.Write(console, query, found);
        }

        return (int)ExitCode.Success;
    }
}
