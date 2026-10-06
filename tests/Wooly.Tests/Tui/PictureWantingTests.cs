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
    ///     by how far it is from the page in either direction, and nothing more than a screen above or below it. A
    ///     picture whose box is on the page is on screen even when the row that wants it has been scrolled off the
    ///     top, and a picture wanted twice is said once.
    /// </summary>
    [Fact]
    public async Task AFrameWantsWhatIsNearestFirstWithWhatIsOnScreenMarked()
    {
        var pictures = FakePictures.With();
        var rows = Enumerable.Range(0, 60).Select(at => Line.Of($"row {at}", Role.Body)).ToArray();

        Wanting(rows, 3, "above");
        Wanting(rows, 15, "behind");
        Wanting(rows, 18, "box");
        rows[19] = rows[19] with { Insets = [new Inset(Picture("box"), Column: 0, Columns: 10, Rows: 4)] };
        Wanting(rows, 25, "here");
        Wanting(rows, 33, "ahead");
        Wanting(rows, 36, "here");
        Wanting(rows, 38, "far");
        Wanting(rows, 45, "past");

        using var shown = await Draw(rows, pictures, top: 20);

        Assert.Equal(
            [("box", true), ("here", true), ("ahead", false), ("behind", false), ("far", false)],
            pictures.Frames[^1].Select(wanted => (wanted.Drawn.Id, wanted.OnScreen)));
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

        return new Shown(application, window);
    }

    private sealed record Shown(IApplication Application, Runnable Window) : IDisposable
    {
        public void Dispose()
        {
            Window.Dispose();
            Application.Dispose();
        }
    }
}
