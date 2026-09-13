using Wooly.Tui.Rendering;
using Wooly.Tui.Theme;

namespace Wooly.Tests.Tui;

/// <summary>
///     What a terminal will draw, as against what an instance sent: the marks the two count differently (#206), and
///     how wide what is left comes out in the columns a terminal actually has (#207).
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
    // Every one of them, because each narrow character wearing a selector costs its row another column: a name with
    // two would have taken the whole of an age with it rather than the unit off the end of one.
    [InlineData("a➡️b❤️c⭐️d", "a➡b❤c⭐d")]
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
    ///     How wide a piece of text is, in the columns a terminal draws it in — which is not how many characters are
    ///     in it for anything but Latin text, and the whole of what #207 is about. A CJK character takes two columns
    ///     and one character; a combining mark takes none and one; a ZWJ sequence takes one emoji's two and however
    ///     many characters it was written with.
    /// </summary>
    [Theory]
    [InlineData("Maria Ochoa", 11)]
    [InlineData("", 0)]
    [InlineData("ドット絵アカウント", 18)]
    [InlineData("An Amazing Wizard\u27A1\uFE0FKICKSTARTER", 29)]
    [InlineData("Family \U0001F468\u200D\U0001F469\u200D\U0001F467 Account", 17)]
    [InlineData("\U0001F389", 2)]
    [InlineData("e\u0301", 1)]
    public void Columns_CountsTheColumnsATerminalDrawsRatherThanTheCharacters(string text, int columns) =>
        Assert.Equal(columns, Glyphs.Columns(text));

    /// <summary>
    ///     Cutting stops at the last character that fits whole. A row given an odd number of columns and written in a
    ///     script with none of them cannot fill the last one, and a half-drawn character is what filling it would mean.
    /// </summary>
    [Theory]
    [InlineData("ドット絵", 8, "ドット絵")]
    [InlineData("ドット絵", 5, "ドッ")]
    [InlineData("ドット絵", 4, "ドッ")]
    [InlineData("ドット絵", 1, "")]
    [InlineData("Maria Ochoa", 5, "Maria")]
    [InlineData("Maria Ochoa", 0, "")]
    public void Cut_KeepsWhatFitsInTheColumnsAndNoHalfOfAWideCharacter(string text, int columns, string kept)
    {
        Assert.Equal(kept, Glyphs.Cut(text, columns));
        Assert.True(Glyphs.Columns(Glyphs.Cut(text, columns)) <= columns);
    }

    /// <summary>
    ///     A grapheme cluster is one thing to a reader and is cut as one: between a base and its combining mark, or
    ///     inside a ZWJ sequence, is a cut that paints something nobody wrote.
    /// </summary>
    [Theory]
    [InlineData("e\u0301clair", 1, "e\u0301")]
    [InlineData("e\u0301clair", 0, "")]
    [InlineData("\U0001F468\u200D\U0001F469\u200D\U0001F467!", 2, "\U0001F468\u200D\U0001F469\u200D\U0001F467")]
    [InlineData("\U0001F468\u200D\U0001F469\u200D\U0001F467!", 1, "")]
    public void Cut_NeverCutsInsideAGraphemeCluster(string text, int columns, string kept) =>
        Assert.Equal(kept, Glyphs.Cut(text, columns));

    /// <summary>
    ///     Padding fills the columns that are left rather than the characters, which is the same mistake as measuring
    ///     by them: a rail entry padded by its characters is a rail entry drawn past the rail.
    /// </summary>
    [Theory]
    [InlineData("ドット", 8, "ドット  ")]
    [InlineData("ドット", 6, "ドット")]
    [InlineData("ドット", 3, "ドット")]
    [InlineData("ab", 4, "ab  ")]
    public void Padded_FillsTheColumnsThatAreLeft(string text, int columns, string padded)
    {
        Assert.Equal(padded, Glyphs.Padded(text, columns));
        Assert.Equal(Math.Max(columns, Glyphs.Columns(text)), Glyphs.Columns(Glyphs.Padded(text, columns)));
    }

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

    /// <summary>
    ///     And measured in columns there too, so that every layout in the shell — which weighs spans rather than
    ///     strings — is weighing what the terminal will fill (#207).
    /// </summary>
    [Fact]
    public void Span_MeasuresItselfInColumns()
    {
        Assert.Equal(8, new Span("ドット絵", Role.BylineName).Width);
        Assert.Equal(11, Line.Of([new Span("ドット絵", Role.BylineName), new Span("abc", Role.Body)]).Width);
    }
}
