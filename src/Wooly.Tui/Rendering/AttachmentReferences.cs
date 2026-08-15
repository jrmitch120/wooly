using Wooly.Core.Posts;
using Wooly.Tui.Theme;

namespace Wooly.Tui.Rendering;

/// <summary>
///     The <see cref="Reference" />s a post's attachments add to its walk — a <c>Video</c>, <c>Animation</c>,
///     <c>Audio</c> or <c>Unknown</c> attachment's own address, in attachment order, after every reference the post's
///     text carries (ADR-0017, #109).
/// </summary>
/// <remarks>
///     <c>Image</c> never appears here: a still picture is drawn, or linked the way the CLI already links one, and
///     neither is a thing <c>←</c>/<c>→</c> walks. Built the same way for <see cref="Screens.Screen.References" />,
///     which is what the walk answers to, and for <see cref="PostLines" />, which is what draws the bracket around
///     whichever one is picked — one formula rather than two, so the two can never come to disagree about which
///     attachment a pick landed on.
/// </remarks>
public static class AttachmentReferences
{
    /// <summary>
    ///     <paramref name="post" />'s own attachment references, or its boost's where it is one — <see cref="Reference.At" />
    ///     starting past the end of the text so that none of them can collide with one <c>BodyText</c> already found.
    /// </summary>
    public static IReadOnlyList<Reference> Of(Post post)
    {
        var shown = post.Boosted ?? post;
        var at = shown.Content.Length;
        var references = new List<Reference>();

        foreach (var attached in shown.Media)
        {
            if (attached.Kind == MediaKind.Image)
            {
                continue;
            }

            references.Add(new Reference(attached.Url, Role.Media, at));
            at++;
        }

        return references;
    }
}
