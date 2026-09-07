namespace Wooly.Tui.Rendering;

/// <summary>
///     Something on a screen that points somewhere else: the thing <c>←</c> and <c>→</c> walk the references of, and
///     <c>⏎</c> opens one of (<c>docs/tui-shell.md</c>).
/// </summary>
/// <remarks>
///     A post was the only answer to this until #164, and <see cref="Screens.Screen" /> derived the walk from a
///     <c>Post?</c> directly. The account screen's header block is the first reference source in this client that is
///     not a post and will not be the last (ADR-0019), so the screen asks the picked thing what it carries and
///     <see cref="PostReferences" /> is what a post answers — a generalisation rather than a second path, because two
///     paths is two places for what may be walked to come apart.
///     <para>
///         What is <em>walked</em> rather than what is drawn: a reference drawn and deliberately not openable is left
///         off — a mention in a bio, which has nothing to resolve it against (#179), the way an <c>Image</c>
///         attachment and a link preview's author name already are.
///     </para>
/// </remarks>
public interface IReferring
{
    /// <summary>
    ///     The references it carries, in the order they are walked — which is the order they are drawn, and the order
    ///     an index into them means anything in.
    /// </summary>
    IReadOnlyList<Reference> References { get; }
}
