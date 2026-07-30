using Spectre.Console;
using Wooly.Core.Profiles;
using Wooly.Core.Timelines;

namespace Wooly.Cli.Commands;

/// <summary>Reads the public posts reaching this profile's instance from everywhere it federates with.</summary>
internal sealed class TimelineFederatedCommand(
    IAnsiConsole console,
    IProfileRegistry profiles,
    ITimelineReader timelines) : TimelineCommand<TimelineSettings>(console, profiles, timelines)
{
    protected override Timeline TimelineToRead(TimelineSettings settings) => Timeline.Federated;
}
