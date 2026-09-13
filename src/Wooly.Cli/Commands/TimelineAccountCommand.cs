using Spectre.Console;
using Wooly.Core.Profiles;
using Wooly.Core.Timelines;

namespace Wooly.Cli.Commands;

/// <summary>
///     Reads one account's own posts — the read the CLI turned out never to have had (#185), and the reason pinned posts
///     got nothing on this surface when ADR-0019 settled the account screen.
/// </summary>
/// <remarks>
///     Replies are left out and boosts left in, which is what the account screen asks for and what an account's own page
///     shows on the web. Neither is an option here: varying them is a change to the shape of <see cref="Timeline" />
///     itself, which ADR-0019 is explicit should not ride in behind a feature (#211).
/// </remarks>
internal sealed class TimelineAccountCommand(
    IAnsiConsole console,
    IProfileRegistry profiles,
    ITimelineReader timelines) : TimelineCommand<TimelineAccountSettings>(console, profiles, timelines)
{
    protected override Timeline TimelineToRead(ActiveProfile profile, TimelineAccountSettings settings) =>
        Timeline.By(settings.Whose(profile.Instance));
}
