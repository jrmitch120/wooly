using System.Drawing;
using Terminal.Gui.ViewBase;
using Wooly.Core.Posts;
using Wooly.Core.Paging;
using Wooly.Tests.Fakes;
using Wooly.Tui.Screens;
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
    ///     status row — and its rows are the 58 columns inside it, starting on row 1.
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
    ///     column after the rail and the last column, the bottom one on row 22 — and the status row the last row.
    /// </summary>
    [Fact]
    public async Task ThePanelReachesTheLastColumnAndTheStatusRowIsTheLastRow()
    {
        using var drawn = await Draw(80, 24);

        var rows = drawn.Rows();

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
    ///     row under it used to push it to row 2.
    /// </summary>
    [Fact]
    public async Task TheScreensFirstRowIsDrawnInsideTheFrameOnRowOne()
    {
        using var drawn = await Draw(80, 24);

        var first = drawn.Shell.Screen.Lines(new Wooly.Tui.Rendering.Drawing(58, AShell.Now))[0].Text;

        Assert.Equal(first, drawn.Rows()[1][(RailLines.Width + 1)..^1].TrimEnd());
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
    ///     it, and the editor under both — every one of them between the panel's edges, and the edges still drawn.
    /// </summary>
    [Theory]
    [InlineData(80, 24)]
    [InlineData(200, 50)]
    public async Task AReplyDrawsInsideThePanel(int width, int height)
    {
        using var drawn = await Draw(width, height);

        drawn.Shell.Reply();
        drawn.Redraw();

        var compose = Assert.IsType<ComposeScreen>(drawn.Shell.Screen);
        var editor = drawn.Window.SubViews.OfType<ComposeEditor>().Single();
        var inside = new Rectangle(
            drawn.Content.ViewportToScreen(Point.Empty),
            drawn.Content.Viewport.Size);
        var rows = drawn.Rows();

        Assert.Equal(
            new Rectangle(inside.X + 2, inside.Y + 7, inside.Width - 4, inside.Height - 9),
            editor.Frame);

        Assert.StartsWith("  From  @jeff", rows[inside.Y + 1][inside.X..], StringComparison.Ordinal);
        Assert.StartsWith("     ↳  ", rows[inside.Y + 2][inside.X..], StringComparison.Ordinal);
        Assert.StartsWith(ComposeRows.NoWarning, rows[inside.Y + 4][inside.X..], StringComparison.Ordinal);
        Assert.Equal(compose.EditorAt(inside.Size).Y, editor.Frame.Y - inside.Y);
        Assert.StartsWith("╭ Home › Reply to @", rows[0][RailLines.Width..], StringComparison.Ordinal);
        Assert.All(rows[1..(height - 2)], row => Assert.Equal('│', row[RailLines.Width]));
        Assert.All(rows[1..(height - 2)], row => Assert.Equal('│', row[^1]));
        Assert.StartsWith("╰─", rows[height - 2][RailLines.Width..], StringComparison.Ordinal);
    }

    /// <summary>A post with nothing to answer starts the editor under the headers, inside the frame.</summary>
    [Fact]
    public async Task AComposeDrawsInsideThePanel()
    {
        using var drawn = await Draw(80, 24);

        drawn.Shell.Compose();
        drawn.Redraw();

        var editor = drawn.Window.SubViews.OfType<ComposeEditor>().Single();

        Assert.Equal(new Rectangle(RailLines.Width + 3, 6, 54, 14), editor.Frame);
        Assert.StartsWith(ComposeRows.NoWarning, drawn.Rows()[3][(RailLines.Width + 1)..], StringComparison.Ordinal);
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

    private static Task<DrawnShell> Draw(
        int width,
        int height,
        ITheme? theme = null,
        AShell? built = null,
        bool launch = false) =>
        DrawnShell.Of(width, height, theme ?? Themes.Plain, built, launch);
}
