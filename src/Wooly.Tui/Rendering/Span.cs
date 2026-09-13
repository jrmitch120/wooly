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
}
