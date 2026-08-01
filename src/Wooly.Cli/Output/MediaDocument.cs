using System.Text.Json.Serialization;
using Wooly.Core.Posts;

namespace Wooly.Cli.Output;

/// <summary>
///     How one of a post's attachments is spelled for another program to read. Written out by hand for the reason
///     <see cref="PostDocument" /> gives: these names are a contract with whatever is parsing them, not a projection of
///     whatever the domain record happens to be called this week.
/// </summary>
/// <param name="Kind">
///     What sort of thing it is, in this client's words — <c>image</c>, <c>animation</c>, <c>video</c>, <c>audio</c>,
///     or <c>unknown</c> for a kind newer than this client. Lower-cased so it reads like every other value here, and
///     kept rather than dropped so a script can filter on it.
/// </param>
/// <param name="Url">Where the file itself is.</param>
/// <param name="Preview">A smaller copy for showing in place, left out where the instance offered none.</param>
/// <param name="Description">
///     What its author said it shows, left out where they said nothing (<see cref="JsonOutput" />). Not filled in with
///     this client's <c>"a picture, undescribed"</c>: that phrase is for a person reading a line of output, and a
///     program asking whether an attachment was described deserves the actual answer.
/// </param>
internal sealed record MediaDocument(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("kind")] string Kind,
    [property: JsonPropertyName("url")] string Url,
    [property: JsonPropertyName("preview")] string? Preview,
    [property: JsonPropertyName("description")] string? Description)
{
    /// <summary>How <paramref name="media" /> is written down.</summary>
    public static MediaDocument Of(PostMedia media) => new(
        media.Id,
        MediaKindName.Written(media.Kind),
        media.Url,
        media.Preview,
        media.Description);
}
