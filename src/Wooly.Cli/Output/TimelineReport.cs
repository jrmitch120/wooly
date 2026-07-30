using System.Globalization;
using Spectre.Console;
using Wooly.Core.Timelines;

namespace Wooly.Cli.Output;

/// <summary>
///     Writes a timeline for a person to read. Everything on screen is in CONTEXT.md's vocabulary — posts, boosts,
///     favorites — and every value that came from an instance is written as text rather than markup, because a post's
///     content is the author's and a square bracket in it is not a colour tag.
/// </summary>
internal static class TimelineReport
{
    public static void Write(IAnsiConsole console, Timeline timeline, TimelineFetch fetch)
    {
        if (fetch.Posts.Count == 0)
        {
            // Only when the timeline really is empty. A fetch a rate limit stopped before anything arrived is
            // reported as that failure, and saying "no posts" as well would be saying the opposite of what happened.
            if (fetch.IsComplete)
            {
                console.MarkupLineInterpolated($"No posts in {timeline.Description}.");
            }

            return;
        }

        foreach (var post in fetch.Posts)
        {
            WritePost(console, post);
        }
    }

    private static void WritePost(IAnsiConsole console, Post post)
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
        console.WriteLine();
    }

    /// <summary>
    ///     Shown in this machine's own time zone: a person reading their timeline is placing posts against their own
    ///     day. Output meant to be read back somewhere else is <c>--json</c>'s, and that stays UTC.
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
