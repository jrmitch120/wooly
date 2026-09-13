using Spectre.Console;
using Wooly.Cli.Output;
using Wooly.Core.Posts;
using Wooly.Core.Profiles;
using Wooly.Core.Timelines;

namespace Wooly.Cli.Commands;

/// <summary>
///     What the six timeline commands read, which is a timeline and differs only in which one. That one difference is
///     what a subclass supplies; the reading, the writing and what a rate limit means are
///     <see cref="PagedListCommand{TSettings,TItem}" />'s, shared with every other list this client prints.
/// </summary>
internal abstract class TimelineCommand<TSettings>(
    IAnsiConsole console,
    IProfileRegistry profiles,
    ITimelineReader timelines) : PagedListCommand<TSettings, Post>(profiles)
    where TSettings : TimelineSettings
{
    /// <summary>
    ///     Which timeline this command reads, given the profile to read as and what the user typed. The profile is here
    ///     for the two timelines that are about a person: a bare username is somebody on the profile's own instance, and
    ///     an address is resolved into one an instance would recognise before the timeline is built rather than after
    ///     (<see cref="TimelineAccountSettings.Whose" />).
    /// </summary>
    protected abstract Timeline TimelineToRead(ActiveProfile profile, TSettings settings);

    protected override Listing<Post> ListingFor(ActiveProfile profile, TSettings settings)
    {
        // Settled once and closed over by all three, so that the timeline read and the timeline named in the output
        // cannot be worked out twice and disagree.
        var timeline = TimelineToRead(profile, settings);

        return new Listing<Post>(
            token => timelines.Read(profile, timeline, settings.Limit, token),
            fetch => TimelineJson.Write(console, timeline, fetch),
            fetch => TimelineReport.Write(console, timeline, fetch));
    }
}
