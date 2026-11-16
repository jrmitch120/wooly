using System.Text.Json.Serialization;
using Spectre.Console;
using Wooly.Core.Trends;

namespace Wooly.Cli.Output;

/// <summary>
///     Writes what is trending for another program to read: an object of three lists, with a kind that was not asked for
///     left out and a kind with nothing trending written as <c>[]</c>, as <see cref="SearchJson" /> does (ADR-0011).
///     Hashtags and posts are the documents every command writes for one; a trending link is the one new document.
/// </summary>
internal static class TrendsJson
{
    public static void Write(IAnsiConsole console, TrendsReading trending)
    {
        var document = new TrendingDocument(
            trending.Hashtags?.Select(HashtagDocument.Of).ToList(),
            trending.Posts?.Select(PostDocument.Of).ToList(),
            trending.Links?.Select(Of).ToList());

        JsonOutput.Write(console, document);
    }

    private static LinkDocument Of(TrendingLink link) => new(
        link.Url.OriginalString,
        link.Title,
        link.Description,
        link.Site,
        link.PeopleSharing);

    private sealed record TrendingDocument(
        [property: JsonPropertyName("hashtags")] IReadOnlyList<HashtagDocument>? Hashtags,
        [property: JsonPropertyName("posts")] IReadOnlyList<PostDocument>? Posts,
        [property: JsonPropertyName("links")] IReadOnlyList<LinkDocument>? Links);

    /// <param name="Url">What <c>timeline link</c> takes, and what a browser opens.</param>
    /// <param name="Description">Left out where the article said nothing of itself.</param>
    /// <param name="Site">Left out where the site did not name itself.</param>
    /// <param name="PeopleSharing">How many people shared it over the days the instance reported.</param>
    private sealed record LinkDocument(
        [property: JsonPropertyName("url")] string Url,
        [property: JsonPropertyName("title")] string Title,
        [property: JsonPropertyName("description")] string? Description,
        [property: JsonPropertyName("site")] string? Site,
        [property: JsonPropertyName("peopleSharing")] long PeopleSharing);
}
