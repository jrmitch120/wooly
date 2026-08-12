using Spectre.Console.Cli;
using Wooly.Core.Profiles;

namespace Wooly.Cli.Commands;

/// <summary>
///     Everything a command that reads a paged list does, which is everything except which list it reads. That is what
///     a subclass supplies, as a <see cref="PagedList{T}" />; the rest — resolving the profile, choosing between output
///     for a person and output for a program, and what a rate limit means — happens identically, so that no list can
///     come to behave unlike the others.
/// </summary>
/// <remarks>
///     Five commands wrote this sequence out, and the copy that mattered was the last step: a fetch a rate limit
///     stopped is thrown after what arrived has been written, which is what keeps ADR-0006's promise that no command
///     writes its own error text and what puts the rate-limited exit code on the process. Five copies of that is four
///     chances for one list to print the limit itself, or to swallow it and exit zero (#101).
/// </remarks>
internal abstract class PagedListCommand<TSettings, TItem>(IProfileRegistry profiles) : AsyncCommand<TSettings>
    where TSettings : PagedListSettings
{
    /// <summary>Which list this command reads, given the profile to read as and what the user typed.</summary>
    protected abstract PagedList<TItem> Listing(ActiveProfile profile, TSettings settings);

    protected override async Task<int> ExecuteAsync(
        CommandContext context,
        TSettings settings,
        CancellationToken cancellationToken)
    {
        var profile = profiles.Resolve(settings.Profile);
        var listing = Listing(profile, settings);
        var fetch = await listing.Reads(cancellationToken);

        if (settings.Json)
        {
            listing.AsJson(fetch);
        }
        else
        {
            listing.AsReport(fetch);
        }

        // What did arrive is worth having, so it is written before the limit that stopped the rest is reported at all.
        // Reporting it is ADR-0006's one handler's job — hence throwing rather than printing, which is also what puts
        // the rate-limited exit code on the process.
        if (fetch.StoppedBy is not null)
        {
            throw fetch.StoppedBy;
        }

        return (int)ExitCode.Success;
    }
}
