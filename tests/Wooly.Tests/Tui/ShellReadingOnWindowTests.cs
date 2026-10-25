using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Wooly.Core.Errors;
using Wooly.Tests.Fakes;
using Wooly.Tui.Rendering;
using Wooly.Tui.Screens;
using Wooly.Tui.Theme;
using Wooly.Tui.Views;

namespace Wooly.Tests.Tui;

/// <summary>
///     Reading on as a reader meets it (#469): walking down Home with <c>k</c> or jumping with <c>End</c>, on a real
///     window, which is the only thing that knows where the page is. Real frames between keys, because the page is
///     settled inside the draw — as <see cref="ShellResumeTests" /> says.
/// </summary>
public class ShellReadingOnWindowTests
{
    /// <summary>
    ///     <c>End</c> picks the last post held and takes the page to the foot of the list, which is within a screenful
    ///     of the end: the older posts are read on and put under it, and neither the pick nor the rows on the page move.
    /// </summary>
    [Fact]
    public async Task End_ReadsOnAndPutsTheOlderPostsBelowWithoutMovingThePage()
    {
        var (window, shell, built) = await Laid();

        using (window)
        {
            var content = Content(window);

            window.Draw();
            built.Timelines.HoldReadsOn();
            window.NewKeyDownEvent(Key.End);
            window.Draw();
            built.Host.Drain();

            var top = content.Top;
            var page = Page(shell, content, built);

            built.Timelines.LetReadsOnGo();
            built.Host.Drain();
            window.Draw();

            var feed = Assert.IsType<FeedScreen>(shell.Screen);
            Assert.Single(built.Timelines.ReadsOn);
            Assert.Equal(80, feed.Posts.Count);
            Assert.Equal("40", feed.Picked?.Id);
            Assert.Equal(top, content.Top);
            Assert.Equal(page[..^1], Page(shell, content, built)[..^1]);
        }
    }

    /// <summary>
    ///     Walking down with <c>k</c> asks nothing while the page is further than a screenful from the end, and reads
    ///     on the first time it comes within one.
    /// </summary>
    [Fact]
    public async Task K_ReadsOnOnlyOnceThePageIsWithinAScreenfulOfTheEnd()
    {
        var (window, shell, built) = await Laid();

        using (window)
        {
            var content = Content(window);

            window.Draw();

            var height = content.Viewport.Height;

            for (var walked = 0; walked < 40 && built.Timelines.ReadsOn.Count == 0; walked++)
            {
                var under = Rows(shell, content, built).Count - (content.Top + height);

                Assert.True(under > height, $"Nothing was read on with {under} rows under a page of {height}.");

                window.NewKeyDownEvent(Key.K);
                window.Draw();
            }

            Assert.Single(built.Timelines.ReadsOn);
            Assert.True(Rows(shell, content, built).Count - (content.Top + height) <= height);
        }
    }

    /// <summary>
    ///     Walking back out of a post drilled into mid-read, with the page still near the end, reads on again — the
    ///     window tells the shell where the page is on the screen walked back to, as on any other.
    /// </summary>
    [Fact]
    public async Task Esc_BackOntoAPageNearTheEndReadsOnAgain()
    {
        var (window, shell, built) = await Laid();

        using (window)
        {
            window.Draw();
            built.Timelines.HoldReadsOn();
            window.NewKeyDownEvent(Key.End);
            window.Draw();
            built.Host.Drain();

            window.NewKeyDownEvent(Key.Enter);
            built.Host.Drain();
            window.Draw();
            built.Timelines.LetReadsOnGo();
            built.Host.Drain();

            window.NewKeyDownEvent(Key.Esc);
            window.Draw();
            built.Host.Drain();
            window.Draw();

            Assert.Equal(2, built.Timelines.ReadsOn.Count);
            Assert.Equal(80, Assert.IsType<FeedScreen>(shell.Screen).Posts.Count);
        }
    }

    /// <summary>
    ///     A read on that fails, on a real window (#462): the foot of the list says so under the last post, where the
    ///     page shows it once the view is taken down past the pick — <c>End</c> leaves the pick's last row at the foot
    ///     of the page, and the foot of the list goes under it rather than moving anything. Taking the view down with
    ///     <c>↓</c> is itself an approach to the end (#461), so it tries again from the same place, and an instance
    ///     still refusing leaves the foot saying so again.
    /// </summary>
    [Fact]
    public async Task End_OntoAReadOnThatFails_DrawsCouldntReadOnAtTheFootOfTheList()
    {
        var (window, shell, built) = await Laid();

        using (window)
        {
            var content = Content(window);

            window.Draw();
            built.Timelines.RefusesReadsOn(new TransientNetworkException(null, 3, new HttpRequestException("Down.")));
            window.NewKeyDownEvent(Key.End);
            window.Draw();
            built.Host.Drain();
            window.Draw();

            window.NewKeyDownEvent(Key.CursorDown);
            window.Draw();
            built.Host.Drain();
            window.Draw();

            Assert.Equal(2, built.Timelines.ReadsOn.Count);
            Assert.Equal(built.Timelines.ReadsOn[0].From, built.Timelines.ReadsOn[1].From);
            Assert.Equal(ReadingOn.FailedSays, Page(shell, content, built)[^1]);
        }
    }

    /// <summary>The content region, which is the one that scrolls and so the one with a page.</summary>
    private static PaintedView Content(ShellWindow window) =>
        window.SubViews.OfType<PaintedView>().Single(view => view.Id == ShellWindow.ContentId);

    /// <summary>Every row the screen on top draws, at the width the region draws them.</summary>
    private static IReadOnlyList<Line> Rows(Wooly.Tui.Shell.Shell shell, PaintedView content, AShell built) =>
        shell.Screen.Lines(new Drawing(content.Viewport.Width, built.Clock.GetUtcNow()));

    /// <summary>The rows on the page, as text.</summary>
    private static string[] Page(Wooly.Tui.Shell.Shell shell, PaintedView content, AShell built) =>
    [
        .. Rows(shell, content, built)
            .Skip(content.Top)
            .Take(content.Viewport.Height)
            .Select(line => line.Text),
    ];

    /// <summary>A shell on Home, which holds a hundred posts and reads on, on a window eighty by twenty.</summary>
    private static async Task<(ShellWindow Window, Wooly.Tui.Shell.Shell Shell, AShell Built)> Laid()
    {
        var built = new AShell
        {
            Timelines = FakeTimelineReader.ReadingOn(
                [.. Enumerable.Range(1, 100).Select(at => APost.With(id: $"{at}"))]),
        };

        var shell = await built.Opened();

        var window = new ShellWindow(shell, Themes.Plain, built.Clock, () => { }, new FakePictures())
        {
            Width = 80,
            Height = 20,
        };

        window.Layout();

        return (window, shell, built);
    }
}
