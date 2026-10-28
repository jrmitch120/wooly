using Terminal.Gui.Input;
using Wooly.Tests.Fakes;
using Wooly.Tui.Theme;
using static Wooly.Tests.Tui.ScrollbarFeeds;

namespace Wooly.Tests.Tui;

/// <summary>
///     The <b>Scrollbar</b>'s thumb dragged (#503): pressed on and dragged, the <b>Page</b> follows the pointer row by
///     row, the grabbed point of the thumb kept under it and held at the track's ends, the mouse held until it is let go
///     wherever the pointer wanders. It moves the page and not <b>Picked</b>, as the wheel does. Driven through the whole
///     shell headless.
/// </summary>
public class ScrollbarDragTests
{
    /// <summary>The content panel's right edge at 80 × 24, on the terminal's last column.</summary>
    private const int Edge = 79;

    /// <summary>The margin column just inside it, which a screen is never drawn in (#495).</summary>
    private const int Margin = 78;

    /// <summary>
    ///     Six posts at 80 × 24 are 43 rows on a page of 21: a thumb ten rows long on the terminal's rows 1 to 10, with
    ///     eleven rows to travel for the 22 the page can — two rows of the page to a row of the track.
    /// </summary>
    private const int Grabbed = 3;

    /// <summary>
    ///     Grabbed two rows into the thumb and dragged three rows down, the thumb comes three rows down with the grabbed
    ///     point under the pointer, and the page six — on the frame the motion is drawn in.
    /// </summary>
    [Theory]
    [InlineData(Edge)]
    [InlineData(Margin)]
    public async Task DraggingTheThumbMovesThePageAndTheThumbTogether(int column)
    {
        using var drawn = await Feed(Posts(6));

        Press(drawn, column, Grabbed);
        Move(drawn, column, 6);

        Assert.Equal(6, drawn.Content.Top);
        Assert.Equal("│██████████│", EdgeBetween(drawn, 3, 15));
    }

    /// <summary>
    ///     As a terminal reports it and the running loop draws it, pass by pass: the thumb is on the row the page has
    ///     come to in the very pass the motion lands in, and the let-go ends the drag with nothing clicked.
    /// </summary>
    [Fact]
    public async Task AsTheLoopDrawsTheThumbMovesInTheMotionsPass()
    {
        using var drawn = await Feed(Posts(6));

        drawn.Reports(Sgr.Press(Edge, Grabbed));
        drawn.Reports(Sgr.Drag(Edge, 6));

        Assert.Equal(6, drawn.Content.Top);
        Assert.Equal("│██████████│", EdgeBetween(drawn, 3, 15));

        drawn.Reports(Sgr.Drag(Edge, 9));

        Assert.Equal(12, drawn.Content.Top);
        Assert.Equal("│██████████│", EdgeBetween(drawn, 6, 18));

        drawn.Reports(Sgr.Release(Edge, 9));

        Assert.False(drawn.Application.Mouse.IsGrabbed());
        Assert.Equal(12, drawn.Content.Top);
        Assert.Equal("1", drawn.Shell.Screen.Picked?.Id);
    }

    /// <summary>Dragged past the foot of the track, the thumb and the page are held at the foot; past the top, at the top.</summary>
    [Fact]
    public async Task DraggedPastAnEndTheThumbIsHeldThere()
    {
        using var drawn = await Feed(Posts(6));

        Press(drawn, Edge, Grabbed);
        Move(drawn, Edge, 23);

        Assert.Equal(22, drawn.Content.Top);
        Assert.Equal("│██████████", EdgeBetween(drawn, 11, 22));

        Move(drawn, Edge, 0);

        Assert.Equal(0, drawn.Content.Top);
        Assert.Equal("██████████│", EdgeBetween(drawn, 1, 12));
    }

    /// <summary>
    ///     The drag holds the mouse: dragged sideways off the bar into the posts, and on out of the panel onto the status
    ///     row, the page still follows the pointer's row, and letting go there ends it — the next click is a click.
    /// </summary>
    [Fact]
    public async Task TheDragIsKeptOffTheBarUntilLetGoAnywhere()
    {
        using var drawn = await Feed(Posts(6));

        Press(drawn, Edge, Grabbed);

        Assert.True(drawn.Application.Mouse.IsGrabbed());

        Move(drawn, 20, 8);

        Assert.Equal(10, drawn.Content.Top);
        Assert.Equal("1", drawn.Shell.Screen.Picked?.Id);

        Move(drawn, 40, 23);

        Assert.Equal(22, drawn.Content.Top);

        drawn.Point(40, 23, MouseFlags.LeftButtonReleased);
        drawn.Point(40, 23, MouseFlags.LeftButtonClicked);

        Assert.False(drawn.Application.Mouse.IsGrabbed());
        Assert.Equal(22, drawn.Content.Top);
        Assert.Equal("1", drawn.Shell.Screen.Picked?.Id);

        drawn.Click(Edge, 5);

        Assert.Equal(1, drawn.Content.Top);
    }

    /// <summary>
    ///     A drag let go on the rail's <c>«</c> mark, which a click hides the rail by, is the drag's let-go and nothing
    ///     more: the rail stays.
    /// </summary>
    [Fact]
    public async Task ADragLetGoOnTheRailsMarkLeavesTheRail()
    {
        using var drawn = await Feed(Posts(6));

        Press(drawn, Edge, Grabbed);
        Move(drawn, 1, 22);
        Let(drawn, 1, 22);

        Assert.False(drawn.Shell.HidesRail);
        Assert.Equal(22, drawn.Content.Top);
    }

    /// <summary>
    ///     A drag leaves the pick where it was, off the page now, and <c>j</c> reclaims from the page it came to — as
    ///     after <c>↓</c> pressed as far.
    /// </summary>
    [Fact]
    public async Task ADragLeavesThePickAndJReclaims()
    {
        using var dragged = await Feed(Posts(6));
        using var pressed = await Feed(Posts(6));

        Press(dragged, Edge, Grabbed);
        Move(dragged, Edge, 9);
        Let(dragged, Edge, 9);

        pressed.Press(Key.CursorDown);
        pressed.Press(Key.CursorDown);
        pressed.Press(Key.CursorDown);
        pressed.Press(Key.CursorDown);

        Assert.Equal(pressed.Content.Top, dragged.Content.Top);
        Assert.Equal("1", dragged.Shell.Screen.Picked?.Id);

        dragged.Press(Key.J);
        pressed.Press(Key.J);

        Assert.NotEqual("1", pressed.Shell.Screen.Picked?.Id);
        Assert.Equal(pressed.Shell.Screen.Picked?.Id, dragged.Shell.Screen.Picked?.Id);
        Assert.Equal(pressed.Content.Top, dragged.Content.Top);
    }

    /// <summary>A reference walked to on the first post, carried off the page by a drag, is let go.</summary>
    [Fact]
    public async Task AHeldReferenceDraggedOffThePageIsLetGo()
    {
        using var drawn = await Feed([APost.With(id: "1", content: "Shearing #wool"), .. Posts(6)[1..]]);

        drawn.Press(Key.CursorRight);

        Assert.NotNull(drawn.Shell.Screen.Reference);

        Press(drawn, Edge, Grabbed);
        Move(drawn, Edge, 9);
        Let(drawn, Edge, 9);

        Assert.Null(drawn.Shell.Screen.Reference);
        Assert.Equal("1", drawn.Shell.Screen.Picked?.Id);
    }

    /// <summary>The thumb dragged to the foot of a list that reads on reads it on, as <c>End</c> does there (#469).</summary>
    [Fact]
    public async Task DraggingToTheFootReadsTheListOn()
    {
        var built = new AShell
        {
            Timelines = FakeTimelineReader.ReadingOn(
                [.. Enumerable.Range(1, 100).Select(at => APost.With(id: $"{at}"))]),
        };

        using var drawn = await DrawnShell.Of(80, 24, Themes.Plain, built);

        Press(drawn, Edge, 1);
        Move(drawn, Edge, 23);
        Let(drawn, Edge, 23);
        drawn.Settle();

        Assert.Single(built.Timelines.ReadsOn);
        Assert.Equal("1", drawn.Shell.Screen.Picked?.Id);
    }

    /// <summary>
    ///     The grabbed point stays under the pointer when rows arrive mid-drag: at 80 × 60 the first posts read are a
    ///     thumb twelve rows long, grabbed by its last row and dragged to the foot, which reads the list on and halves
    ///     it. The next motion puts the thumb's last row — the nearest it has to the point grabbed — back under the
    ///     pointer, rather than the thumb where the press's start would put it, clear of the pointer.
    /// </summary>
    [Fact]
    public async Task RowsArrivingMidDragKeepTheGrabbedPointUnderThePointer()
    {
        var built = new AShell
        {
            Timelines = FakeTimelineReader.ReadingOn(
                [.. Enumerable.Range(1, 100).Select(at => APost.With(id: $"{at}"))]),
        };

        using var drawn = await DrawnShell.Of(80, 60, Themes.Plain, built);

        Assert.Equal("████████████│", EdgeBetween(drawn, 1, 14));

        Press(drawn, Edge, 12);
        Move(drawn, Edge, 57);
        drawn.Settle();

        Assert.Single(built.Timelines.ReadsOn);

        Move(drawn, Edge, 40);

        Assert.Equal("│██████│", EdgeBetween(drawn, 34, 42));
    }

    /// <summary>With a confirmation open a drag moves nothing, and its let-go declines the confirmation as a click does.</summary>
    [Fact]
    public async Task WithAConfirmationOpenADragMovesNothing()
    {
        var answers = Enumerable.Range(1, 3).Select(at => APost.AnAnswer($"Answer {at}", at)).ToList();

        using var drawn = await Feed([APost.With(id: "110", poll: APost.APoll(options: answers)), .. Posts(6)]);

        drawn.Press(new Key('2'));
        drawn.Press(Key.V);

        Assert.NotNull(drawn.Shell.Asking);

        Press(drawn, Edge, 1);
        Move(drawn, Edge, 12);

        Assert.Equal(0, drawn.Content.Top);

        Let(drawn, Edge, 12);

        Assert.Equal(0, drawn.Content.Top);
        Assert.Null(drawn.Shell.Asking);
        Assert.Empty(drawn.Built.Engagement.Votes);
    }

    /// <summary>The left button pressed on the cell.</summary>
    private static void Press(DrawnShell drawn, int column, int row) =>
        drawn.Point(column, row, MouseFlags.LeftButtonPressed);

    /// <summary>The pointer moved to the cell with the left button held.</summary>
    private static void Move(DrawnShell drawn, int column, int row) =>
        drawn.Point(column, row, MouseFlags.LeftButtonPressed | MouseFlags.PositionReport);

    /// <summary>The left button let go on the cell, and the click Terminal.Gui makes of it.</summary>
    private static void Let(DrawnShell drawn, int column, int row)
    {
        drawn.Point(column, row, MouseFlags.LeftButtonReleased);
        drawn.Point(column, row, MouseFlags.LeftButtonClicked);
    }

    /// <summary>The right edge read down the terminal's rows <paramref name="from" /> to <paramref name="to" />.</summary>
    private static string EdgeBetween(DrawnShell drawn, int from, int to) =>
        string.Concat(drawn.Rows()[from..to].Select(row => row[Edge]));
}
