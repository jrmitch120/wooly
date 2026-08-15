using Wooly.Tui.Theme;

namespace Wooly.Tui.Rendering;

/// <summary>
///     A hashtag, a mention, or an address inside a post's text — or a <c>Video</c>, <c>Animation</c>, <c>Audio</c> or
///     <c>Unknown</c> attachment's own address, once it is not drawn as a picture in its own right — one of the things
///     that point somewhere else, which <c>←</c> and <c>→</c> walk and <c>⏎</c> opens (CONTEXT.md, #83, ADR-0017).
/// </summary>
/// <remarks>
///     The first three are found once on the whole post's flattened text rather than a row at a time, which is what
///     gives them an order and a place in it: a row on its own cannot tell whether it is holding the second half of an
///     address the wrap cut in two, and before this that half matched nothing and was drawn as prose. An attachment
///     reference carries no place in that text at all — it is walked after every one of them, in attachment order,
///     drawn on <c>PostLines</c>' own path rather than <c>BodyText</c>'s (#109).
///     <para>
///         Which of the four it is, is said by the role it draws in. That is one vocabulary rather than two: the
///         theme already tells a tag from a mention from an address, and a second enum saying the same thing would be
///         a second place to add a kind to.
///     </para>
/// </remarks>
/// <param name="Text">
///     The reference as it was written, without whatever the sentence around it left behind — or, for an attachment,
///     its own address, which is what <c>⏎</c> hands the browser.
/// </param>
/// <param name="Role">Which of the four it is — <c>Hashtag</c>, <c>Mention</c>, <c>Link</c>, or an attachment's own
///     <c>Media</c>.</param>
/// <param name="At">
///     Where it starts in the post's text — or, for an attachment reference, past the end of it, so that every
///     attachment reference sorts after every one the text carries and no two collide (<see cref="Rendering.AttachmentReferences" />).
/// </param>
public readonly record struct Reference(string Text, Role Role, int At)
{
    /// <summary>Where it stops in that text, one past its last character.</summary>
    public int End => At + Text.Length;
}
