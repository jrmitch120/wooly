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
    /// <remarks>
    ///     Which timeline this was leads the envelope, as <c>timeline</c> and — where one was asked for — the
    ///     <c>hashtag</c> or the <c>account</c>, so that a timeline read out of a file still says which one it is. The
    ///     account is written in full, <c>user@host</c>, which is what the command resolved a bare username into before
    ///     the read: a file saying <c>@maria</c> says nothing about which server her posts came from.
    /// </remarks>
    public static void Write(IAnsiConsole console, Timeline timeline, Fetch<Post> fetch) =>
        ListDocument.Write(
            console,
            fetch,
            PostDocument.Of,
            "posts",
            ("timeline", NameOf(timeline.Scope)),
            ("hashtag", timeline.Hashtag),
            ("account", timeline.Account?.Address.Text));

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
        TimelineScope.Account => "account",
        TimelineScope.Pinned => "pinned",
        _ => throw new ArgumentOutOfRangeException(nameof(scope), scope, "Not a timeline this client reads."),
    };
}
