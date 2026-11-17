using System.Text.Json.Serialization;
using Wooly.Core.Search;

namespace Wooly.Cli.Output;

/// <summary>
///     How a hashtag is spelled for another program to read — one record shared by every command that writes one,
///     <c>search</c> and <c>trends</c> alike, as <see cref="PostDocument" /> is for a post: a trending hashtag being a
///     hashtag, a second spelling is how the two would come to describe the same tag differently (#598's review).
/// </summary>
/// <param name="Name">Bare, with no leading <c>#</c>, which is how <c>timeline tag</c> takes one.</param>
/// <param name="RecentPosts">How many posts used it over the days the instance reported.</param>
/// <param name="RecentAccounts">How many accounts used it over the same days.</param>
/// <param name="Url">Where the instance shows it on the web, where it said.</param>
internal sealed record HashtagDocument(
    [property: JsonPropertyName("hashtag")] string Name,
    [property: JsonPropertyName("recentPosts")] long RecentPosts,
    [property: JsonPropertyName("recentAccounts")] long RecentAccounts,
    [property: JsonPropertyName("url")] string? Url)
{
    /// <summary><paramref name="hashtag" /> as every command writes one.</summary>
    public static HashtagDocument Of(Hashtag hashtag) => new(
        hashtag.Name,
        hashtag.RecentPosts,
        hashtag.RecentAccounts,
        hashtag.Url);
}
