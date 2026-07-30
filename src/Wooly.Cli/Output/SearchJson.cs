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
///         The posts and the accounts themselves are <see cref="PostDocument" />s and <see cref="AccountDocument" />s,
///         spelled the one way every command spells each — an account names its address and display name with the same
///         two words a post does.
///     </para>
/// </summary>
internal static class SearchJson
{
    public static void Write(IAnsiConsole console, SearchQuery query, SearchResults found)
    {
        var document = new FoundDocument(
            query.Text,
            found.Accounts?.Select(AccountDocument.Of).ToList(),
            found.Hashtags?.Select(Of).ToList(),
            found.Posts?.Select(PostDocument.Of).ToList());

        JsonOutput.Write(console, document);
    }

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

    /// <param name="Name">Bare, with no leading <c>#</c>, which is how <c>timeline tag</c> takes one.</param>
    private sealed record HashtagDocument(
        [property: JsonPropertyName("hashtag")] string Name,
        [property: JsonPropertyName("recentPosts")] long RecentPosts,
        [property: JsonPropertyName("recentAccounts")] long RecentAccounts,
        [property: JsonPropertyName("url")] string? Url);
}
