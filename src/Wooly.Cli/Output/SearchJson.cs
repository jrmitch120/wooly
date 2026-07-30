using System.Text.Json.Serialization;
using Spectre.Console;
using Wooly.Core.Accounts;
using Wooly.Core.Search;

namespace Wooly.Cli.Output;

/// <summary>
///     Writes what a search found for another program to read. An object rather than three arrays, per ADR-0007, and
///     with one thing this client's other envelopes do not have: a kind that was not asked for is left out altogether,
///     while a kind that was asked for and found nothing is written as <c>[]</c>. That is the difference
///     <c>--type</c> would otherwise destroy — an empty <c>posts</c> in both cases would tell a script that nothing it
///     searched for had been posted, when in fact posts were never looked for.
///     <para>
///         The posts themselves are <see cref="PostDocument" />s, spelled the one way every command spells a post, and
///         an account names its address and display name with the same two words a post does.
///     </para>
/// </summary>
internal static class SearchJson
{
    public static void Write(IAnsiConsole console, SearchQuery query, SearchResults found)
    {
        var document = new FoundDocument(
            query.Text,
            found.Accounts?.Select(Of).ToList(),
            found.Hashtags?.Select(Of).ToList(),
            found.Posts?.Select(PostDocument.Of).ToList());

        JsonOutput.Write(console, document);
    }

    private static AccountDocument Of(Account account) => new(
        account.Address,
        account.Author,
        account.Followers,
        account.Following,
        account.Posts,
        account.Url);

    private static HashtagDocument Of(Hashtag hashtag) => new(
        hashtag.Name,
        hashtag.RecentPosts,
        hashtag.RecentAccounts,
        hashtag.Url);

    /// <param name="Query">
    ///     What was searched for, so that an answer read on its own — out of a file, or a log — still says what
    ///     question it belongs to.
    /// </param>
    private sealed record FoundDocument(
        [property: JsonPropertyName("query")] string Query,
        [property: JsonPropertyName("accounts")] IReadOnlyList<AccountDocument>? Accounts,
        [property: JsonPropertyName("hashtags")] IReadOnlyList<HashtagDocument>? Hashtags,
        [property: JsonPropertyName("posts")] IReadOnlyList<PostDocument>? Posts);

    /// <param name="Address">
    ///     Written as <c>account</c>, which is what a post calls the same fact, so one <c>jq</c> filter reads an
    ///     account's address wherever it turns up.
    /// </param>
    /// <param name="Posts">How many posts the account has published — not the posts a search found.</param>
    private sealed record AccountDocument(
        [property: JsonPropertyName("account")] string Address,
        [property: JsonPropertyName("author")] string Author,
        [property: JsonPropertyName("followers")] long Followers,
        [property: JsonPropertyName("following")] long Following,
        [property: JsonPropertyName("posts")] long Posts,
        [property: JsonPropertyName("url")] string? Url);

    /// <param name="Name">Bare, with no leading <c>#</c>, which is how <c>timeline tag</c> takes one.</param>
    private sealed record HashtagDocument(
        [property: JsonPropertyName("hashtag")] string Name,
        [property: JsonPropertyName("recentPosts")] long RecentPosts,
        [property: JsonPropertyName("recentAccounts")] long RecentAccounts,
        [property: JsonPropertyName("url")] string? Url);
}
