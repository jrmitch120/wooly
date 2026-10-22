using Terminal.Gui.Input;
using Wooly.Tests.Fakes;
using Wooly.Tui.Screens;
using Wooly.Tui.Theme;
using Wooly.Tui.Views;
using static Wooly.Tests.Tui.ContentClicks;

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

    /// <summary>A column inside the narrowed shell's content panel, clear of its left edge and the pick's gutter.</summary>
    private const int OverContent = Left + ContentClicks.OverContent;

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
    ///     And as the running TUI reads a terminal's reports: the notch over a margin is drawn on the loop's next pass, the
    ///     page moved on the terminal as it is by a notch over the content panel.
    /// </summary>
    [Fact]
    public async Task AWheelOverAMarginIsDrawnAsTheLoopDraws()
    {
        using var overMargin = await Narrowed(Four());
        using var overContent = await Narrowed(Four());

        var before = overMargin.Rows();

        overMargin.Reports(Sgr.Wheel(LeftMargin, Inside));
        overContent.Reports(Sgr.Wheel(OverContent, Inside));

        Assert.NotEqual(before, overMargin.Rows());
        Assert.Equal(overContent.Rows(), overMargin.Rows());
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

    /// <summary>A click on a row of a post in the narrowed shell's content panel picks that post.</summary>
    [Fact]
    public async Task AClickOnAPostPicksIt()
    {
        using var drawn = await Narrowed(Two());

        drawn.Click(OverContent, RowOf(drawn, "Two sheep"));

        Assert.Equal("220", drawn.Shell.Screen.Picked?.Id);
    }

    /// <summary>A double click on a post opens it, as a click on it and then <c>⏎</c> do in the narrowed shell.</summary>
    [Fact]
    public async Task ADoubleClickOnAPostIsAClickAndThenEnter()
    {
        using var keyed = await Narrowed(Two());
        using var doubled = await Narrowed(Two());

        keyed.Press(Key.K);
        keyed.Press(Key.Enter);
        keyed.Settle();

        doubled.DoubleClick(OverContent, RowOf(doubled, "Two sheep"));
        doubled.Settle();

        Assert.IsType<PostScreen>(doubled.Shell.Screen);
        Assert.Equal(keyed.Rows(), doubled.Rows());
    }

    /// <summary>A right click inside the narrowed shell is <c>esc</c>: a post opened off Home goes back to Home.</summary>
    [Theory]
    [InlineData(OverRail)]
    [InlineData(OverContent)]
    public async Task ARightClickInsideTheShellIsEsc(int column)
    {
        using var keyed = await Narrowed(Two());
        using var clicked = await Narrowed(Two());

        foreach (var drawn in new[] { keyed, clicked })
        {
            drawn.Press(Key.Enter);
            drawn.Settle();

            Assert.IsType<PostScreen>(drawn.Shell.Screen);
        }

        keyed.Press(Key.Esc);
        keyed.Settle();

        clicked.RightClick(column, Inside);
        clicked.Settle();

        Assert.IsType<FeedScreen>(clicked.Shell.Screen);
        Assert.Equal(keyed.Rows(), clicked.Rows());
    }

    /// <summary>A click on the first crumb of the narrowed shell's breadcrumb walks back to it.</summary>
    [Fact]
    public async Task AClickOnACrumbWalksBackToIt()
    {
        using var drawn = await Narrowed(Two());

        drawn.Press(Key.Enter);
        drawn.Settle();

        Assert.Equal(2, drawn.Shell.Depth);

        var crumb = drawn.Rows()[0].IndexOf(drawn.Shell.Crumbs[0], Left + RailLines.Width, StringComparison.Ordinal);

        Assert.True(crumb >= 0, drawn.Rows()[0]);

        drawn.Click(crumb + 1, 0);
        drawn.Settle();

        Assert.Equal(1, drawn.Shell.Depth);
        Assert.IsType<FeedScreen>(drawn.Shell.Screen);
    }

    /// <summary>
    ///     Compose's view sits over the narrowed content panel's viewport — inside the panel's edge after the margin and
    ///     the rail, and as wide as the panel's inside — so its fields are under the pointer where they are drawn.
    /// </summary>
    [Fact]
    public async Task ComposeSitsOverTheNarrowedContentPanel()
    {
        using var drawn = await Narrowed();

        drawn.Shell.Compose();
        drawn.Redraw();

        var compose = drawn.Window.SubViews.OfType<ComposeView>().Single().FrameToScreen();

        // The margin's 10, the rail's 20 and the panel's edge; and 180 less the rail and the panel's two edges.
        Assert.Equal((31, 1, 158), (compose.X, compose.Y, compose.Width));
    }

    /// <summary>A click on a compose field in the narrowed shell moves the typing to it.</summary>
    [Fact]
    public async Task AClickOnAComposeFieldMovesTheTypingThere()
    {
        using var drawn = await Narrowed(new AShell { Accounts = FakeAccountRelationships.HoldingNobody() });

        drawn.Shell.Compose();
        drawn.Redraw();

        var lang = drawn.Window.SubViews.OfType<ComposeView>().Single().SubViews.OfType<ComposeLangField>().Single();
        var at = lang.FrameToScreen();

        Assert.InRange(at.X, 31, 188);

        drawn.Click(at.X, at.Y);

        Assert.Equal(ComposeField.Lang, Assert.IsType<ComposeScreen>(drawn.Shell.Screen).Typing);
        Assert.True(lang.HasFocus);
    }

    /// <summary>
    ///     Dragging an attachment's row by the pointer in the narrowed shell moves it live, and it stays where it is let
    ///     go, as at full width.
    /// </summary>
    [Fact]
    public async Task DraggingAnAttachmentsRowReordersIt()
    {
        using var files = new TemporaryDirectory();
        using var drawn = await Narrowed();

        drawn.Shell.Compose();
        drawn.Redraw();

        foreach (var name in new[] { "one.png", "two.png", "three.png" })
        {
            Assert.True(drawn.Shell.Paste(files.WriteFile(name)));
        }

        drawn.Redraw();

        var rows = drawn.Rows();
        var row = Array.FindIndex(rows, line => line.Contains("three.png", StringComparison.Ordinal));
        var column = rows[row].IndexOf("three.png", StringComparison.Ordinal);

        Assert.InRange(column, 31, 188);

        drawn.Point(column, row, MouseFlags.LeftButtonPressed);
        drawn.Point(column, row - 1, MouseFlags.LeftButtonPressed | MouseFlags.PositionReport);
        drawn.Point(column, row - 2, MouseFlags.LeftButtonPressed | MouseFlags.PositionReport);
        drawn.Point(column, row - 2, MouseFlags.LeftButtonReleased);
        drawn.Point(column, row - 2, MouseFlags.LeftButtonClicked);

        var compose = Assert.IsType<ComposeScreen>(drawn.Shell.Screen);

        Assert.Equal(["three.png", "one.png", "two.png"], compose.Media.Attachments.Select(attachment => attachment.Name));
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

    /// <summary>Two posts on Home, the second reading "Two sheep".</summary>
    private static AShell Two() => new()
    {
        Timelines = FakeTimelineReader.Holding(
            APost.With(id: "110"),
            APost.With(id: "220", content: "Two sheep")),
    };

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
