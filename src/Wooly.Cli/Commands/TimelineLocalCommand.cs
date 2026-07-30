using Spectre.Console;
using Wooly.Core.Profiles;
using Wooly.Core.Timelines;

namespace Wooly.Cli.Commands;

/// <summary>Reads the public posts of accounts on this profile's own instance.</summary>
internal sealed class TimelineLocalCommand(IAnsiConsole console, IProfileRegistry profiles, ITimelineReader timelines)
    : TimelineCommand<TimelineSettings>(console, profiles, timelines)
{
    protected override Timeline TimelineToRead(TimelineSettings settings) => Timeline.Local;
}
