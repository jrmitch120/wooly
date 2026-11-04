using Spectre.Console;
using Wooly.Core.Paging;
using Wooly.Core.Posts;
using Wooly.Core.Timelines;

namespace Wooly.Cli.Output;

/// <summary>
///     Writes a timeline for a person to read: the posts, one after another, with a blank line between them. Each post
///     is written by <see cref="PostReport.Write" /> rather than here, so that a post read on a timeline and the same
///     post read on its own cannot come to look like two different posts.
/// </summary>
internal static class TimelineReport
{
    public static void Write(IAnsiConsole console, Timeline timeline, Fetch<Post> fetch)
    {
        if (fetch.Items.Count == 0)
        {
            // Only when the timeline really is empty. A fetch a rate limit stopped before anything arrived is
            // reported as that failure, and saying "no posts" as well would be saying the opposite of what happened.
            if (fetch.IsComplete)
            {
                // Written as text rather than markup, a hashtag being the user's own. A run of marks says what has not
                // been done yet, in the words the TUI says it in.
                console.WriteLine(timeline.NothingMarked ?? $"No posts in {timeline.Description}.");
            }

            return;
        }

        foreach (var post in fetch.Items)
        {
            PostReport.Write(console, post);
            console.WriteLine();
        }
    }
}
