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
///     turn out to be. <c>--type</c> narrows it where they do, and <c>--limit</c> and <c>--offset</c> page it (#599).
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

        /// <remarks>
        ///     No default of its own, unlike a timeline's <c>--limit</c>: left out, nothing is sent, and the instance
        ///     serves what it always has — so a search that names no limit prints what it did before there was one.
        /// </remarks>
        [CommandOption("--limit <COUNT>")]
        [Description("How many of each kind to ask for, from 1 to 40. The instance's own default otherwise.")]
        public int? Limit { get; init; }

        [CommandOption("--offset <N>")]
        [Description("How many results to skip, for the next page of one kind. Needs --type.")]
        public int? Offset { get; init; }

        [CommandOption("--json")]
        [Description("Write what was found as JSON, for another program to read.")]
        public bool Json { get; init; }

        /// <summary>
        ///     Which kind of result was asked for, which is all of them unless <c>--type</c> named one. A word that
        ///     names none of them cannot reach here: <see cref="Validate" /> has already turned the invocation down.
        /// </summary>
        public SearchKind Kind => SearchKindName.Parse(Type) ?? SearchKind.Everything;

        /// <summary>The page <c>--limit</c> and <c>--offset</c> ask for, neither of which is sent unless given.</summary>
        public SearchPage Page => SearchPage.Of(Limit, Offset);

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

            if (Type is not null && SearchKindName.Parse(Type) is null)
            {
                return ValidationResult.Error(SearchKindName.Rejection(Type));
            }

            if (SearchPage.Rejection(Limit, Offset, "--limit", "--offset") is { } rejection)
            {
                return ValidationResult.Error(rejection);
            }

            // Refused rather than sent: the instance would answer an offset without a kind with the first page again,
            // which a script paging through would take for the next one (#599).
            return Offset is not null && !SearchQuery.IsPageable(Kind)
                ? ValidationResult.Error($"--offset needs --type. {SearchQuery.PagingRejection}")
                : ValidationResult.Success();
        }
    }

    public override async Task<int> ExecuteAsync(
        CommandContext context,
        Settings settings,
        CancellationToken cancellationToken)
    {
        var profile = profiles.Resolve(settings.Profile);
        var query = SearchQuery.For(settings.Query, settings.Kind, settings.Page);

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
