using Wooly.Tui.Rendering;
using Wooly.Tui.Theme;

namespace Wooly.Tests.Tui;

/// <summary>
///     What a terminal will draw, as against what an instance sent — the one difference between the two that costs a
///     column, and the place it is settled so that no screen has to remember it (#206).
/// </summary>
public class GlyphsTests
{
    /// <summary>
    ///     The variation selector asking for the emoji form of a character is dropped: Terminal.Gui gives the pair a
    ///     single cell and a terminal paints it two columns wide, so a row measured by the characters is a row drawn
    ///     wider than the room it was laid out in.
    /// </summary>
    [Theory]
    [InlineData("An Amazing Wizard➡️KICKSTARTER", "An Amazing Wizard➡KICKSTARTER")]
    [InlineData("❤️", "❤")]
    [InlineData("❤︎", "❤")]
    [InlineData("️", "")]
    public void Plain_DropsAVariationSelector(string sent, string drawn) => Assert.Equal(drawn, Glyphs.Plain(sent));

    /// <summary>
    ///     Everything else is left exactly as it was written, the string itself rather than a copy of it — which is
    ///     nearly every string this client draws, on a path walked afresh for every row of every frame.
    /// </summary>
    [Theory]
    [InlineData("Maria Ochoa")]
    [InlineData("ドット絵")]
    [InlineData("Party \U0001F389 Time")]
    [InlineData("")]
    public void Plain_LeavesTextCarryingNoSelectorAlone(string sent) => Assert.Same(sent, Glyphs.Plain(sent));

    /// <summary>
    ///     Said by the span rather than by the screens, so a mark a layout cannot measure has nowhere to arrive
    ///     from: what reaches a terminal is what was measured.
    /// </summary>
    [Fact]
    public void Span_DrawsAndMeasuresThePlainForm()
    {
        var span = new Span("➡️", Role.BylineName);

        Assert.Equal("➡", span.Text);
        Assert.Equal(1, span.Width);
        Assert.Equal(1, Line.Of([span]).Width);
    }
}
