using Spectre.Console;
using Wooly.Cli.Output;
using Wooly.Core.Posts;
using Wooly.Core.Profiles;
using Wooly.Core.Timelines;

namespace Wooly.Cli.Commands;

/// <summary>
///     What the four timeline commands read, which is a timeline and differs only in which one. That one difference is
///     what a subclass supplies; the reading, the writing and what a rate limit means are
///     <see cref="PagedListCommand{TSettings,TItem}" />'s, shared with every other list this client prints.
/// </summary>
internal abstract class TimelineCommand<TSettings>(
    IAnsiConsole console,
    IProfileRegistry profiles,
    ITimelineReader timelines) : PagedListCommand<TSettings, Post>(profiles)
    where TSettings : TimelineSettings
{
    /// <summary>Which timeline this command reads, given what the user typed.</summary>
    protected abstract Timeline TimelineToRead(TSettings settings);

    protected override PagedList<Post> Listing(ActiveProfile profile, TSettings settings)
    {
        // Settled once and closed over by all three, so that the timeline read and the timeline named in the output
        // cannot be worked out twice and disagree.
        var timeline = TimelineToRead(settings);

        return new PagedList<Post>(
            token => timelines.Read(profile, timeline, settings.Limit, token),
            fetch => TimelineJson.Write(console, timeline, fetch),
            fetch => TimelineReport.Write(console, timeline, fetch));
    }
}
