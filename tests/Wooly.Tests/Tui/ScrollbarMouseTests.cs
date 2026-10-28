using Terminal.Gui.Input;
using Wooly.Core.Posts;
using Wooly.Tests.Fakes;
using Wooly.Tui.Screens;
using Wooly.Tui.Shell;
using Wooly.Tui.Theme;

namespace Wooly.Tests.Tui;

/// <summary>
///     The <b>Scrollbar</b> answering the mouse (#502): a click on the track turns one page toward it, as <c>PgUp</c> and
///     <c>PgDn</c> do. It answers on the edge and the margin column just inside it, beside the bar's rows only. Driven
///     through the whole shell headless.
/// </summary>
public class ScrollbarMouseTests
{
    /// <summary>The content panel's right edge at 80 × 24, on the terminal's last column.</summary>
    private const int Edge = 79;

    /// <summary>The margin column just inside it, which a screen is never drawn in (#495).</summary>
    private const int Margin = 78;

    /// <summary>
    ///     Six posts at 80 × 24 are 43 rows on a page of 21: a thumb from the terminal's row 1 to 10, and the track
    ///     under it to row 21.
    /// </summary>
    private const int BelowTheThumb = 15;

    [Fact]
    public async Task AClickOnTheTrackBelowTheThumbTurnsOnePageDown()
    {
        using var drawn = await Feed(Posts(6));

        var picked = drawn.Shell.Screen.Picked?.Id;

        drawn.Click(Edge, BelowTheThumb);

        Assert.Equal(21, drawn.Content.Top);
        Assert.Equal(picked, drawn.Shell.Screen.Picked?.Id);
    }

    /// <summary>A page turned down puts the thumb at the foot; a click on the track above it turns back to the top.</summary>
    [Fact]
    public async Task AClickOnTheTrackAboveTheThumbTurnsOnePageUp()
    {
        using var drawn = await Feed(Posts(6));

        drawn.Press(Key.PageDown);

        Assert.Equal(21, drawn.Content.Top);

        drawn.Click(Edge, 5);

        Assert.Equal(0, drawn.Content.Top);
    }

    /// <summary>
    ///     With the last post picked the page is 20 rows down, the thumb on the terminal's rows 11 to 20 and one row of
    ///     track under it: a click there turns the page no further than <c>PgDn</c> does from there.
    /// </summary>
    [Fact]
    public async Task AClickOnTheTrackStopsAtTheFootWherePgDnStops()
    {
        using var clicked = await Feed(Posts(6));
        using var pressed = await Feed(Posts(6));

        clicked.WalkToTheLast();
        pressed.WalkToTheLast();

        Assert.Equal(20, clicked.Content.Top);

        clicked.Click(Edge, 21);
        pressed.Press(Key.PageDown);

        Assert.Equal(pressed.Content.Top, clicked.Content.Top);
        Assert.Equal(pressed.Rows(), clicked.Rows());
    }

    /// <summary>
    ///     A page turned by a click leaves the pick where it was, off the page now, and <c>j</c> reclaims from the page it
    ///     came to — as after <c>PgDn</c>.
    /// </summary>
    [Fact]
    public async Task AClickOnTheTrackLeavesThePickAndJReclaims()
    {
        using var clicked = await Feed(Posts(6));
        using var pressed = await Feed(Posts(6));

        clicked.Click(Edge, BelowTheThumb);
        pressed.Press(Key.PageDown);

        Assert.Equal("1", clicked.Shell.Screen.Picked?.Id);

        clicked.Press(Key.J);
        pressed.Press(Key.J);

        Assert.NotEqual("1", pressed.Shell.Screen.Picked?.Id);
        Assert.Equal(pressed.Shell.Screen.Picked?.Id, clicked.Shell.Screen.Picked?.Id);
        Assert.Equal(pressed.Content.Top, clicked.Content.Top);
    }

    /// <summary>A click on the thumb, on the edge or the margin beside it, moves nothing.</summary>
    [Theory]
    [InlineData(Edge)]
    [InlineData(Margin)]
    public async Task AClickOnTheThumbMovesNothing(int column)
    {
        using var drawn = await Feed(Posts(6));

        drawn.Press(Key.CursorDown);

        var rows = drawn.Rows();

        drawn.Click(column, 5);

        Assert.Equal(3, drawn.Content.Top);
        Assert.Equal("1", drawn.Shell.Screen.Picked?.Id);
        Assert.Equal(rows, drawn.Rows());
    }

    /// <summary>The margin column just inside the edge answers as the edge does beside the bar's rows.</summary>
    [Fact]
    public async Task TheMarginBesideTheBarTurnsAPageAsTheEdgeDoes()
    {
        using var drawn = await Feed(Posts(6));

        drawn.Click(Margin, BelowTheThumb);

        Assert.Equal(21, drawn.Content.Top);
        Assert.Equal("1", drawn.Shell.Screen.Picked?.Id);
    }

    /// <summary>
    ///     Where every row fits there is no bar, and the margin column beside a post's row keeps today's meaning: a click
    ///     on the post's blank space picks it.
    /// </summary>
    [Fact]
    public async Task WithNoBarAClickInTheMarginPicksThePostBesideIt()
    {
        using var drawn = await Feed([APost.With(id: "1"), APost.With(id: "2", content: "Two sheep")]);

        drawn.Click(Margin, ContentClicks.RowOf(drawn, "Two sheep"));

        Assert.Equal("2", drawn.Shell.Screen.Picked?.Id);
        Assert.Equal(0, drawn.Content.Top);
    }

    /// <summary>Where every row fits, the edge answers no button: a click on it picks and moves nothing.</summary>
    [Fact]
    public async Task WithNoBarAClickOnTheEdgeDoesNothing()
    {
        using var drawn = await Feed([APost.With(id: "1"), APost.With(id: "2", content: "Two sheep")]);

        drawn.Click(Edge, ContentClicks.RowOf(drawn, "Two sheep"));

        Assert.Equal("1", drawn.Shell.Screen.Picked?.Id);
        Assert.Equal(0, drawn.Content.Top);
    }

    /// <summary>
    ///     With <c>scrollbar = false</c> a screen that overflows draws no bar, so the edge answers no button where the
    ///     track would be and the margin beside a post's row picks it, as before there was a bar.
    /// </summary>
    [Fact]
    public async Task TurnedOffTheEdgeAnswersNoButtonAndTheMarginPicks()
    {
        using var drawn = await DrawnShell.Of(
            80,
            24,
            Themes.Plain,
            new AShell { Timelines = FakeTimelineReader.Holding(Posts(6)), Scrollbar = false });

        drawn.Click(Edge, BelowTheThumb);

        Assert.Equal(0, drawn.Content.Top);
        Assert.Equal("1", drawn.Shell.Screen.Picked?.Id);

        drawn.Click(Margin, BelowTheThumb);

        Assert.Equal(0, drawn.Content.Top);
        Assert.Equal("3", drawn.Shell.Screen.Picked?.Id);
    }

    /// <summary>
    ///     Compose over a feed that overflows has widgets of its own and draws no bar: a click on its edge where the
    ///     track would be leaves the draft where it is.
    /// </summary>
    [Fact]
    public async Task OnComposeTheEdgeAnswersNoButton()
    {
        using var drawn = await Feed(Posts(6));

        drawn.Shell.Press(ShellKey.C);
        drawn.Redraw();

        var rows = drawn.Rows();

        drawn.Click(Edge, BelowTheThumb);

        Assert.IsType<ComposeScreen>(drawn.Shell.Screen);
        Assert.Equal(rows, drawn.Rows());
    }

    /// <summary>
    ///     An open confirmation wins: a click on the track declines it, as <c>PgDn</c> does, and turns nothing — and a
    ///     click on the thumb declines it too, as a click anywhere does.
    /// </summary>
    [Theory]
    [InlineData(BelowTheThumb)]
    [InlineData(5)]
    public async Task AClickOnTheBarDeclinesAnOpenConfirmationAndTurnsNothing(int row)
    {
        var answers = Enumerable.Range(1, 3).Select(at => APost.AnAnswer($"Answer {at}", at)).ToList();

        using var drawn = await Feed([APost.With(id: "110", poll: APost.APoll(options: answers)), .. Posts(6)]);

        drawn.Press(new Key('2'));
        drawn.Press(Key.V);

        Assert.NotNull(drawn.Shell.Asking);

        drawn.Click(Edge, row);

        Assert.Null(drawn.Shell.Asking);
        Assert.Equal(0, drawn.Content.Top);
        Assert.Empty(drawn.Built.Engagement.Votes);
    }

    /// <summary>
    ///     A right click on the bar is not <c>esc</c> there: on a post's thread that overflows, it walks back to nothing.
    /// </summary>
    [Fact]
    public async Task ARightClickOnTheBarDoesNothing()
    {
        using var drawn = await Thread();

        drawn.RightClick(Edge, BelowTheThumb);

        Assert.IsType<PostScreen>(drawn.Shell.Screen);
        Assert.Equal(0, drawn.Content.Top);
    }

    /// <summary>
    ///     A double click on the track is its first click's page turn and nothing more: never a <c>⏎</c> on whatever is
    ///     picked.
    /// </summary>
    [Fact]
    public async Task ADoubleClickOnTheBarIsItsFirstClicksTurnAndNothingMore()
    {
        using var drawn = await Feed(Posts(6));

        drawn.DoubleClick(Edge, BelowTheThumb);

        Assert.IsNotType<PostScreen>(drawn.Shell.Screen);
        Assert.Equal(21, drawn.Content.Top);
    }

    /// <summary>The wheel over the bar scrolls the page as it does anywhere over the panel: a notch is a row.</summary>
    [Fact]
    public async Task TheWheelOverTheBarScrollsAsOverThePanel()
    {
        using var drawn = await Feed(Posts(6));

        drawn.Wheel(Margin, BelowTheThumb);

        Assert.Equal(1, drawn.Content.Top);
    }

    /// <summary>
    ///     On a 200-column terminal stepped narrower to 180 the shell sits in columns 10 to 189, its edge on 189 and its
    ///     margin on 188: the bar answers where it is drawn, and a page of 27 is turned.
    /// </summary>
    [Theory]
    [InlineData(189)]
    [InlineData(188)]
    public async Task OnANarrowedShellTheBarAnswersWhereItIsDrawn(int column)
    {
        using var drawn = await DrawnShell.Of(
            200,
            30,
            Themes.Plain,
            new AShell { Timelines = FakeTimelineReader.Holding(Posts(6)) });

        drawn.PressAsTheLoopDraws(new Key('-'));
        drawn.Redraw();

        Assert.Equal('█', drawn.Rows()[1][189]);
        Assert.Equal('│', drawn.Rows()[27][189]);

        drawn.Click(column, 27);

        Assert.Equal(27, drawn.Content.Top);
    }

    /// <summary>A post with six replies under it, opened: a thread taller than the page.</summary>
    private static async Task<DrawnShell> Thread()
    {
        var built = new AShell
        {
            Timelines = FakeTimelineReader.Holding(APost.With(id: "110", content: "The reply")),
            Engagement = FakePostEngagement.Threaded(
                APost.With(id: "110", content: "The reply"),
                [],
                [.. Enumerable.Range(1, 6).Select(at => APost.With(id: $"12{at}", content: $"Answer {at}"))]),
        };

        var drawn = await DrawnShell.Of(80, 24, Themes.Plain, built);

        drawn.Press(Key.Enter);
        built.Host.Drain();
        drawn.Redraw();

        Assert.IsType<PostScreen>(drawn.Shell.Screen);
        Assert.Contains('█', string.Concat(drawn.Rows()[1..^2].Select(row => row[Edge])));

        return drawn;
    }

    /// <summary>Posts from 1 up to <paramref name="count" />.</summary>
    private static Post[] Posts(int count) =>
        [.. Enumerable.Range(1, count).Select(at => APost.With(id: $"{at}"))];

    private static Task<DrawnShell> Feed(Post[] posts, int height = 24, ITheme? theme = null) =>
        DrawnShell.Of(80, height, theme ?? Themes.Plain, new AShell { Timelines = FakeTimelineReader.Holding(posts) });
}
