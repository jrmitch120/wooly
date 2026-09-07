using Wooly.Core.Posts;

namespace Wooly.Tui.Rendering;

/// <summary>
///     What a post carries that points somewhere else: the hashtags, handles and addresses written in its text, in the
///     order they were written, then its attachments' own addresses and its link preview's (#83, ADR-0017, ADR-0018).
///     What every screen with a post on it walks, and the default answer to <see cref="IReferring" />.
/// </summary>
/// <remarks>
///     Nothing that is not on screen, on either half. The text ones go while the post's text is behind a content
///     warning, because the brackets a picked reference is drawn in would be behind it too and a pick there is one
///     nobody can see (<c>docs/tui-shell.md</c>). The attachment ones go as soon as the post is warned at all: since
///     #113 the attachments are behind the warning with the text, so <c>←</c>/<c>→</c> would be walking to a label
///     nobody can see and <c>⏎</c> opening a video the reader never asked for.
///     <para>
///         Both halves asked of <see cref="OnShow" /> rather than worked out here, because they are the same two
///         questions <see cref="Screens.PostLines" /> puts about the same post: what is walked is what was drawn, and
///         the two answering separately is what let a walk reach inside something the reader was never shown (#145).
///     </para>
/// </remarks>
/// <param name="Post">The post being read, which may be a boost — <see cref="OnShow" /> unwraps it.</param>
/// <param name="Revealed">Whether this reader has asked past whatever warning it carries.</param>
public readonly record struct PostReferences(Post Post, bool Revealed) : IReferring
{
    /// <inheritdoc />
    public IReadOnlyList<Reference> References
    {
        get
        {
            var show = OnShow.Of(Post, Revealed);

            return
            [
                .. show.Words ? BodyText.References(show.Shown.Content) : [],
                .. show.Media ? BeyondTheText(show.Shown) : [],
            ];
        }
    }

    /// <summary>
    ///     The references a post carries beyond its own text: its attachments' addresses in the order they were
    ///     attached, and then its link preview's (ADR-0018).
    /// </summary>
    /// <remarks>
    ///     Shown and hidden together, which is why they are one question here: the link preview stands behind a post's
    ///     warning on exactly the terms its attachments do since #113, and asking twice would be two places for that to
    ///     be answered differently.
    /// </remarks>
    private static IEnumerable<Reference> BeyondTheText(Post post)
    {
        foreach (var attached in AttachmentReferences.Of(post))
        {
            yield return attached;
        }

        if (LinkPreviewReference.Of(post) is { } link)
        {
            yield return link;
        }
    }
}
