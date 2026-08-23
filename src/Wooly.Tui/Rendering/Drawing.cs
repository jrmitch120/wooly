using Wooly.Tui.Media;

namespace Wooly.Tui.Rendering;

/// <summary>
///     What a screen needs to know that is not about the screen: how much room there is, what the moment is, what this
///     terminal can paint, and what the reader asked for. Everything <see cref="Screens.Screen.Lines" /> is given, as
///     one thing.
/// </summary>
/// <remarks>
///     One record rather than an argument apiece, because none of the four is the screen's own business and every one
///     of them arrived a ticket at a time: <c>pictures</c> with ADR-0016, <c>hideDrawnCaption</c> with #71, and each
///     cost a signature edit at eleven overrides plus a threading change through <see cref="Screens.PostList" /> to
///     carry one new fact (#148). What comes next is a field here.
///     <para>
///         The two beyond width and the moment default, so a caller says only what it cares about: a screen laid out
///         with no terminal in the room is <c>new Drawing(width, now)</c>, and that reads as every attachment being
///         linked rather than drawn.
///     </para>
///     <para>
///         Distinct from a <see cref="Media.Drawn" />, which is one picture the TUI paints in place. This is the
///         conditions a screen is drawn under; that is a thing being drawn.
///     </para>
/// </remarks>
/// <param name="Width">How wide the content region is — 61 at an 80-column terminal.</param>
/// <param name="Now">What to measure timestamps against.</param>
/// <param name="Pictures">
///     What this terminal can draw and which attachments' pixels have arrived, or <see langword="null" /> for a screen
///     being laid out with no terminal in the room — which is every test, and which reads as every attachment being
///     linked rather than drawn.
///     <para>
///         A screen needs this while it is working out its rows rather than while they are being painted, because it
///         changes what the rows are: a picture's own proportions settle how many rows its box takes, and an
///         attachment on a terminal that draws nothing becomes a link and a description instead (ADR-0016).
///     </para>
/// </param>
/// <param name="HideDrawnCaption">
///     The reader's <c>hide_drawn_caption</c> preference: whether a picture's caption hides once it is actually drawn
///     (#71). Ignored by a screen with no posts on it.
/// </param>
public sealed record Drawing(
    int Width,
    DateTimeOffset Now,
    IPictures? Pictures = null,
    bool HideDrawnCaption = false)
{
    /// <summary>The same drawing in less room, which is what a gutter or an indent leaves the thing inside it.</summary>
    /// <remarks>
    ///     The one thing about a drawing that changes on the way down: a row is stamped in a column the post inside it
    ///     never gets, and a message under a conversation is indented past that again. Said as a method rather than
    ///     left to <c>with</c> at each site so that narrowing is one word wherever it happens, and so that nothing
    ///     else on the record looks equally adjustable.
    /// </remarks>
    /// <param name="width">The room left.</param>
    public Drawing In(int width) => this with { Width = width };
}
