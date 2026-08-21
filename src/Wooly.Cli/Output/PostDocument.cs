using System.Text.Json.Serialization;
using Wooly.Core.Posts;

namespace Wooly.Cli.Output;

/// <summary>
///     How a post is spelled for another program to read. The field names below are a contract with whatever is parsing
///     them, which is why they are written out here rather than derived from <see cref="Post" /> — a rename in the domain
///     must not silently rename somebody's <c>jq</c> filter. They are this project's vocabulary, not the API's:
///     <c>boosts</c>, <c>favorites</c>.
///     <para>
///         One record, shared by every command that writes a post — a timeline, a post just published, a post just
///         edited. A second spelling is how <c>timeline home --json</c> and <c>post create --json</c> would come to
///         describe the same post differently.
///     </para>
/// </summary>
/// <param name="Visibility">Who can see it, in the same words <c>--visibility</c> takes.</param>
/// <param name="Sensitive">
///     Whether the instance flagged what the post carries as something to ask for before seeing (#122). Written on
///     every post rather than only the flagged ones, unlike the keys this document leaves out where they do not apply:
///     <see langword="false" /> is an answer, and a script filtering on it should not have to tell a post nobody
///     flagged from a client too old to say. Beside <c>contentWarning</c>, which is the other half of the same promise
///     and the half a post commonly does not carry.
/// </param>
/// <param name="Media">
///     What is attached, in the order the author attached it, and empty where nothing is. Written out even though the
///     human output links it too, because the whole point of <c>--json</c> is that a script does not have to read the
///     human output to find out what a post carries.
/// </param>
/// <param name="LinkPreview">
///     What the instance made of a link the text already carries, or absent on a post it made nothing of — an object
///     rather than a list, because a post has at most one (ADR-0018), and so absent rather than empty where there is
///     none. It follows <see cref="Media" /> here as it does in the human output.
/// </param>
internal sealed record PostDocument(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("account")] string Account,
    [property: JsonPropertyName("author")] string Author,
    [property: JsonPropertyName("postedAt")] DateTimeOffset PostedAt,
    [property: JsonPropertyName("contentWarning")] string? ContentWarning,
    [property: JsonPropertyName("sensitive")] bool Sensitive,
    [property: JsonPropertyName("content")] string Content,
    [property: JsonPropertyName("visibility")] string Visibility,
    [property: JsonPropertyName("boosts")] long Boosts,
    [property: JsonPropertyName("favorites")] long Favorites,
    [property: JsonPropertyName("replies")] long Replies,
    [property: JsonPropertyName("url")] string? Url,
    [property: JsonPropertyName("media")] IReadOnlyList<MediaDocument> Media,
    [property: JsonPropertyName("linkPreview")] LinkPreviewDocument? LinkPreview,
    [property: JsonPropertyName("boosted")] PostDocument? Boosted)
{
    /// <summary>How <paramref name="post" /> is written down.</summary>
    public static PostDocument Of(Post post) => new(
        post.Id,
        post.Account,
        post.Author,
        post.PostedAt,
        post.ContentWarning,
        post.Sensitive,
        post.Content,
        PostVisibilityName.Of(post.Visibility),
        post.Boosts,
        post.Favorites,
        post.Replies,
        post.Url,
        [.. post.Media.Select(MediaDocument.Of)],
        LinkPreviewDocument.Of(post.LinkPreview),
        post.Boosted is null ? null : Of(post.Boosted));
}
