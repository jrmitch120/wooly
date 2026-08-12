using Spectre.Console;
using Wooly.Core.Paging;
using Wooly.Core.Posts;
using Wooly.Core.Timelines;

namespace Wooly.Cli.Output;

/// <summary>
///     Writes a timeline for another program to read: <see cref="ListDocument" />'s envelope, led by which timeline
///     this was. The posts themselves are <see cref="PostDocument" />s, spelled the one way every command spells a
///     post.
/// </summary>
internal static class TimelineJson
{
    public static void Write(IAnsiConsole console, Timeline timeline, Fetch<Post> fetch) =>
        ListDocument.Write(
            console,
            fetch,
            PostDocument.Of,
            "posts",
            ("timeline", NameOf(timeline.Scope)),
            ("hashtag", timeline.Hashtag));

    /// <summary>
    ///     What each timeline is called in the output. Spelled out for the same reason the field names are: derived from
    ///     the enum's own member names, renaming one would silently change a value somebody is matching on.
    /// </summary>
    private static string NameOf(TimelineScope scope) => scope switch
    {
        TimelineScope.Home => "home",
        TimelineScope.Local => "local",
        TimelineScope.Federated => "federated",
        TimelineScope.Tag => "tag",
        _ => throw new ArgumentOutOfRangeException(nameof(scope), scope, "Not a timeline this client reads."),
    };
}
