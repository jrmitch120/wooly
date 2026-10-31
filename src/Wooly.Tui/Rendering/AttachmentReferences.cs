using Wooly.Core.Posts;
using Wooly.Tui.Theme;

namespace Wooly.Tui.Rendering;

/// <summary>
///     The <see cref="Reference" />s a post's attachments add to its walk, in attachment order and after every reference
///     the post's text carries: a <c>Video</c>, <c>Animation</c>, <c>Audio</c> or <c>Unknown</c> attachment's own
///     address (ADR-0017, #109), and since ADR-0034 a still picture's wherever it is drawn (#515).
/// </summary>
/// <remarks>
///     A still picture joins only where it is drawn, because where it is not it is linked the way the CLI already links
///     one, and its <c>⏵</c>, description and address are the whole of it. The rest are read off
///     <see cref="PostMedia.Opens" /> rather than off <see cref="PostMedia.IsDrawable" />, because the two stopped being
///     opposites in #110: a video's preview is drawn <em>and</em> its address is walked, which is the whole shape
///     ADR-0017 settled on — a still frame standing beside the word that opens the thing itself — and its box is never
///     walked to, the label being what opens it. Built the same way for <see cref="Screens.Screen.References" />, which
///     is what the walk answers to, and for <see cref="PostLines" />, which is what draws the brackets round whichever
///     one is picked — one formula rather than two, so the two can never come to disagree about which attachment a pick
///     landed on.
///     <para>
///         Each attachment has its own place past the end of the text whether or not it earns a reference, so that
///         where one is in the walk does not turn on whether the terminal draws the pictures beside it, and a picture's
///         reference names the attachment it was made of by its place alone (<see cref="Picture" />).
///     </para>
/// </remarks>
public static class AttachmentReferences
{
    /// <summary>
    ///     <paramref name="post" />'s own attachment references, or its boost's where it is one —
    ///     <see cref="Reference.At" /> starting past the end of the text so that none of them can collide with one
    ///     <c>BodyText</c> already found.
    /// </summary>
    /// <param name="post">The post, which may be a boost.</param>
    /// <param name="drawsPictures">
    ///     Whether its pictures are drawn rather than linked — <see cref="Drawing.Draws" />'s answer for what they are
    ///     laid out under — which is what settles whether they are walked at all.
    /// </param>
    public static IReadOnlyList<Reference> Of(Post post, bool drawsPictures)
    {
        var shown = post.Boosted ?? post;
        var references = new List<Reference>();

        for (var at = 0; at < shown.Media.Count; at++)
        {
            var attached = shown.Media[at];

            if (attached.Opens || (drawsPictures && attached.Kind is MediaKind.Image))
            {
                references.Add(new Reference(attached.Url, Role.Media, shown.Content.Length + at));
            }
        }

        return references;
    }

    /// <summary>
    ///     The still picture <paramref name="reference" /> was made of, where it is one of <paramref name="post" />'s —
    ///     or <see langword="null" /> where it is a text reference, an attachment's label, a link preview's, or of
    ///     nothing on this post. What tells a walked picture from an attachment whose address <c>⏎</c> hands the
    ///     browser, the two sharing <see cref="Role.Media" />.
    /// </summary>
    public static PostMedia? Picture(Post post, Reference reference)
    {
        var shown = post.Boosted ?? post;
        var at = reference.At - shown.Content.Length;

        return reference.Role is Role.Media
               && at >= 0
               && at < shown.Media.Count
               && shown.Media[at] is { Kind: MediaKind.Image } attached
               && attached.Url == reference.Text
            ? attached
            : null;
    }

    /// <summary>
    ///     Where a reference sorting after every one of these goes, which is the walk's next free place — what a link
    ///     preview's own takes (<see cref="LinkPreviewReference" />).
    /// </summary>
    /// <remarks>
    ///     Here rather than worked out again at the caller, because it is the same arithmetic <see cref="Of" /> places
    ///     by: whoever comes after these has to know both where the text stopped and how many places the attachments
    ///     take, and that second question is this class's to answer.
    /// </remarks>
    public static int Past(Post post)
    {
        var shown = post.Boosted ?? post;

        return shown.Content.Length + shown.Media.Count;
    }
}
