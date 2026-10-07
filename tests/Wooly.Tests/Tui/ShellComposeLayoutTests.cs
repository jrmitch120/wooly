using System.Drawing;
using Terminal.Gui.ViewBase;
using Wooly.Tests.Fakes;
using Wooly.Tui.Screens;
using Wooly.Tui.Theme;
using Wooly.Tui.Views;

namespace Wooly.Tests.Tui;

/// <summary>
///     What the shell's window knows of compose (#366): it puts <see cref="ComposeView" /> over the content panel's
///     viewport, and compose's fields show only while compose is on top. Where each field sits in that viewport, and
///     everything they do, is <see cref="ComposeViewTests" />' and the tests of each field's.
/// </summary>
public class ShellComposeLayoutTests
{
    /// <summary>
    ///     The view sits exactly over the content panel's viewport, inside the panel's edge beside the rail, on every
    ///     compose.
    /// </summary>
    [Theory]
    [InlineData(ComposeFor.Post)]
    [InlineData(ComposeFor.Reply)]
    [InlineData(ComposeFor.Edit)]
    public async Task TheWindowPutsComposeViewOverTheContentViewport(ComposeFor opening)
    {
        var (window, shell) = await Opened();

        using (window)
        {
            ComposeRows.Open(shell, opening);
            window.Layout();

            var content = Content(window);
            var viewport = new Rectangle(content.FrameToScreen().Location + new Size(1, 1), content.Viewport.Size);

            Assert.Equal(viewport, Compose(window).FrameToScreen());
        }
    }

    /// <summary>
    ///     Compose's fields show only while compose is on top: none before it opens, every one once it does, and none
    ///     again once it is gone — and the content under them stops scrolling for as long as they show, since nothing
    ///     below what is being answered can be read behind the editor (ADR-0015).
    /// </summary>
    [Fact]
    public async Task ComposesFieldsShowOnlyWhileComposeIsOnTop()
    {
        var (window, shell) = await Opened();

        using (window)
        {
            var content = Content(window);

            Assert.False(Compose(window).Visible);
            Assert.True(content.Scrolls);

            shell.Reply();
            window.Layout();

            Assert.True(Compose(window).Visible);
            Assert.All(Compose(window).SubViews.Where(view => view is not PaintedView), view => Assert.True(view.Visible));
            Assert.False(content.Scrolls);

            shell.Back();
            window.Layout();

            Assert.False(Compose(window).Visible);
            Assert.All(Compose(window).SubViews, view => Assert.False(view.Visible));
            Assert.True(content.Scrolls);
        }
    }

    /// <summary>
    ///     The content panel redrawn on a frame of its own leaves compose's fields drawn over it. The pictures on the
    ///     page a reply was opened from are let go of while the panel draws, which lays the panel out again on the next
    ///     frame — and the panel, redrawn alone, painted its rows over a mention nothing had changed, leaving it blank on
    ///     the terminal until a key redrew the editor.
    /// </summary>
    [Fact]
    public async Task APanelRedrawnAloneLeavesComposeDrawnOverIt()
    {
        var built = new AShell
        {
            Timelines = FakeTimelineReader.Holding(
                APost.With(id: "220", account: "ben@hachyderm.io"),
                APost.With(id: "330", media: [APost.APicture("m1")])),
        };

        using var drawn = await DrawnShell.Of(
            100,
            30,
            Themes.Plain,
            built,
            pictures: new FakePictures().Holding("m1", 800, 400),
            drawsPictures: true);

        // Two frames as the running client draws them, not forced as Redraw() does: a forced frame redraws compose
        // along with everything else, and the panel redrawn alone on the second is the whole of what is being pinned.
        drawn.Shell.Reply();
        drawn.Application.LayoutAndDraw(false);
        drawn.Application.LayoutAndDraw(false);

        var editor = Editor(drawn.Window).FrameToScreen();

        Assert.StartsWith("@ben@hachyderm.io ", drawn.Rows()[editor.Y][editor.X..editor.Right], StringComparison.Ordinal);
    }

    /// <summary>A window showing one post by somebody else, laid out and ready.</summary>
    private static async Task<(ShellWindow Window, Wooly.Tui.Shell.Shell Shell)> Opened()
    {
        var built = new AShell
        {
            Timelines = FakeTimelineReader.Holding(APost.With(id: "220", account: "jeff@mastodon.social")),
        };

        var shell = await built.Opened();

        var window = new ShellWindow(shell, Themes.Plain, built.Clock, () => { }, new FakePictures())
        {
            Width = 80,
            Height = 20,
        };

        window.Layout();

        return (window, shell);
    }

    private static PaintedView Content(View window) =>
        window.SubViews.OfType<PaintedView>().Single(view => view.Id == ShellWindow.ContentId);

    private static ComposeView Compose(View window) => window.SubViews.OfType<ComposeView>().Single();

    private static ComposeEditor Editor(View window) => Compose(window).SubViews.OfType<ComposeEditor>().Single();
}
