using Wooly.Core.Posts;
using Wooly.Tui.Theme;

namespace Wooly.Tui.Rendering;

/// <summary>
///     The one <see cref="Reference" /> a post's link preview adds to its walk: the address the instance made the
///     preview of, walked last of all and opened with <c>⏎</c> (ADR-0018, #116).
/// </summary>
/// <remarks>
///     Its address will usually be one a <c>Link</c> reference inside the post's own text already reaches, and it is
///     walked anyway: a title and a description are a pointer to the article rather than the article, and a reader who
///     wants it needs something to press <c>⏎</c> on without reading a long post to find the matching link. What the
///     page says its author is called is never one of these — that would be a third thing on the post reaching for the
///     same handful of places, which is where consistency with attachments stopped being the stronger argument
///     (ADR-0018).
///     <para>
///         Built the same way for <see cref="Screens.Screen.References" />, which is what the walk answers to, and for
///         <see cref="Screens.PostLines" />, which draws the brackets round it while it is picked — one formula rather
///         than two, the bargain <see cref="AttachmentReferences" /> already struck.
///     </para>
/// </remarks>
public static class LinkPreviewReference
{
    /// <summary>
    ///     <paramref name="post" />'s link preview reference, or its boost's where it is one — or
    ///     <see langword="null" /> where the instance previewed no link in it.
    /// </summary>
    /// <remarks>
    ///     <see cref="Reference.At" /> is past the end of the text <em>and</em> past every attachment reference, which
    ///     is what sorts it after all of them. Counted off <see cref="AttachmentReferences" /> rather than off
    ///     <c>Media</c> directly, so that which attachments earn a reference is settled in the one place: a rule
    ///     written twice is a rule that comes to disagree with itself.
    /// </remarks>
    public static Reference? Of(Post post)
    {
        var shown = post.Boosted ?? post;

        return shown.LinkPreview is { } link
            ? new Reference(link.Url, Role.Media, shown.Content.Length + AttachmentReferences.Of(post).Count)
            : null;
    }
}
