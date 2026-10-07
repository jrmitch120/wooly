using Terminal.Gui.App;
using Terminal.Gui.Views;
using Wooly.Tests.Fakes;
using Wooly.Tui.Media;
using Wooly.Tui.Rendering;
using Wooly.Tui.Theme;
using Wooly.Tui.Views;
using Line = Wooly.Tui.Rendering.Line;

namespace Wooly.Tests.Tui;

/// <summary>
///     What a frame says it wants of <see cref="IPictures" />: the pictures on screen and near it, nearest first, with
///     those on screen marked — which is what lets the cache keep what a reader is looking at (ADR-0025, #350).
/// </summary>
public class PictureWantingTests
{
    private const int Width = 40;
    private const int Height = 10;

    /// <summary>
    ///     Scrolled to row 20 of 60, ten rows of page: what is on the page comes first and is marked, then the rest
    ///     by how far it is from the page in either direction. A picture whose box is on the page is on screen even
    ///     when the row that wants it has been scrolled off the top, and a picture wanted twice is said once.
    /// </summary>
    [Fact]
    public async Task AFrameWantsWhatIsNearestFirstWithWhatIsOnScreenMarked()
    {
        var pictures = new FakePictures();
        var rows = Enumerable.Range(0, 60).Select(at => Line.Of($"row {at}", Role.Body)).ToArray();

        Wanting(rows, 15, "behind");
        Wanting(rows, 18, "box");
        rows[19] = rows[19] with { Insets = [new Inset(Picture("box"), Column: 0, Columns: 10, Rows: 4)] };
        Wanting(rows, 25, "here");
        Wanting(rows, 33, "ahead");
        Wanting(rows, 36, "here");
        Wanting(rows, 38, "far");

        using var shown = await Draw(rows, pictures, top: 20);

        Assert.Equal(
            [("box", true), ("here", true), ("ahead", false), ("behind", false), ("far", false)],
            pictures.Frames[^1].Select(wanted => (wanted.Drawn.Id, wanted.OnScreen)));
    }

    /// <summary>
    ///     A page that has not moved reaches two screens above it and two below, since there is no telling which way the
    ///     reader will go: scrolled to row 40 of 100, ten rows of page, that is rows 20 to 69 and nothing past them.
    /// </summary>
    [Fact]
    public async Task AStillPageWantsTwoScreensEitherSide()
    {
        var pictures = new FakePictures();
        var rows = Reach();

        using var shown = await Draw(rows, pictures, top: 40);

        Assert.Equal((20, 69), Edges(pictures.Frames[^1]));
    }

    /// <summary>
    ///     A page moving down reaches three screens below it and one above, so that what the reader is heading for is
    ///     usually here before they arrive: from row 40 down to 45, that is rows 35 to 84.
    /// </summary>
    [Fact]
    public async Task APageScrollingDownWantsThreeScreensBelowAndOneAbove()
    {
        var pictures = new FakePictures();

        using var shown = await Draw(Reach(), pictures, top: 40);

        shown.Step(5);

        Assert.Equal((35, 84), Edges(pictures.Frames[^1]));
    }

    /// <summary>
    ///     And moving up, the reverse: from row 45 up to 40, three screens above and one below — rows 10 to 59.
    /// </summary>
    [Fact]
    public async Task APageScrollingUpWantsThreeScreensAboveAndOneBelow()
    {
        var pictures = new FakePictures();

        using var shown = await Draw(Reach(), pictures, top: 45);

        shown.Step(-5);

        Assert.Equal((10, 59), Edges(pictures.Frames[^1]));
    }

    /// <summary>
    ///     A frame drawn without the page moving — a picture landing redraws the screen — keeps reaching the way the
    ///     page last went. Falling back to two either side there would take the third screen ahead out of what is
    ///     wanted, and the fetches queued for it would be abandoned by the very picture whose arrival redrew it.
    /// </summary>
    [Fact]
    public async Task APageThatStopsKeepsReachingTheWayItWasGoing()
    {
        var pictures = new FakePictures();

        using var shown = await Draw(Reach(), pictures, top: 40);

        shown.Step(5);
        shown.Redraw();

        Assert.Equal((35, 84), Edges(pictures.Frames[^1]));
    }

    /// <summary>
    ///     Rows each wanting a picture named for the row, at the edges of every reach a page at rows 35 to 45 could
    ///     have — so that which of them a frame says is exactly where its reach begins and ends.
    /// </summary>
    private static Line[] Reach()
    {
        var rows = Enumerable.Range(0, 100).Select(at => Line.Of($"row {at}", Role.Body)).ToArray();

        foreach (var at in new[] { 4, 5, 9, 10, 19, 20, 24, 25, 34, 35, 59, 60, 69, 70, 84, 85 })
        {
            Wanting(rows, at, $"{at}");
        }

        return rows;
    }

    /// <summary>The first and last rows a frame reaches to, by the rows its pictures are named for.</summary>
    private static (int First, int Last) Edges(IReadOnlyList<WantedPicture> frame)
    {
        var at = frame.Select(wanted => int.Parse(wanted.Drawn.Id)).ToList();

        return (at.Min(), at.Max());
    }

    private static void Wanting(Line[] rows, int at, string id) => rows[at] = rows[at] with { Wants = Picture(id) };

    private static Drawn Picture(string id) => new(id, $"https://files.mastodon.social/{id}.png");

    private static async Task<Shown> Draw(IReadOnlyList<Line> lines, IPictures pictures, int top)
    {
        await Task.Yield();

        var application = Application.Create();
        application.Init("ansi");
        application.Driver!.SetScreenSize(Width, Height);

        var window = new Runnable { BorderStyle = Terminal.Gui.Drawing.LineStyle.None };
        var view = new PaintedView(Themes.Plain, (_, _) => lines, pictures)
        {
            Width = Width,
            Height = Height,
            Scrolls = true,
        };

        view.Resume(top, following: false);
        window.Add(view);

        application.Begin(window);
        application.LayoutAndDraw(true);

        return new Shown(application, window, view);
    }

    private sealed record Shown(IApplication Application, Runnable Window, PaintedView View) : IDisposable
    {
        /// <summary>
        ///     Moves the page by <paramref name="rows" />, as the arrows do, and draws the frame that follows.
        /// </summary>
        public void Step(int rows)
        {
            View.Step(rows);
            Application.LayoutAndDraw(true);
        }

        /// <summary>Draws a frame with the page where it is, as a picture landing does.</summary>
        public void Redraw()
        {
            View.SetNeedsDraw();
            Application.LayoutAndDraw(true);
        }

        public void Dispose()
        {
            Window.Dispose();
            Application.Dispose();
        }
    }
}
