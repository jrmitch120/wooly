using System.Text.Json.Serialization;
using Wooly.Core.Posts;

namespace Wooly.Cli.Output;

/// <summary>
///     What an instance made of a link inside a post's text, spelled for another program to read. Written out by hand
///     for the reason <see cref="PostDocument" /> gives: these names are a contract with whatever is parsing them, not
///     a projection of whatever the domain record happens to be called this week.
/// </summary>
/// <param name="Url">Where the preview points — the address a script following the link would use.</param>
/// <param name="Title">What the page calls itself, left out where the instance made nothing of it.</param>
/// <param name="Provider">
///     The site it is on, as the site names itself. Written as <c>provider</c> rather than the domain's
///     <c>providerName</c>: a field on an object about one link needs no suffix saying it is the name of one.
/// </param>
/// <param name="Description">What the page says it is about, left out where the instance offered none.</param>
/// <param name="Image">
///     The picture the instance chose for the link, left out where it chose none. Kept though the human output never
///     mentions it, the same way <see cref="MediaDocument.Preview" /> is on a surface that draws nothing: the point of
///     <c>--json</c> is that a script does not have to read the human output to find out what a post carries.
/// </param>
/// <param name="Author">Who the page says wrote it, left out where it says nothing. Plain text, never an address.</param>
internal sealed record LinkPreviewDocument(
    [property: JsonPropertyName("url")] string Url,
    [property: JsonPropertyName("title")] string? Title,
    [property: JsonPropertyName("provider")] string? Provider,
    [property: JsonPropertyName("description")] string? Description,
    [property: JsonPropertyName("image")] string? Image,
    [property: JsonPropertyName("author")] string? Author)
{
    /// <summary>
    ///     How <paramref name="link" /> is written down, or <see langword="null" /> on a post the instance made
    ///     nothing of — which <see cref="JsonOutput" /> leaves out altogether rather than writing as a null.
    /// </summary>
    public static LinkPreviewDocument? Of(LinkPreview? link) => link is null
        ? null
        : new LinkPreviewDocument(
            link.Url,
            link.Title,
            link.ProviderName,
            link.Description,
            link.Image,
            link.Author);
}
