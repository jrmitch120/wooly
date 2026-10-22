using Terminal.Gui.Input;
using Wooly.Tests.Fakes;
using Wooly.Tui.Screens;
using Wooly.Tui.Theme;

namespace Wooly.Tests.Tui;

/// <summary>
///     The mouse on a narrowed shell (#416): inside it, every gesture lands where it would at full width; in the margins
///     either side, every button does nothing and the wheel scrolls the content panel, as it does over the panel itself.
///     Sent at a cell of a headless terminal 200 columns wide, with the shell narrowed a step to 180 columns from the
///     eleventh, and read back as the existing click, wheel and rail-click tests read them.
/// </summary>
public class NarrowedShellMouseTests
{
    /// <summary>The shell's first column: the left margin's 10.</summary>
    private const int Left = 10;

    /// <summary>A column in the left margin.</summary>
    private const int LeftMargin = Left / 2;

    /// <summary>A column in the right margin, after the shell's 180 columns.</summary>
    private const int RightMargin = Left + 180 + 5;

    /// <summary>A row inside both panels, below their top edges.</summary>
    private const int Inside = 3;

    /// <summary>
    ///     A notch over either margin moves the page as a notch over the content panel does, a row at a time, either way.
    /// </summary>
    [Theory]
    [InlineData(LeftMargin, true)]
    [InlineData(LeftMargin, false)]
    [InlineData(RightMargin, true)]
    [InlineData(RightMargin, false)]
    public async Task AWheelOverAMarginScrollsTheContent(int column, bool down)
    {
        using var drawn = await Narrowed(Four());

        drawn.Press(Key.CursorDown);
        drawn.Press(Key.CursorDown);

        var from = drawn.Content.Top;

        drawn.Wheel(column, Inside, down);

        Assert.Equal(from + (down ? 1 : -1), drawn.Content.Top);
    }

    /// <summary>The shell on a 200-column terminal, narrowed a step to 180 columns from the eleventh.</summary>
    private static async Task<DrawnShell> Narrowed(AShell? built = null, int height = 30)
    {
        var drawn = await DrawnShell.Of(200, height, Themes.Plain, built);

        drawn.Press(new Key('-'));

        Assert.Equal(Left, drawn.Rows()[0].IndexOf('╭', StringComparison.Ordinal));

        return drawn;
    }

    /// <summary>Four posts on Home, by people other than the reader.</summary>
    private static AShell Four() => new()
    {
        Timelines = FakeTimelineReader.Holding(
            APost.With(id: "110"),
            APost.With(id: "220"),
            APost.With(id: "330"),
            APost.With(id: "440")),
    };
}
