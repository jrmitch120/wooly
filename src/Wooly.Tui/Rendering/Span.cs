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
    /// <summary>The width this takes on screen, which is the width every layout is measured in.</summary>
    public int Width => Text.Length;
}
