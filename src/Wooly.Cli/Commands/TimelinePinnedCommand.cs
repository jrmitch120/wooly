using Spectre.Console;
using Wooly.Core.Profiles;
using Wooly.Core.Timelines;

namespace Wooly.Cli.Commands;

/// <summary>
///     Reads the run of posts an account has fastened to the top of their own profile. A command of its own rather than
///     a flag on <see cref="TimelineAccountCommand" />, because it is a different reading rather than a filter over the
///     same one: a pin is often a reply, which the account timeline leaves out, and an instance reports a post's own pin
///     mark only to whoever wrote it (ADR-0019).
/// </summary>
internal sealed class TimelinePinnedCommand(
    IAnsiConsole console,
    IProfileRegistry profiles,
    ITimelineReader timelines) : TimelineCommand<TimelineAccountSettings>(console, profiles, timelines)
{
    protected override Timeline TimelineToRead(ActiveProfile profile, TimelineAccountSettings settings) =>
        Timeline.Pinned(settings.Whose(profile.Instance));
}
