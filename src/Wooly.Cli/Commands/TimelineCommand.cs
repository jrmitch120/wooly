using Spectre.Console;
using Spectre.Console.Cli;
using Wooly.Cli.Output;
using Wooly.Core.Profiles;
using Wooly.Core.Timelines;

namespace Wooly.Cli.Commands;

/// <summary>
///     Everything the four timeline commands do, which is everything except deciding which timeline they read. That
///     one difference is what a subclass supplies; the rest — resolving the profile, asking for the posts, choosing
///     between output for a person and output for a program, and what a rate limit means — happens identically, so
///     that no timeline can come to behave unlike the others.
/// </summary>
internal abstract class TimelineCommand<TSettings>(
    IAnsiConsole console,
    IProfileRegistry profiles,
    ITimelineReader timelines) : AsyncCommand<TSettings>
    where TSettings : TimelineSettings
{
    /// <summary>Which timeline this command reads, given what the user typed.</summary>
    protected abstract Timeline TimelineToRead(TSettings settings);

    protected override async Task<int> ExecuteAsync(
        CommandContext context,
        TSettings settings,
        CancellationToken cancellationToken)
    {
        var profile = profiles.Resolve(settings.Profile);
        var timeline = TimelineToRead(settings);
        var fetch = await timelines.Read(profile, timeline, settings.Limit, cancellationToken);

        if (settings.Json)
        {
            TimelineJson.Write(console, timeline, fetch);
        }
        else
        {
            TimelineReport.Write(console, timeline, fetch);
        }

        // The posts that did arrive are worth having, so they are written before the limit that stopped the rest is
        // reported at all. Reporting it is ADR-0006's one handler's job — hence throwing rather than printing, which
        // is also what puts the rate-limited exit code on the process.
        if (fetch.StoppedBy is not null)
        {
            throw fetch.StoppedBy;
        }

        return (int)ExitCode.Success;
    }
}
