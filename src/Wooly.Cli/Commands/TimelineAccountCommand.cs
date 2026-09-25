using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;
using Wooly.Core.Profiles;
using Wooly.Core.Timelines;

namespace Wooly.Cli.Commands;

/// <summary>
///     Reads one account's own posts — the read the CLI turned out never to have had (#185), and the reason pinned posts
///     got nothing on this surface when ADR-0019 settled the account screen.
/// </summary>
/// <remarks>
///     Replies are left out and boosts left in by default, which is what the account screen asks for and what an
///     account's own page shows on the web. <c>--replies</c> widens that to the account's posts and replies — a seventh
///     timeline rather than a filter on this one, because a reply that was never fetched is one no pipe can get back
///     (#211). Boosts are not an option: a pipe can drop those itself.
/// </remarks>
internal sealed class TimelineAccountCommand(
    IAnsiConsole console,
    IProfileRegistry profiles,
    ITimelineReader timelines) : TimelineCommand<TimelineAccountCommand.Settings>(console, profiles, timelines)
{
    protected override Timeline TimelineToRead(ActiveProfile profile, Settings settings) =>
        settings.Replies
            ? Timeline.WithReplies(settings.Whose(profile.Instance))
            : Timeline.By(settings.Whose(profile.Instance));

    internal sealed class Settings : TimelineAccountSettings
    {
        [CommandOption("--replies")]
        [Description("Read the account's posts and replies: its answers to other people as well as its own posts.")]
        public bool Replies { get; init; }
    }
}
