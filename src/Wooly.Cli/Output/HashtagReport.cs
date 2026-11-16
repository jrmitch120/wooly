using Spectre.Console;
using Wooly.Core.Search;

namespace Wooly.Cli.Output;

/// <summary>
///     Writes a hashtag for a person to read — one way for every command that writes one, <c>search</c> and
///     <c>trends</c> alike, as <see cref="PostReport" /> is for a post: a trending hashtag being a hashtag (#598's review).
/// </summary>
internal static class HashtagReport
{
    /// <summary>
    ///     One hashtag: the tag as <c>timeline tag</c> takes it, and how much use it has had. No web address, unlike an
    ///     account — the tag itself is what the next command takes, and a line of address under every tag on a list read
    ///     down is noise.
    /// </summary>
    public static void Write(IAnsiConsole console, Hashtag hashtag)
    {
        console.MarkupLineInterpolated($"[bold]#{hashtag.Name}[/]");

        // An instance that sent no usage at all is one this has nothing to say about, and "0 posts from 0 accounts"
        // would say something — that nobody is posting to a tag which may well be busy.
        if (hashtag.RecentPosts > 0 || hashtag.RecentAccounts > 0)
        {
            console.MarkupLineInterpolated($"  [dim]{Use(hashtag)}[/]");
        }

        console.WriteLine();
    }

    private static string Use(Hashtag hashtag) =>
        $"{Plural.Of(hashtag.RecentPosts, "post")} from {Plural.Of(hashtag.RecentAccounts, "account")} recently";
}
