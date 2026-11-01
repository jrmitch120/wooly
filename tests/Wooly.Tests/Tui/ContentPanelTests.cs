using System.Drawing;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Wooly.Core.Posts;
using Wooly.Core.Paging;
using Wooly.Tests.Fakes;
using Wooly.Tui.Screens;
using Wooly.Tui.Shell;
using Wooly.Tui.Theme;
using Wooly.Tui.Views;

namespace Wooly.Tests.Tui;

/// <summary>
///     The content region as a panel (#271, ADR-0021): a frame titled with the breadcrumb, beside the rail and over the
///     status row, with its rows starting on row 1. It replaces the breadcrumb row, the blank row under it and the
///     gutter column, whose dividing the frame now does. Drawn headless and read back off the terminal's cells.
/// </summary>
/// <remarks>
///     What the title says and how it elides is <see cref="BreadcrumbTests" />'s; this is where it is.
/// </remarks>
public class ContentPanelTests
{
    /// <summary>
    ///     At 80×24 the panel runs from the column after the rail to the last column, from row 0 to the row above the
    ///     status row — and its viewport is the 58 columns inside it, starting on row 1, of which a screen is drawn in
    ///     57 (#495).
    /// </summary>
    [Fact]
    public async Task AtEightyByTwentyFourThePanelsRowsAreFiftyEightColumnsWideAndStartOnRowOne()
    {
        using var drawn = await Draw(80, 24);

        var content = drawn.Content;

        Assert.Equal(new Rectangle(RailLines.Width, 0, 80 - RailLines.Width, 23), content.Frame);
        Assert.Equal(new Size(58, 21), content.Viewport.Size);
        Assert.Equal(new Point(RailLines.Width + 1, 1), content.ViewportToScreen(Point.Empty));
    }

    /// <summary>
    ///     The edges are where the frame says: the top one titled with the breadcrumb on row 0, the sides down the
    ///     column after the rail and the last column, the bottom one on row 22 — and the status row the last row. The one
    ///     post fits the page, so the right edge is <c>│</c> the whole way down rather than a scrollbar (#500).
    /// </summary>
    [Fact]
    public async Task ThePanelReachesTheLastColumnAndTheStatusRowIsTheLastRow()
    {
        using var drawn = await Draw(80, 24);

        var rows = drawn.Rows();

        Assert.True(
            ScrollbarFeeds.RowsOf(drawn) <= drawn.Content.Viewport.Height,
            "The screen overflows the page, so its right edge is a scrollbar.");

        Assert.StartsWith("╭ Home ─", rows[0][RailLines.Width..], StringComparison.Ordinal);
        Assert.EndsWith("─╮", rows[0], StringComparison.Ordinal);
        Assert.All(rows[1..22], row => Assert.Equal('│', row[RailLines.Width]));
        Assert.All(rows[1..22], row => Assert.Equal('│', row[^1]));
        Assert.StartsWith("╰─", rows[22][RailLines.Width..], StringComparison.Ordinal);
        Assert.EndsWith("─╯", rows[22], StringComparison.Ordinal);
        Assert.DoesNotContain('│', rows[23]);
        Assert.Contains("Post: j/k", rows[23], StringComparison.Ordinal);
    }

    /// <summary>
    ///     The first row of the screen is drawn on row 1, inside the left edge — where the breadcrumb row and the blank
    ///     row under it used to push it to row 2 — drawn 57 wide, the margin's column given up on the right (#495).
    /// </summary>
    [Fact]
    public async Task TheScreensFirstRowIsDrawnInsideTheFrameOnRowOne()
    {
        using var drawn = await Draw(80, 24);

        var first = drawn.Shell.Screen.Lines(new Wooly.Tui.Rendering.Drawing(57, AShell.Now))[0].Text;

        Assert.Equal(first, drawn.Rows()[1][(RailLines.Width + 1)..^1].TrimEnd());
    }

    /// <summary>
    ///     The rule between two posts runs from the column inside the panel's left edge to the column inside its right
    ///     edge, with no gap at either end — ruled to the view's edge rather than drawn at a width it is handed (#494).
    /// </summary>
    [Fact]
    public async Task TheRuleBetweenTwoPostsRunsFromEdgeToEdge()
    {
        using var drawn = await Draw(
            80,
            24,
            built: new AShell
            {
                Timelines = FakeTimelineReader.Holding(APost.With(id: "1"), APost.With(id: "2")),
            });

        var rules = drawn.Rows()[1..22].Where(row => row[RailLines.Width + 1] == '─').ToList();

        Assert.NotEmpty(rules);
        Assert.All(rules, rule => Assert.Equal(new string('─', 58), rule[(RailLines.Width + 1)..^1]));
    }

    /// <summary>
    ///     A post whose text wraps — a long link broken across rows — wraps one column short of the panel's right edge:
    ///     the column just inside it is the margin, mirroring the gutter on the left, and nothing is written there.
    /// </summary>
    [Fact]
    public async Task APostsWrappedTextLeavesTheMarginBlank()
    {
        using var drawn = await Draw(
            80,
            24,
            built: new AShell
            {
                Timelines = FakeTimelineReader.Holding(
                    APost.With(content: "https://example.com/" + new string('a', 150))),
            });

        var rows = drawn.Rows()[1..22];

        Assert.Contains(rows, row => row[^3] == 'a');
        Assert.All(rows.Where(row => row[RailLines.Width + 1] != '─'), row => Assert.Equal(' ', row[^2]));
    }

    /// <summary>
    ///     A post header's right-aligned age stops at the margin, one column in from the panel's right edge, rather
    ///     than against it.
    /// </summary>
    [Fact]
    public async Task APostHeadersAgeEndsOneColumnInFromTheEdge()
    {
        using var drawn = await Draw(80, 24);

        var header = drawn.Rows()[1];

        Assert.Equal(' ', header[^2]);
        Assert.NotEqual(' ', header[^3]);
    }

    /// <summary>
    ///     The picked post's band covers the margin as it covers the gutter, running to the column just inside the
    ///     panel's right edge past where the words stop.
    /// </summary>
    [Fact]
    public async Task ThePickedPostsBandCoversTheMargin()
    {
        using var drawn = await Draw(80, 24, Themes.Dark);

        Assert.Equal(Themes.Dark.For(Role.Band).Background, drawn.Cell(1, 78).Background);
    }

    /// <summary>
    ///     Compose is sized from the panel's viewport, not the room a screen is drawn in, so the margin takes nothing
    ///     from it: its hairlines still run from two columns inside the left edge to two inside the right.
    /// </summary>
    [Fact]
    public async Task ComposeKeepsItsPaddingInsideTheWholeViewport()
    {
        using var drawn = await Draw(80, 24);

        drawn.Shell.Press(ShellKey.C);
        drawn.Redraw();

        var hairlines = drawn.Rows()[1..22].Where(row => row[RailLines.Width + 3] == '─').ToList();

        Assert.NotEmpty(hairlines);
        Assert.All(hairlines, row => Assert.Equal("  " + new string('─', 54) + "  ", row[(RailLines.Width + 1)..^1]));
        AssertPaddedTwoAndTwo(drawn);
    }

    /// <summary>
    ///     The description editor, a screen with widgets of its own like compose, is drawn the viewport's whole 58
    ///     columns: two blank columns inside the left edge and two inside the right on every row, the margin taking
    ///     nothing from it (#495).
    /// </summary>
    [Fact]
    public async Task TheDescriptionScreenKeepsItsPaddingInsideTheWholeViewport()
    {
        using var files = new TemporaryDirectory();
        using var drawn = await Draw(80, 24);

        drawn.Shell.Press(ShellKey.C);
        drawn.Shell.Paste(files.WriteFile("cat.png"));
        drawn.Settle();
        drawn.Press(Key.CursorUp);
        drawn.Press(Key.Enter);
        drawn.Redraw();

        Assert.IsType<DescriptionScreen>(drawn.Shell.Screen);
        AssertPaddedTwoAndTwo(drawn);
    }

    /// <summary>And the edit public profile form, the third screen with widgets of its own (#495).</summary>
    [Fact]
    public async Task ThePublicProfileFormKeepsItsPaddingInsideTheWholeViewport()
    {
        var me = AnAccount.With(address: "jeff@mastodon.social", author: "Jeff", id: "7");

        using var drawn = await Draw(
            80,
            24,
            built: new AShell
            {
                Accounts = FakeAccountRelationships.Holding(me),
                PublicProfile = FakePublicProfileEditor.Holding(APublicProfile.With(account: me)),
            });

        drawn.Shell.Rail.GoTo(drawn.Shell.Rail.Of(DestinationKind.Profile));
        drawn.Settle();
        drawn.Press(new Key('E'));
        drawn.Settle();
        drawn.Redraw();

        Assert.IsType<PublicProfileScreen>(drawn.Shell.Screen);
        AssertPaddedTwoAndTwo(drawn);
    }

    /// <summary>
    ///     A click in the margin column of a post's row picks that post, as a click on the row's blank space just inside
    ///     the margin does: the margin is part of the row, not a dead zone of a new kind (#493, story 16).
    /// </summary>
    [Fact]
    public async Task AClickInTheMarginPicksThePostAsAClickOnItsBlankSpaceDoes()
    {
        const int blank = 77;
        const int margin = 78;

        using var onBlank = await Draw(80, 24, built: TwoPosts());
        using var onMargin = await Draw(80, 24, built: TwoPosts());

        var row = ContentClicks.RowOf(onBlank, "Two sheep");

        Assert.Equal(' ', onBlank.Rows()[row][blank]);
        Assert.Equal(' ', onMargin.Rows()[row][margin]);

        onBlank.Click(blank, row);
        onMargin.Click(margin, row);

        Assert.Equal("220", onBlank.Shell.Screen.Picked?.Id);
        Assert.Equal("220", onMargin.Shell.Screen.Picked?.Id);
    }

    /// <summary>
    ///     The frame's cells are the theme's answer for the panel roles, the edge in the active one — the content
    ///     panel is the one being read — and the title in the title's.
    /// </summary>
    [Fact]
    public async Task TheFrameIsDrawnInThePanelRoles()
    {
        using var drawn = await Draw(80, 24, Themes.Dark);

        Assert.Equal(Themes.Dark.For(Role.PanelBorderActive), drawn.Cell(0, RailLines.Width));
        Assert.Equal(Themes.Dark.For(Role.PanelBorderActive), drawn.Cell(5, 79));
        Assert.Equal(Themes.Dark.For(Role.PanelBorderActive), drawn.Cell(22, 40));
        Assert.Equal(Themes.Dark.For(Role.PanelTitle), drawn.Cell(0, RailLines.Width + 2));
    }

    /// <summary>
    ///     Nothing divides the rail from the panel but the panel's own edge: there is no gutter column and no blank row
    ///     any more, so no cell between them is left to the window.
    /// </summary>
    [Fact]
    public async Task NoRegionIsLeftBetweenTheRailAndThePanel()
    {
        using var drawn = await Draw(80, 24);

        var regions = drawn.Window.SubViews.OfType<PaintedView>().Where(view => view.Visible).ToList();

        Assert.DoesNotContain(regions, view => view.Frame is { X: RailLines.Width, Width: 1 });
        Assert.DoesNotContain(regions, view => view.Frame is { Y: 1, Height: 1 });
    }

    /// <summary>
    ///     A tick of the fetch mark redraws the title and nothing else, as it redrew the breadcrumb row and nothing
    ///     else before: the panel's rows place every picture on them each time they are drawn, and a tick comes two
    ///     and a half times a second (#217).
    /// </summary>
    [Fact]
    public async Task ATickRedrawsTheTitleAndNotThePanelsRows()
    {
        var held = new TaskCompletionSource<Fetch<Post>>();
        var reads = 0;

        var built = new AShell
        {
            Timelines = FakeTimelineReader.Awaiting(_ => reads++ == 0
                ? Task.FromResult(Fetch<Post>.Complete([APost.With(id: "110")]))
                : held.Task),
        };

        using var drawn = await Draw(80, 24, built: built);

        var title = drawn.Window.SubViews.OfType<PaintedView>()
                         .Single(view => view.Visible && view.Frame == new Rectangle(RailLines.Width, 0, 80 - RailLines.Width, 1));

        var refreshing = drawn.Shell.Refresh();

        built.Host.Drain();
        drawn.Redraw();

        Assert.False(drawn.Content.NeedsDraw);
        Assert.False(title.NeedsDraw);

        built.Host.Settle();

        Assert.Equal(1, drawn.Shell.SpinnerFrame);
        Assert.True(title.NeedsDraw);
        Assert.False(drawn.Content.NeedsDraw);

        held.SetResult(Fetch<Post>.Complete([APost.With(id: "111")]));
        await refreshing;
    }

    /// <summary>
    ///     A wide terminal widens the panel and nothing else: the rail keeps its 20 columns and the panel takes the
    ///     rest, still reaching the last column.
    /// </summary>
    [Fact]
    public async Task AtAWideTerminalThePanelTakesTheRest()
    {
        using var drawn = await Draw(200, 50);

        Assert.Equal(new Rectangle(RailLines.Width, 0, 200 - RailLines.Width, 49), drawn.Content.Frame);
        Assert.Equal(new Size(200 - RailLines.Width - 2, 47), drawn.Content.Viewport.Size);
        Assert.EndsWith("─╮", drawn.Rows()[0], StringComparison.Ordinal);
    }

    /// <summary>
    ///     A reply draws inside the panel: the "answering" block on the panel's first rows, the warning field under
    ///     it, and the editor under both — every one of them between the panel's edges, and the edges still drawn. The
    ///     right edge is <c>│</c> the whole way down whatever the reply's rows: compose has widgets of its own and draws
    ///     no scrollbar (#500).
    /// </summary>
    [Theory]
    [InlineData(80, 24)]
    [InlineData(200, 50)]
    public async Task AReplyDrawsInsideThePanel(int width, int height)
    {
        using var drawn = await Draw(width, height);

        drawn.Shell.Press(ShellKey.R);
        drawn.Redraw();

        var compose = Assert.IsType<ComposeScreen>(drawn.Shell.Screen);
        var editor = drawn.Window.ComposeField<ComposeEditor>();
        var inside = new Rectangle(
            drawn.Content.ViewportToScreen(Point.Empty),
            drawn.Content.Viewport.Size);
        var rows = drawn.Rows();

        Assert.Equal(
            new Rectangle(inside.X + 2, inside.Y + 10, inside.Width - 4, inside.Height - 12),
            editor.FrameToScreen());

        Assert.StartsWith("   From  @jeff", rows[inside.Y + 1][inside.X..], StringComparison.Ordinal);
        Assert.StartsWith(ComposeRows.ToPublic, rows[inside.Y + 2][inside.X..], StringComparison.Ordinal);
        Assert.StartsWith(ComposeRows.NoLanguage, rows[inside.Y + 3][inside.X..], StringComparison.Ordinal);
        Assert.StartsWith("      ↳  ", rows[inside.Y + 4][inside.X..], StringComparison.Ordinal);
        Assert.StartsWith(ComposeRows.NoWarning, rows[inside.Y + 6][inside.X..], StringComparison.Ordinal);
        Assert.Equal(compose.EditorAt(inside.Size).Y, editor.FrameToScreen().Y - inside.Y);
        Assert.StartsWith("╭ Home › Reply to Jeff ", rows[0][RailLines.Width..], StringComparison.Ordinal);
        Assert.All(rows[1..(height - 2)], row => Assert.Equal('│', row[RailLines.Width]));
        Assert.True(compose.HasWidgetsOfItsOwn);
        Assert.All(rows[1..(height - 2)], row => Assert.Equal('│', row[^1]));
        Assert.StartsWith("╰─", rows[height - 2][RailLines.Width..], StringComparison.Ordinal);
    }

    /// <summary>A post with nothing to answer starts the editor under the headers, inside the frame.</summary>
    [Fact]
    public async Task AComposeDrawsInsideThePanel()
    {
        using var drawn = await Draw(80, 24);

        drawn.Shell.Press(ShellKey.C);
        drawn.Redraw();

        var editor = drawn.Window.ComposeField<ComposeEditor>();

        Assert.Equal(new Rectangle(RailLines.Width + 3, 9, 54, 11), editor.FrameToScreen());
        Assert.StartsWith(ComposeRows.ToAccountDefault, drawn.Rows()[3][(RailLines.Width + 1)..], StringComparison.Ordinal);
        Assert.StartsWith(ComposeRows.NoLanguage, drawn.Rows()[4][(RailLines.Width + 1)..], StringComparison.Ordinal);
        Assert.StartsWith(ComposeRows.NoWarning, drawn.Rows()[5][(RailLines.Width + 1)..], StringComparison.Ordinal);
    }

    /// <summary>
    ///     With the rail hidden — adding a profile on first run, with nobody to act as — the panel takes the full width,
    ///     its left edge in the first column.
    /// </summary>
    [Fact]
    public async Task WithTheRailHiddenThePanelTakesTheFullWidth()
    {
        var built = new AShell { Profiles = FakeProfileRegistry.Holding(current: null) };

        using var drawn = await Draw(80, 24, built: built, launch: true);

        Assert.Equal(new Rectangle(0, 0, 80, 23), drawn.Content.Frame);
        Assert.Equal(new Size(78, 21), drawn.Content.Viewport.Size);
        Assert.StartsWith("╭ Add a profile ─", drawn.Rows()[0], StringComparison.Ordinal);
    }

    /// <summary>
    ///     That every row inside the panel's edges leaves the two columns inside its left edge and the two inside its
    ///     right blank, with something written in the column before those two — padding kept evenly either side of the
    ///     viewport's whole width, which a screen drawn a column short of it would not keep.
    /// </summary>
    private static void AssertPaddedTwoAndTwo(DrawnShell drawn)
    {
        var inside = drawn.Rows()[1..22].Select(row => row[(RailLines.Width + 1)..^1]).ToList();

        Assert.All(inside, row => Assert.Equal("  ", row[..2]));
        Assert.All(inside, row => Assert.Equal("  ", row[^2..]));
        Assert.Contains(inside, row => row[^3] != ' ');
    }

    private static AShell TwoPosts() => new()
    {
        Timelines = FakeTimelineReader.Holding(APost.With(id: "110"), APost.With(id: "220", content: "Two sheep")),
    };

    private static Task<DrawnShell> Draw(
        int width,
        int height,
        ITheme? theme = null,
        AShell? built = null,
        bool launch = false) =>
        DrawnShell.Of(width, height, theme ?? Themes.Plain, built, launch);
}
