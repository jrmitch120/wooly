using Wooly.Tui.Theme;

namespace Wooly.Tui.Rendering;

/// <summary>
///     A run of text and what it is. The unit every screen is built out of: a screen produces spans, and the one view
///     that touches a terminal turns each role into an attribute and paints it.
/// </summary>
/// <remarks>
///     This is what makes the interesting half of rendering testable with no terminal in the room (ADR-0005,
///     ADR-0014). <em>This post is mine, so its delete affordance takes the destructive role</em> is a fact about a
///     span; how many pixels it ends up as is not.
/// </remarks>
/// <param name="Text">The characters to draw.</param>
/// <param name="Role">What they are, for the theme to answer.</param>
public readonly record struct Span(string Text, Role Role)
{
    /// <summary>
    ///     The characters to draw, in the form a terminal will draw them — which is what <see cref="Glyphs.Plain" />
    ///     settles.
    /// </summary>
    /// <remarks>
    ///     Done here rather than at the sites that hand somebody else's words to a screen, for the reason a role is
    ///     answered here (ADR-0014): a span is the only thing that is ever painted, so a mark a layout cannot measure
    ///     has nowhere left to arrive from. What reaches a terminal is what was measured, by construction.
    /// </remarks>
    public string Text { get; init; } = Glyphs.Plain(Text);

    /// <summary>The width this takes on screen, which is the width every layout is measured in.</summary>
    /// <remarks>
    ///     Measured off <see cref="Text" /> after that, so a layout weighing a name against the room beside it weighs
    ///     what will be drawn rather than what arrived (#206) — and measured in the columns a terminal draws in rather
    ///     than in characters, which are not the same number for anything but Latin text (#207).
    /// </remarks>
    public int Width => Glyphs.Columns(Text);

    /// <summary>
    ///     Which thing this run stands for, where a row is made of several that a pointer can tell apart — a crumb of the
    ///     breadcrumb, carrying its place in the stack (#308) — or <see langword="null" /> for a run that stands for none.
    /// </summary>
    /// <remarks>
    ///     <see cref="Line.Item" /> for one row's columns rather than a screen's rows: what a click is answered from, off
    ///     what is drawn under the pointer rather than off a second reckoning of where each run fell.
    /// </remarks>
    public int? Item { get; init; }

    /// <summary>
    ///     Which reference this run is drawn for — a hashtag, a mention, an address, an attachment's own address or a
    ///     link preview's — or <see langword="null" /> for a run that is part of none. Every run of one carries it,
    ///     brackets included, and so does every row of one the wrap cut across several (#475).
    /// </summary>
    /// <remarks>
    ///     Beside <see cref="Item" /> rather than folded into it, because the two are asked different questions and a
    ///     row can want both: a screen already reads its own meaning into a run's item, a face of the notification
    ///     strip among them. The reference itself rather than its place among the walk's, so that what was drawn is
    ///     matched against what may be walked to — a reference drawn and never walked, a mention in a bio, matches
    ///     nothing — rather than trusted to have been numbered the same way twice.
    /// </remarks>
    public Reference? Reference { get; init; }

    /// <summary>
    ///     The custom emoji this run is written for, or <see langword="null" /> for a run that is none: its shortcode
    ///     as written until <see cref="CustomEmojiBox.Boxed" /> sets it as its box, where the <b>Raster</b> draws, and
    ///     its box after (ADR-0035).
    /// </summary>
    /// <remarks>
    ///     Never a <see cref="Reference" /> as well: a custom emoji is not walked to, clicked open, or shown on the
    ///     Picture screen.
    /// </remarks>
    public Core.CustomEmoji? Emoji { get; init; }
}
