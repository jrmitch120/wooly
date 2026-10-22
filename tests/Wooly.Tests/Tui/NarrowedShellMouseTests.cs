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

    /// <summary>A column inside the narrowed shell's rail, clear of its left edge and its mark column.</summary>
    private const int OverRail = Left + RailLines.Width / 2;

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

    /// <summary>
    ///     A click, a double click, a right click or a drag in either margin does nothing: an open confirmation, which a
    ///     click anywhere in the shell declines and a right click anywhere answers, is still open, and nothing drawn has
    ///     changed.
    /// </summary>
    [Theory]
    [InlineData("click", LeftMargin)]
    [InlineData("click", RightMargin)]
    [InlineData("double click", LeftMargin)]
    [InlineData("double click", RightMargin)]
    [InlineData("right click", LeftMargin)]
    [InlineData("right click", RightMargin)]
    [InlineData("drag", LeftMargin)]
    [InlineData("drag", RightMargin)]
    public async Task AButtonInAMarginDoesNothing(string gesture, int column)
    {
        var answers = Enumerable.Range(1, 3).Select(at => APost.AnAnswer($"Answer {at}", at)).ToList();

        using var drawn = await Narrowed(
            new AShell
            {
                Timelines = FakeTimelineReader.Holding(
                    APost.With(id: "110", poll: APost.APoll(options: answers)),
                    APost.With(id: "220", content: "Two sheep")),
            });

        drawn.Press(new Key('2'));
        drawn.Press(Key.V);

        var asking = drawn.Shell.Asking;
        var rows = drawn.Rows();

        Assert.NotNull(asking);

        switch (gesture)
        {
            case "click":
                drawn.Click(column, Inside);
                break;

            case "double click":
                drawn.DoubleClick(column, Inside);
                break;

            case "right click":
                drawn.RightClick(column, Inside);
                break;

            case "drag":
                drawn.Point(column, Inside, MouseFlags.LeftButtonPressed);
                drawn.Point(column, Inside + 1, MouseFlags.LeftButtonPressed | MouseFlags.PositionReport);
                drawn.Point(column, Inside + 1, MouseFlags.LeftButtonReleased);
                drawn.Point(column, Inside + 1, MouseFlags.LeftButtonClicked);
                break;
        }

        drawn.Settle();

        Assert.Same(asking, drawn.Shell.Asking);
        Assert.Equal(rows, drawn.Rows());
        Assert.Empty(drawn.Built.Engagement.Votes);
    }

    /// <summary>A click on a destination on the narrowed shell's rail arrives at the one drawn under the pointer.</summary>
    [Theory]
    [InlineData("Notifications")]
    [InlineData("Local")]
    public async Task AClickOnTheRailArrivesAtTheEntryUnderThePointer(string label)
    {
        using var drawn = await Narrowed();

        drawn.Click(OverRail, RailRowOf(drawn, label));
        drawn.Settle();

        Assert.Equal(label, drawn.Shell.Rail.Showing.Label);
        Assert.Equal(drawn.Shell.Rail.Current, drawn.Shell.Rail.Cursor);
    }

    /// <summary>The shell on a 200-column terminal, narrowed a step to 180 columns from the eleventh.</summary>
    private static async Task<DrawnShell> Narrowed(AShell? built = null, int height = 30)
    {
        var drawn = await DrawnShell.Of(200, height, Themes.Plain, built);

        drawn.Press(new Key('-'));

        Assert.Equal(Left, drawn.Rows()[0].IndexOf('╭', StringComparison.Ordinal));

        return drawn;
    }

    /// <summary>The terminal row the narrowed shell's rail draws <paramref name="text" /> on.</summary>
    private static int RailRowOf(DrawnShell drawn, string text)
    {
        var rows = drawn.Rail();
        var row = Array.FindIndex(rows, line => line.Contains(text, StringComparison.Ordinal));

        Assert.True(row >= 0, $"Nothing on the rail reads {text}:\n{string.Join('\n', rows)}");

        return row;
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
