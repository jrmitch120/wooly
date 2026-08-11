using Wooly.Tui.Theme;

namespace Wooly.Tui.Rendering;

/// <summary>
///     A hashtag, a mention, or an address inside a post's text — one of the things that point somewhere else, which
///     <c>←</c> and <c>→</c> walk and <c>⏎</c> opens (CONTEXT.md, #83).
/// </summary>
/// <remarks>
///     Found once on the whole post's flattened text rather than a row at a time, which is what gives the references
///     in a post an order and a place in it: a row on its own cannot tell whether it is holding the second half of an
///     address the wrap cut in two, and before this that half matched nothing and was drawn as prose.
///     <para>
///         Which of the three it is, is said by the role it draws in. That is one vocabulary rather than two: the
///         theme already tells a tag from a mention from an address, and a second enum saying the same thing would be
///         a second place to add the fourth kind to.
///     </para>
/// </remarks>
/// <param name="Text">The reference as it was written, without whatever the sentence around it left behind.</param>
/// <param name="Role">Which of the three it is — <c>Hashtag</c>, <c>Mention</c> or <c>Link</c>.</param>
/// <param name="At">Where it starts in the post's text.</param>
public readonly record struct Reference(string Text, Role Role, int At)
{
    /// <summary>Where it stops in that text, one past its last character.</summary>
    public int End => At + Text.Length;
}
