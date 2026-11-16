using Spectre.Console;
using Wooly.Core.Posts;
using Wooly.Core.Trends;

namespace Wooly.Cli.Output;

/// <summary>
///     Writes what is trending for a person to read: the hashtags, then the posts, then the links, each under a heading
///     naming what follows (#601). A trending hashtag is written as a searched one is and a trending post as a post on a
///     timeline, so a trend cannot come to look like a different thing from the same hashtag or post found elsewhere.
/// </summary>
internal static class TrendsReport
{
    /// <summary>Writes what is trending on <paramref name="instance" />, or says that nothing is.</summary>
    /// <param name="asked">The kinds asked for, which decides both the headings and how an empty answer is put.</param>
    public static void Write(
        IAnsiConsole console,
        string instance,
        IReadOnlyList<TrendKind> asked,
        TrendsReading trending)
    {
        // An instance with trends turned off answers with nothing, and printing nothing at all would leave a user unable
        // to tell that from a broken client.
        if (trending.IsEmpty)
        {
            console.MarkupLineInterpolated($"{NothingTrending(instance, asked)}");

            return;
        }

        // One kind asked has every line of that kind, and a heading over the only thing there is says nothing the
        // command line did not — as search's report leaves it out.
        var withHeadings = asked.Count > 1;

        WriteSection(console, "Trending hashtags", trending.Hashtags, withHeadings, SearchReport.Write);
        WriteSection(console, "Trending posts", trending.Posts, withHeadings, WritePost);
        WriteSection(console, "Trending links", trending.Links, withHeadings, Write);
    }

    /// <summary>
    ///     Said in the words of what was asked for: "nothing trending" after <c>--type links</c> would read as though the
    ///     hashtags and posts had been looked at too.
    /// </summary>
    private static string NothingTrending(string instance, IReadOnlyList<TrendKind> asked) => asked.Count > 1
        ? $"Nothing trending on {instance}."
        : $"No trending {TrendKindName.Of(asked[0])} on {instance}.";

    /// <summary>One kind under its heading, or nothing at all where nothing of it is trending.</summary>
    private static void WriteSection<TTrend>(
        IAnsiConsole console,
        string heading,
        IReadOnlyList<TTrend>? trending,
        bool withHeadings,
        Action<IAnsiConsole, TTrend> write)
    {
        if (trending is not { Count: > 0 })
        {
            return;
        }

        if (withHeadings)
        {
            console.MarkupLineInterpolated($"[bold]{heading}[/]");
        }

        foreach (var trend in trending)
        {
            write(console, trend);
        }
    }

    private static void WritePost(IAnsiConsole console, Post post)
    {
        PostReport.Write(console, post);
        console.WriteLine();
    }

    /// <summary>
    ///     One link: its title, where it goes — the address being what <c>timeline link</c> takes and a browser opens —
    ///     and whose site it is on with how many are sharing it. No description, which is the TUI's second row: on a list
    ///     read down in a terminal, a paragraph under every link is the article's blurb rather than the trend.
    /// </summary>
    private static void Write(IAnsiConsole console, TrendingLink link)
    {
        console.MarkupLineInterpolated($"[bold]{link.Title}[/]");
        console.MarkupLineInterpolated($"  {link.Url.OriginalString}");

        var sharing = $"{Plural.Of(link.PeopleSharing, "person", "people")} sharing";

        console.MarkupLineInterpolated($"  [dim]{link.Site} · {sharing}[/]");
        console.WriteLine();
    }
}
