using Terminal.Gui.Input;
using Wooly.Core.Configuration;
using Wooly.Tests.Fakes;
using Wooly.Tui.Screens;
using Wooly.Tui.Shell;
using Wooly.Tui.Theme;
using static Wooly.Tests.Tui.ScrollbarFeeds;

namespace Wooly.Tests.Tui;

/// <summary>
///     The <b>Scrollbar</b> on the content panel's right edge (#500): where the <b>Page</b> stands among a screen's rows,
///     the edge's own <c>│</c> the track and a <c>█</c> thumb on it. Drawn through the whole shell headless and read back
///     off the right edge's cells.
/// </summary>
public class ScrollbarTests
{
    /// <summary>The terminal's rows beside the content panel's viewport at 80 × 24: from under its top edge to over its foot.</summary>
    private const int First = 1;

    private const int Last = 21;

    /// <summary>
    ///     Six posts at 80 × 24 are 43 rows — seven a post and the list's foot — on a page of 21: the thumb is 21 × 21 / 43,
    ///     ten rows, and drawn from the top of the track on a page at the top of the screen.
    /// </summary>
    [Fact]
    public async Task OnAScreenThatOverflowsTheThumbIsThePagesShareOfTheTrackAndStartsAtItsTop()
    {
        using var drawn = await Feed(Posts(6), 24);

        Assert.Equal(43, RowsOf(drawn));
        Assert.Equal(EdgeWithThumb(1, 10), Edge(drawn));
    }

    /// <summary>Two posts fit the page, so the right edge is drawn as it always was: <c>│</c> the whole way down.</summary>
    [Fact]
    public async Task NoBarWhenEveryRowFits()
    {
        using var drawn = await Feed(Posts(2), 24, Themes.Dark);

        Assert.Equal(EdgeWithThumb(First, 0), Edge(drawn));
        Assert.All(
            Enumerable.Range(First, Last - First + 1),
            row => Assert.Equal(Themes.Dark.For(Role.PanelBorderActive), drawn.Cell(row, 79)));
    }

    /// <summary>
    ///     The thumb in the scrollbar's own role and the track in the inactive edge's, while the top, left and bottom
    ///     edges stay the active edge's.
    /// </summary>
    [Fact]
    public async Task TheThumbIsInItsOwnRoleAndTheTrackInTheInactiveEdges()
    {
        using var drawn = await Feed(Posts(6), 24, Themes.Dark);

        Assert.Equal(Themes.Dark.For(Role.Scrollbar), drawn.Cell(1, 79));
        Assert.Equal(Themes.Dark.For(Role.Scrollbar), drawn.Cell(10, 79));
        Assert.Equal(Themes.Dark.For(Role.PanelBorder), drawn.Cell(11, 79));
        Assert.Equal(Themes.Dark.For(Role.PanelBorder), drawn.Cell(21, 79));
        Assert.Equal(Themes.Dark.For(Role.PanelBorderActive), drawn.Cell(5, RailLines.Width));
        Assert.Equal(Themes.Dark.For(Role.PanelBorderActive), drawn.Cell(22, 40));
        Assert.Equal(Themes.Dark.For(Role.PanelBorderActive), drawn.Cell(22, 79));
        Assert.Equal(Themes.Dark.For(Role.PanelBorderActive), drawn.Cell(0, 79));
    }

    /// <summary>
    ///     Without colour the thumb and the track are drawn in the one pair as every edge is, and told apart by their
    ///     glyphs alone.
    /// </summary>
    [Fact]
    public async Task WithoutColourTheGlyphsCarryIt()
    {
        using var drawn = await Feed(Posts(6), 24, Themes.Plain);

        Assert.Equal(EdgeWithThumb(1, 10), Edge(drawn));
        Assert.Equal(drawn.Cell(5, RailLines.Width), drawn.Cell(1, 79));
        Assert.Equal(drawn.Cell(5, RailLines.Width), drawn.Cell(15, 79));
    }

    /// <summary>The scrollbar's colours, which are the active edge's in each built-in theme.</summary>
    [Fact]
    public void TheScrollbarRoleIsTheActiveEdgesColourInEachBuiltInTheme()
    {
        Assert.Equal("scrollbar", RoleName.Of(Role.Scrollbar));
        Assert.Equal(Themes.Dark.For(Role.PanelBorderActive), Themes.Dark.For(Role.Scrollbar));
        Assert.Equal(Themes.Light.For(Role.PanelBorderActive), Themes.Light.For(Role.Scrollbar));
    }

    /// <summary>
    ///     Two presses of <c>↓</c> are six rows down a page of 21 over 43 rows: the thumb has travelled six
    ///     twenty-seconds of the eleven rows it can, three.
    /// </summary>
    [Fact]
    public async Task TheThumbMovesWithTheArrows()
    {
        using var drawn = await Feed(Posts(6), 24);

        drawn.Press(Key.CursorDown);
        drawn.Press(Key.CursorDown);

        Assert.Equal(6, drawn.Content.Top);
        Assert.Equal(EdgeWithThumb(4, 10), Edge(drawn));

        for (var press = 0; press < 2; press++)
        {
            drawn.Press(Key.CursorUp);
        }

        Assert.Equal(EdgeWithThumb(1, 10), Edge(drawn));
    }

    /// <summary>
    ///     The same, drawn as the running loop draws each press — only what changed, onto the frame before — so the thumb
    ///     is on the frame the rows moved on and never a press behind.
    /// </summary>
    [Fact]
    public async Task TheThumbMovesOnTheSameFrameAsTheRowsWhenDrawnAsTheLoopDraws()
    {
        using var drawn = await Feed(Posts(6), 24);

        drawn.PressAsTheLoopDraws(Key.CursorDown);

        Assert.Equal(3, drawn.Content.Top);
        Assert.Equal(EdgeWithThumb(3, 10), Edge(drawn));

        drawn.PressAsTheLoopDraws(Key.CursorDown);

        Assert.Equal(6, drawn.Content.Top);
        Assert.Equal(EdgeWithThumb(4, 10), Edge(drawn));

        drawn.PressAsTheLoopDraws(Key.End);

        Assert.Equal(EdgeWithThumb(12, 10), Edge(drawn));
    }

    /// <summary><c>End</c> puts the thumb at the foot of the track and <c>Home</c> back at its top.</summary>
    [Fact]
    public async Task EndAndHomeTakeTheThumbToTheFootAndTheTop()
    {
        using var drawn = await Feed(Posts(6), 24);

        drawn.Press(Key.End);

        Assert.Equal(EdgeWithThumb(12, 10), Edge(drawn));

        drawn.Press(Key.Home);

        Assert.Equal(EdgeWithThumb(1, 10), Edge(drawn));
    }

    /// <summary>
    ///     A page turned down is 21 rows of the 22 the page can travel, which rounds to the foot; turned back up, the top.
    /// </summary>
    [Fact]
    public async Task APageTurnMovesTheThumb()
    {
        using var drawn = await Feed(Posts(6), 24);

        drawn.Press(Key.PageDown);

        Assert.Equal(21, drawn.Content.Top);
        Assert.Equal(EdgeWithThumb(12, 10), Edge(drawn));

        drawn.Press(Key.PageUp);

        Assert.Equal(EdgeWithThumb(1, 10), Edge(drawn));
    }

    /// <summary>A notch of the wheel moves the page, and the thumb with it.</summary>
    [Fact]
    public async Task TheWheelMovesTheThumb()
    {
        using var drawn = await Feed(Posts(6), 24);

        for (var notch = 0; notch < 6; notch++)
        {
            drawn.Wheel(50, 10);
        }

        Assert.Equal(6, drawn.Content.Top);
        Assert.Equal(EdgeWithThumb(4, 10), Edge(drawn));
    }

    /// <summary>
    ///     Wheeled on past the list's foot onto the blank under the last thing, the thumb stays at the foot of the track
    ///     rather than running off it.
    /// </summary>
    [Fact]
    public async Task APageScrolledOntoTheBlankUnderTheLastThingHasTheThumbAtTheFoot()
    {
        using var drawn = await Feed(Posts(6), 24);

        for (var notch = 0; notch < 30; notch++)
        {
            drawn.Wheel(50, 10);
        }

        Assert.True(drawn.Content.Top > 43 - 21, $"The page only came to {drawn.Content.Top}.");
        Assert.Equal(EdgeWithThumb(12, 10), Edge(drawn));
    }

    /// <summary>
    ///     <c>k</c> walking the pick to the last post carries the page down with it until the post is on it whole, the
    ///     list's foot still under it: twenty rows of 22, the thumb ten of its eleven.
    /// </summary>
    [Fact]
    public async Task TheThumbFollowsKWhenThePageFollowsThePick()
    {
        using var drawn = await Feed(Posts(6), 24);

        drawn.WalkToTheLast();

        Assert.Equal(20, drawn.Content.Top);
        Assert.Equal(EdgeWithThumb(11, 10), Edge(drawn));
    }

    /// <summary>
    ///     A hundred posts are 701 rows, of which the page is 21: a thumb of one row by its share, drawn two so that it
    ///     can't be lost, and at the foot of the track at the foot of the list.
    /// </summary>
    [Fact]
    public async Task TheThumbIsNeverShorterThanTwoRows()
    {
        using var drawn = await Feed(Posts(100), 24);

        Assert.Equal(EdgeWithThumb(1, 2), Edge(drawn));

        drawn.Press(Key.End);

        Assert.Equal(EdgeWithThumb(20, 2), Edge(drawn));
    }

    /// <summary>
    ///     On a list that reads on, the thumb is measured against the rows loaded so far: at the foot of what was loaded
    ///     it is at the foot of the track, and once the next posts arrive under it, it has moved up off the foot.
    /// </summary>
    [Fact]
    public async Task OnAListThatReadsOnTheThumbIsMeasuredAgainstTheRowsLoadedSoFar()
    {
        var built = new AShell { Timelines = FakeTimelineReader.ReadingOn(Posts(100)) };

        using var drawn = await DrawnShell.Of(80, 24, Themes.Plain, built);

        drawn.Press(Key.End);

        var before = Edge(drawn);

        Assert.EndsWith("██│", before + "│", StringComparison.Ordinal);

        drawn.Settle();

        Assert.Single(built.Timelines.ReadsOn);

        var after = Edge(drawn);

        Assert.True(after.IndexOf('█') < before.IndexOf('█'), $"{before}\n{after}");
        Assert.EndsWith("│", after, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Made taller, the page holds more of the 43 rows and the thumb is measured against it on the first frame at
    ///     the new size: 27 of 43, seventeen rows of a track of 27.
    /// </summary>
    [Fact]
    public async Task ATallerTerminalIsMeasuredOnTheFirstFrameAtItsSize()
    {
        using var drawn = await Feed(Posts(6), 24);

        drawn.Resize(80, 30);

        Assert.Equal(EdgeWithThumb(1, 17, last: 27), Edge(drawn));
    }

    /// <summary>
    ///     Posts whose text wraps at 57 columns take a row more each than at the 75 the hidden rail leaves: 49 rows at
    ///     first, a thumb of nine, and 43 with the rail hidden, a thumb of ten on the first frame without it.
    /// </summary>
    [Fact]
    public async Task HidingTheRailIsMeasuredOnTheFirstFrameWithoutIt()
    {
        var wrapping = Enumerable.Range(1, 6)
                                 .Select(at => APost.With(id: $"{at}", content: new string('w', 30) + " " + new string('v', 30)))
                                 .ToArray();

        using var drawn = await Feed(wrapping, 24);

        Assert.Equal(EdgeWithThumb(1, 9), Edge(drawn));

        drawn.PressAsTheLoopDraws(new Key('\\'));

        Assert.Equal(EdgeWithThumb(1, 10), Edge(drawn));
    }

    /// <summary>
    ///     On a 200-column terminal, posts of 161 columns fit one row of the 177 a screen is drawn in and take two of the
    ///     157 left once the shell is stepped narrower to 180: 43 rows on a page of 27, a thumb of seventeen, then 49, a
    ///     thumb of fifteen on the first frame at the new width.
    /// </summary>
    [Fact]
    public async Task AShellWidthStepIsMeasuredOnTheFirstFrameAtTheNewWidth()
    {
        var wide = Enumerable.Range(1, 6)
                             .Select(at => APost.With(id: $"{at}", content: new string('w', 80) + " " + new string('v', 80)))
                             .ToArray();

        using var drawn = await DrawnShell.Of(
            200,
            30,
            Themes.Plain,
            new AShell { Timelines = FakeTimelineReader.Holding(wide) });

        Assert.Equal(EdgeWithThumb(1, 17, last: 27), Edge(drawn));

        drawn.PressAsTheLoopDraws(new Key('-'));

        Assert.Equal(EdgeWithThumb(1, 15, last: 27), Edge(drawn, column: 189));
    }

    /// <summary>
    ///     Walked back to with <c>esc</c> after reading a post, the feed's thumb is where the page it was left at puts it.
    /// </summary>
    [Fact]
    public async Task AScreenWalkedBackToWithEscShowsTheThumbWhereItsPagePutsIt()
    {
        using var drawn = await Feed(Posts(6), 24);

        drawn.Press(Key.CursorDown);
        drawn.Press(Key.Enter);
        drawn.Settle();

        Assert.IsType<PostScreen>(drawn.Shell.Screen);

        drawn.Press(Key.Esc);

        Assert.Equal(3, drawn.Content.Top);
        Assert.Equal(EdgeWithThumb(3, 10), Edge(drawn));
    }

    /// <summary>
    ///     In the file browser the folder, the filter and the rule under them are pinned at the top of the page: the edge
    ///     beside them is left the active edge's <c>│</c>, and the track begins on the row under the rule — the
    ///     thumb in whichever glyph the <c>scrollbar</c> preference picks (#505).
    /// </summary>
    [Theory]
    [InlineData(ScrollbarStyle.Block, '█')]
    [InlineData(ScrollbarStyle.Line, '┃')]
    [InlineData(ScrollbarStyle.LeftHalf, '▌')]
    [InlineData(ScrollbarStyle.RightHalf, '▐')]
    public async Task InTheFileBrowserTheTrackStartsBelowThePinnedRows(ScrollbarStyle style, char thumb)
    {
        using var files = new TemporaryDirectory();

        for (var at = 0; at < 40; at++)
        {
            files.WriteFile($"picture {at:00}.png");
        }

        using var drawn = await DrawnShell.Of(80, 24, Themes.Dark, new AShell
        {
            LaunchedFrom = files.Path,
            Timelines = FakeTimelineReader.Holding(APost.With(id: "220")),
            Scrollbar = style,
        });

        ComposeRows.Open(drawn.Shell, ComposeFor.Post);
        drawn.Redraw();
        drawn.Press(Key.O.WithCtrl);

        Assert.IsType<FileBrowserScreen>(drawn.Shell.Screen);

        var edge = Edge(drawn);

        Assert.Equal($"│││{thumb}{thumb}", edge[..5]);
        Assert.All([1, 2, 3], row => Assert.Equal(Themes.Dark.For(Role.PanelBorderActive), drawn.Cell(row, 79)));
        Assert.Equal(Themes.Dark.For(Role.Scrollbar), drawn.Cell(4, 79));

        drawn.Press(Key.End);

        Assert.EndsWith($"{thumb}", Edge(drawn), StringComparison.Ordinal);
        Assert.Equal("│││", Edge(drawn)[..3]);
    }

    /// <summary>
    ///     Compose over a feed that overflows has widgets of its own, which do their own scrolling: no bar, the edge
    ///     drawn as it always was.
    /// </summary>
    [Fact]
    public async Task ComposeDrawsNoBar()
    {
        using var drawn = await Feed(Posts(6), 24, Themes.Dark);

        drawn.Shell.Press(ShellKey.C);
        drawn.Redraw();

        Assert.IsType<ComposeScreen>(drawn.Shell.Screen);
        Assert.Equal(EdgeWithThumb(First, 0), Edge(drawn));
        Assert.Equal(Themes.Dark.For(Role.PanelBorderActive), drawn.Cell(5, 79));
    }

    /// <summary>
    ///     The description screen has widgets of its own, which do their own scrolling: no bar, the edge drawn as it
    ///     always was.
    /// </summary>
    [Fact]
    public async Task TheDescriptionScreenDrawsNoBar()
    {
        using var files = new TemporaryDirectory();
        using var drawn = await DrawnShell.Of(100, 30, Themes.Dark, new AShell());

        drawn.Shell.Press(ShellKey.C);
        drawn.Redraw();
        drawn.Shell.Paste(files.WriteFile("cat.png"));
        drawn.Built.Author.Attaching.Single().Ready();
        drawn.Settle();
        drawn.PressThroughTheApplication(Key.CursorUp);
        drawn.PressThroughTheApplication(Key.Enter);
        drawn.Settle();

        Assert.IsType<DescriptionScreen>(drawn.Shell.Screen);
        Assert.DoesNotContain('█', Edge(drawn));
        Assert.Equal(Themes.Dark.For(Role.PanelBorderActive), drawn.Cell(3, 99));
    }

    /// <summary>
    ///     The public profile form, on a terminal too short for its rows, has widgets of its own: no bar, the edge drawn
    ///     as it always was.
    /// </summary>
    [Fact]
    public async Task ThePublicProfileFormDrawsNoBar()
    {
        var me = AnAccount.With(address: "jeff@mastodon.social", author: "Jeff", id: "7");
        var built = new AShell
        {
            Accounts = FakeAccountRelationships.Holding(me),
            PublicProfile = FakePublicProfileEditor.Holding(APublicProfile.With(displayName: "Jeff", account: me)),
        };

        using var drawn = await DrawnShell.Of(80, 12, Themes.Dark, built);

        drawn.Shell.Rail.GoTo(DestinationKind.Profile);
        drawn.Settle();
        drawn.Press(new Key('E'));
        drawn.Settle();

        Assert.IsType<PublicProfileScreen>(drawn.Shell.Screen);
        Assert.True(RowsOf(drawn) > drawn.Content.Viewport.Height, "The public profile form fits the page.");
        Assert.DoesNotContain('█', Edge(drawn));
        Assert.Equal(Themes.Dark.For(Role.PanelBorderActive), drawn.Cell(3, 79));
    }

    /// <summary>
    ///     The rules between posts and the picked post's band still reach the column just inside the edge with a bar
    ///     drawn on it.
    /// </summary>
    [Fact]
    public async Task RulesAndTheBandStillReachTheColumnInsideTheEdge()
    {
        using var drawn = await Feed(Posts(6), 24, Themes.Dark);

        var rules = drawn.Rows()[First..(Last + 1)].Where(row => row[RailLines.Width + 1] == '─').ToList();

        Assert.NotEmpty(rules);
        Assert.All(rules, rule => Assert.Equal(new string('─', 58), rule[(RailLines.Width + 1)..^1]));
        Assert.Equal(Themes.Dark.For(Role.Band).Background, drawn.Cell(1, 78).Background);
    }

    /// <summary>
    ///     <c>scrollbar = "none"</c> in the config file's preferences, read at launch (#501, #505): a screen whose rows
    ///     don't fit the page draws its right edge as it always was, <c>│</c> the whole way down in the active edge's
    ///     colour, at the top and after scrolling to the foot.
    /// </summary>
    [Fact]
    public async Task TurnedOffNoBarIsDrawnOnAScreenThatOverflows()
    {
        using var drawn = await DrawnShell.Of(
            80,
            24,
            Themes.Dark,
            new AShell { Timelines = FakeTimelineReader.Holding(Posts(6)), Scrollbar = ScrollbarStyle.None });

        Assert.True(RowsOf(drawn) > drawn.Content.Viewport.Height, "Six posts fit the page.");
        Assert.Equal(EdgeWithThumb(First, 0), Edge(drawn));

        drawn.Press(Key.End);

        Assert.Equal(EdgeWithThumb(First, 0), Edge(drawn));
        Assert.All(
            Enumerable.Range(First, Last - First + 1),
            row => Assert.Equal(Themes.Dark.For(Role.PanelBorderActive), drawn.Cell(row, 79)));
    }

    /// <summary>
    ///     <c>scrollbar</c> names the thumb's glyph (#505): the same thumb, as long and in the same place, at the top and
    ///     at the foot, in the scrollbar's role on a track in the inactive edge's — only the glyph differs.
    /// </summary>
    [Theory]
    [InlineData(ScrollbarStyle.Block, '█')]
    [InlineData(ScrollbarStyle.Line, '┃')]
    [InlineData(ScrollbarStyle.LeftHalf, '▌')]
    [InlineData(ScrollbarStyle.RightHalf, '▐')]
    public async Task EachStyleDrawsTheThumbInItsGlyph(ScrollbarStyle style, char thumb)
    {
        using var drawn = await Feed(Posts(6), 24, Themes.Dark, style);

        Assert.Equal(EdgeWithThumb(1, 10, thumb: thumb), Edge(drawn));
        Assert.Equal(Themes.Dark.For(Role.Scrollbar), drawn.Cell(1, 79));
        Assert.Equal(Themes.Dark.For(Role.Scrollbar), drawn.Cell(10, 79));
        Assert.Equal(Themes.Dark.For(Role.PanelBorder), drawn.Cell(11, 79));
        Assert.Equal(Themes.Dark.For(Role.PanelBorderActive), drawn.Cell(0, 79));

        drawn.Press(Key.End);

        Assert.Equal(EdgeWithThumb(12, 10, thumb: thumb), Edge(drawn));
    }

    /// <summary>A style is a glyph and nothing else: on a screen whose rows fit, there is no bar in any of them.</summary>
    [Theory]
    [InlineData(ScrollbarStyle.Line)]
    [InlineData(ScrollbarStyle.RightHalf)]
    public async Task NoStyleDrawsABarWhenEveryRowFits(ScrollbarStyle style)
    {
        using var drawn = await Feed(Posts(2), 24, Themes.Dark, style);

        Assert.Equal(EdgeWithThumb(First, 0), Edge(drawn));
        Assert.Equal(Themes.Dark.For(Role.PanelBorderActive), drawn.Cell(5, 79));
    }

    /// <summary>
    ///     The right edge beside the content panel's viewport, as the glyphs drawn there — top to foot, on the terminal's
    ///     last column.
    /// </summary>
    private static string Edge(DrawnShell drawn, int? column = null) =>
        string.Concat(drawn.Rows()[First..^2].Select(row => row[column ?? row.Length - 1]));

    /// <summary>
    ///     The edge as a thumb <paramref name="length" /> rows long from the terminal's row <paramref name="from" />
    ///     draws it in <paramref name="thumb" />, the track on every other row down to <paramref name="last" />.
    /// </summary>
    private static string EdgeWithThumb(int from, int length, int last = Last, char thumb = '█') =>
        string.Concat(Enumerable.Range(First, last - First + 1)
                                .Select(row => row >= from && row < from + length ? thumb : '│'));
}
