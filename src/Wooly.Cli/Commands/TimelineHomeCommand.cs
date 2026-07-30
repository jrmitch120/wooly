using Spectre.Console;
using Wooly.Core.Profiles;
using Wooly.Core.Timelines;

namespace Wooly.Cli.Commands;

/// <summary>Reads the posts of the accounts this profile follows.</summary>
internal sealed class TimelineHomeCommand(IAnsiConsole console, IProfileRegistry profiles, ITimelineReader timelines)
    : TimelineCommand<TimelineSettings>(console, profiles, timelines)
{
    protected override Timeline TimelineToRead(TimelineSettings settings) => Timeline.Home;
}
