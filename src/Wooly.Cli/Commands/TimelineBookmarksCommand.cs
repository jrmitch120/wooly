using Spectre.Console;
using Wooly.Core.Profiles;
using Wooly.Core.Timelines;

namespace Wooly.Cli.Commands;

/// <summary>
///     Reads the posts this profile has bookmarked, newest bookmark first. Nobody is named: the run is the profile's
///     own, so which one is read is <c>--profile</c>'s to say.
/// </summary>
internal sealed class TimelineBookmarksCommand(
    IAnsiConsole console,
    IProfileRegistry profiles,
    ITimelineReader timelines) : TimelineCommand<TimelineSettings>(console, profiles, timelines)
{
    protected override Timeline TimelineToRead(ActiveProfile profile, TimelineSettings settings) =>
        Timeline.Bookmarks;
}
