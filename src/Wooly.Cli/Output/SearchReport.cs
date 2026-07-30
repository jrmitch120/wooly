using Spectre.Console;
using Wooly.Core.Accounts;
using Wooly.Core.Search;

namespace Wooly.Cli.Output;

/// <summary>
///     Writes what a search found for a person to read: the accounts, then the hashtags, then the posts, each under a
///     heading naming what follows. The posts are written by <see cref="PostReport.Write" /> rather than here, so that a
///     post found by a search and the same post read on a timeline cannot come to look like two different posts.
/// </summary>
internal static class SearchReport
{
    /// <summary>Writes the results, or says that there were none.</summary>
    public static void Write(IAnsiConsole console, SearchQuery query, SearchResults found)
    {
        if (found.IsEmpty)
        {
            console.MarkupLineInterpolated($"{NothingMatched(query)}");

            return;
        }

        // A search narrowed to one kind has every line on screen of that kind, and a heading over the only thing
        // there is says nothing the command line did not already say.
        var headings = query.Kind is SearchKind.Everything;

        if (found.Accounts is { Count: > 0 } accounts)
        {
            WriteHeading(console, "Accounts", headings);

            foreach (var account in accounts)
            {
                Write(console, account);
            }
        }

        if (found.Hashtags is { Count: > 0 } hashtags)
        {
            WriteHeading(console, "Hashtags", headings);

            foreach (var hashtag in hashtags)
            {
                Write(console, hashtag);
            }
        }

        if (found.Posts is { Count: > 0 } posts)
        {
            WriteHeading(console, "Posts", headings);

            foreach (var post in posts)
            {
                PostReport.Write(console, post);
                console.WriteLine();
            }
        }
    }

    /// <summary>
    ///     What a search that found nothing says, in the words of what it was asked for: "nothing matching" after a
    ///     search narrowed to accounts would read as though the hashtags and the posts had been looked at too.
    /// </summary>
    private static string NothingMatched(SearchQuery query) => query.Kind is SearchKind.Everything
        ? $"Nothing matching '{query.Text}'."
        : $"No {SearchKindName.Of(query.Kind)} matching '{query.Text}'.";

    private static void WriteHeading(IAnsiConsole console, string heading, bool wanted)
    {
        if (wanted)
        {
            console.MarkupLineInterpolated($"[bold]{heading}[/]");
        }
    }

    /// <summary>
    ///     One account: who they are, how much of a presence they have, and where to read them. The address leads,
    ///     because it is what every <c>account</c> command asks the user to type.
    /// </summary>
    private static void Write(IAnsiConsole console, Account account)
    {
        console.MarkupLineInterpolated($"[bold]{account.Address}[/]  {account.Author}");
        console.MarkupLineInterpolated($"  [dim]{Presence(account)}[/]");

        // Written without markup: an address is not this client's text to interpret, and a stray bracket in one would
        // be read as formatting rather than printed.
        if (account.Url is not null)
        {
            console.WriteLine($"  {account.Url}");
        }

        console.WriteLine();
    }

    /// <summary>
    ///     One hashtag: the tag as <c>timeline tag</c> takes it, and how much use it has had. No web address, unlike an
    ///     account — the tag itself is what the next command takes, and a line of address under every tag on a list read
    ///     down is noise.
    /// </summary>
    private static void Write(IAnsiConsole console, Hashtag hashtag)
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

    private static string Presence(Account account) =>
        $"{Plural.Of(account.Followers, "follower")}, {Plural.Of(account.Posts, "post")}, "
        + $"following {Plural.Of(account.Following, "account")}";

    private static string Use(Hashtag hashtag) =>
        $"{Plural.Of(hashtag.RecentPosts, "post")} from {Plural.Of(hashtag.RecentAccounts, "account")} recently";
}
