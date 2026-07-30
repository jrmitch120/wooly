using Spectre.Console;
using Wooly.Core.Accounts;
using Wooly.Core.Posts;
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
        var withHeadings = query.Kind is SearchKind.Everything;

        WriteSection(console, "Accounts", found.Accounts, withHeadings, WriteAccount);
        WriteSection(console, "Hashtags", found.Hashtags, withHeadings, Write);
        WriteSection(console, "Posts", found.Posts, withHeadings, WritePost);
    }

    /// <summary>
    ///     Writes one kind of result under a heading naming it, or nothing at all where there was none of that kind —
    ///     an empty heading is a promise of something to read.
    /// </summary>
    private static void WriteSection<TResult>(
        IAnsiConsole console,
        string heading,
        IReadOnlyList<TResult>? found,
        bool withHeadings,
        Action<IAnsiConsole, TResult> write)
    {
        if (found is not { Count: > 0 })
        {
            return;
        }

        if (withHeadings)
        {
            console.MarkupLineInterpolated($"[bold]{heading}[/]");
        }

        foreach (var result in found)
        {
            write(console, result);
        }
    }

    /// <summary>
    ///     What a search that found nothing says, in the words of what it was asked for: "nothing matching" after a
    ///     search narrowed to accounts would read as though the hashtags and the posts had been looked at too.
    /// </summary>
    private static string NothingMatched(SearchQuery query) => query.Kind is SearchKind.Everything
        ? $"Nothing matching '{query.Text}'."
        : $"No {SearchKindName.Of(query.Kind)} matching '{query.Text}'.";

    /// <summary>
    ///     One account, written by <see cref="AccountReport.Write(IAnsiConsole,Account)" /> so that an account a search
    ///     found and the same account in a followers list cannot come to look like two different accounts.
    /// </summary>
    private static void WriteAccount(IAnsiConsole console, Account account)
    {
        AccountReport.Write(console, account);
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

    /// <summary>
    ///     One post, written by <see cref="PostReport.Write" /> so that a post found by a search and the same post read
    ///     on a timeline cannot come to look like two different posts.
    /// </summary>
    private static void WritePost(IAnsiConsole console, Post post)
    {
        PostReport.Write(console, post);
        console.WriteLine();
    }

    private static string Use(Hashtag hashtag) =>
        $"{Plural.Of(hashtag.RecentPosts, "post")} from {Plural.Of(hashtag.RecentAccounts, "account")} recently";
}
