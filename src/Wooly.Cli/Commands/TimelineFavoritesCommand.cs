using Spectre.Console;
using Wooly.Core.Profiles;
using Wooly.Core.Timelines;

namespace Wooly.Cli.Commands;

/// <summary>
///     Reads the posts this profile has favorited, newest favorite first — its own, as its bookmarks are, since
///     Mastodon keeps an account's favorites private to it.
/// </summary>
internal sealed class TimelineFavoritesCommand(
    IAnsiConsole console,
    IProfileRegistry profiles,
    ITimelineReader timelines) : TimelineCommand<TimelineSettings>(console, profiles, timelines)
{
    protected override Timeline TimelineToRead(ActiveProfile profile, TimelineSettings settings) =>
        Timeline.Favorites;
}
