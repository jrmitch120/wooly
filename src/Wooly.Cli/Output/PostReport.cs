using System.Globalization;
using Spectre.Console;
using Wooly.Core.Posts;

namespace Wooly.Cli.Output;

/// <summary>
///     Writes a post for a person to read — both what became of one, and one in full.
///     <para>
///         What became of one is short on purpose: an author who has just published knows what they wrote, and what they
///         do not know is the id, which is how every later command names this post, and that it went out as narrowly as
///         they asked.
///     </para>
///     <para>
///         In full is <see cref="Write" />, and it lives here rather than wherever a post happens to be printed so that a
///         post read on its own reads the same as the same post read on a timeline. Everything that came from an instance
///         is written as text rather than markup, because a post's content is the author's and a square bracket in it is
///         not a colour tag.
///     </para>
/// </summary>
internal static class PostReport
{
    /// <summary>Reports the post that has just been published.</summary>
    public static void Published(IAnsiConsole console, Post post)
    {
        // The visibility comes off the published post rather than off the draft, so this reports what the instance
        // actually did — which for a draft that left the choice to the account is the only place it is knowable.
        console.MarkupLineInterpolated($"Posted [bold]{post.Id}[/] ({PostVisibilityName.Of(post.Visibility)}).");

        WriteAddress(console, post);
    }

    /// <summary>Reports the post that has just been changed.</summary>
    public static void Edited(IAnsiConsole console, Post post)
    {
        console.MarkupLineInterpolated($"Edited [bold]{post.Id}[/].");

        WriteAddress(console, post);
    }

    /// <summary>Reports the post that has just been taken down, which there is nothing left to link to.</summary>
    public static void Deleted(IAnsiConsole console, string postId) =>
        console.MarkupLineInterpolated($"Deleted post [bold]{postId}[/].");

    /// <summary>Reports the mark that has just been put on a post, or taken off it.</summary>
    /// <remarks>
    ///     The post named is the one the user named. Boosting answers with a post of the booster's own, and reporting
    ///     that one's id would hand back an id nothing else knows the post by — which is why the port unwraps it.
    /// </remarks>
    public static void Marked(IAnsiConsole console, Post post, PostMark mark, bool wanted)
    {
        console.MarkupLineInterpolated($"{Did(mark, wanted)} [bold]{post.Id}[/].");

        WriteAddress(console, post);
    }

    /// <summary>Reports one post asked for by id: the post itself, and where to read it on the web.</summary>
    /// <remarks>
    ///     The address is what a single post gets that a timeline's posts do not. A timeline is read down, and one
    ///     address per post would be a line of noise on every one of them; a post asked for by id is being looked at,
    ///     and the address is the thing that cannot be worked out from what is on screen.
    /// </remarks>
    public static void Shown(IAnsiConsole console, Post post)
    {
        Write(console, post);

        WriteAddress(console, post);
    }

    /// <summary>Writes the post itself: who wrote it, when, what it says, and how it has been received.</summary>
    public static void Write(IAnsiConsole console, Post post)
    {
        // A boost carries none of its own text, so what gets shown is the post it points at — with the account that
        // boosted it named above, which is the only part of the boost worth reading.
        var shown = post.Boosted ?? post;

        if (post.IsBoost)
        {
            console.MarkupLineInterpolated(
                $"[bold]{post.Account}[/] boosted [bold]{shown.Account}[/]  [dim]{PostedAt(shown)}[/]");
        }
        else
        {
            console.MarkupLineInterpolated($"[bold]{shown.Account}[/]  [dim]{PostedAt(shown)}[/]");
        }

        if (shown.ContentWarning is not null)
        {
            console.MarkupLineInterpolated($"  [yellow]content warning:[/] {shown.ContentWarning}");
        }

        // A post can have no text at all — one that is nothing but media, or a poll. Splitting an empty string would
        // yield one empty line, and print a post's worth of blank space for it.
        string[] lines = shown.Content.Length == 0 ? [] : shown.Content.Split('\n');

        foreach (var line in lines)
        {
            // The blank line between two paragraphs is indented like the rest of the post only if you cannot see it;
            // on screen it would be two spaces of trailing whitespace, and in a pipe it would be two spaces.
            if (line.Length == 0)
            {
                console.WriteLine();

                continue;
            }

            console.MarkupLineInterpolated($"  {line}");
        }

        console.MarkupLineInterpolated($"  [dim]{Counts(shown)}[/]");
    }

    /// <summary>
    ///     What just happened, in this project's vocabulary. One table, so that six commands cannot come to describe
    ///     three marks in more than six ways.
    /// </summary>
    private static string Did(PostMark mark, bool wanted) => (mark, wanted) switch
    {
        (PostMark.Boost, true) => "Boosted",
        (PostMark.Boost, false) => "Unboosted",
        (PostMark.Favorite, true) => "Favorited",
        (PostMark.Favorite, false) => "Unfavorited",
        (PostMark.Pin, true) => "Pinned",
        (PostMark.Pin, false) => "Unpinned",
        _ => throw new ArgumentOutOfRangeException(nameof(mark), mark, "Not a mark this client puts on a post."),
    };

    /// <summary>
    ///     Written without markup: an address is not this client's text to interpret, and a stray bracket in one would be
    ///     read as formatting rather than printed.
    /// </summary>
    private static void WriteAddress(IAnsiConsole console, Post post)
    {
        if (post.Url is not null)
        {
            console.WriteLine(post.Url);
        }
    }

    /// <summary>
    ///     Shown in this machine's own time zone: a person reading a post is placing it against their own day. Output
    ///     meant to be read back somewhere else is <c>--json</c>'s, and that stays UTC.
    /// </summary>
    private static string PostedAt(Post post) =>
        post.PostedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);

    private static string Counts(Post post) =>
        $"{Pluralize(post.Boosts, "boost")}, {Pluralize(post.Favorites, "favorite")}, "
        + $"{Pluralize(post.Replies, "reply", "replies")}";

    /// <param name="plural">Given only where adding an <c>s</c> would not make one.</param>
    private static string Pluralize(long count, string singular, string? plural = null) =>
        $"{count} {(count == 1 ? singular : plural ?? singular + "s")}";
}
