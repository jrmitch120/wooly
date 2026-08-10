using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Wooly.Core.Posts;
using Wooly.Tests.Fakes;
using Wooly.Tui.Screens;
using Wooly.Tui.Theme;
using Wooly.Tui.Views;

namespace Wooly.Tests.Tui;

/// <summary>
///     Where the editor sits while a reply is being written. ADR-0015 has said since it was written that a reply
///     "draws what it is answering at the top ... and then the editor underneath", but the editor is a separate view
///     laid over the one that paints those rows — so until it was moved down off them, it covered every one of them
///     and the block was never seen (#82).
/// </summary>
/// <remarks>
///     Worth pinning because the failure is silent both ways. The block being painted is not the block being visible,
///     so a test of the rows alone passes over an editor drawn on top of them; and the position is a
///     <c>Pos.Func</c> read only on a layout pass, which the first attempt at this got wrong by leaving <c>Y</c> at 1
///     with nothing to say so.
/// </remarks>
public class ShellComposeLayoutTests
{
    /// <summary>The content region's width at an 80-column terminal, which is what the rail leaves (RailLines).</summary>
    private const int ContentWidth = 61;

    /// <summary>
    ///     The editor starts below what is being answered rather than on top of it — three rows here: the label, the
    ///     one row "Hello world" wraps to, and the blank under it.
    /// </summary>
    [Fact]
    public async Task Reply_StartsTheEditorBelowWhatIsBeingAnswered()
    {
        var (window, editor, compose) = await Replying();

        using (window)
        {
            Assert.Equal(3, compose.AnsweringHeight(ContentWidth));
            Assert.Equal(4, editor.Frame.Y);
        }
    }

    /// <summary>
    ///     And a post with no reply behind it leaves it where it always was, since there is nothing above it to clear.
    /// </summary>
    [Fact]
    public async Task Post_StartsTheEditorAtTheTopOfTheContentRegion()
    {
        var (window, shell) = await Opened(height: 20);

        using (window)
        {
            shell.Compose();
            window.Layout();

            Assert.Equal(1, Editor(window).Frame.Y);
        }
    }

    /// <summary>
    ///     A terminal too short for both keeps the editor and gives up the tail of what is being answered — the block
    ///     wants three rows and there is only room for two, because an editor pushed to the foot is one nobody can
    ///     type in.
    /// </summary>
    /// <remarks>
    ///     Seven rows: one for the breadcrumb and one for the status row leave the content region five, of which the
    ///     editor keeps three.
    /// </remarks>
    [Fact]
    public async Task Reply_NeverPushesTheEditorPastTheRoomLeftToTypeIn()
    {
        var (window, editor, compose) = await Replying(height: 7);

        using (window)
        {
            Assert.Equal(3, compose.AnsweringHeight(ContentWidth));
            Assert.Equal(2, editor.Frame.Y - 1);
            Assert.Equal(3, editor.Frame.Height);
        }
    }

    /// <summary>
    ///     What is being answered stays put. The region it is painted on is the one the arrows scroll, and everything
    ///     below the block on it is behind the editor — so a scroll here takes the block off the top and puts rows in
    ///     its place that are half of a post nobody can see the rest of.
    /// </summary>
    /// <remarks>
    ///     It never came back, either: a compose screen has no selection on it, so <c>Scroll.To</c> takes its "nothing
    ///     picked out — stay exactly where the arrows left us" branch and the offset stands for as long as the screen
    ///     does. ADR-0015 said the timeline does not scroll while composing; this is what says it.
    /// </remarks>
    [Fact]
    public async Task Reply_DoesNotLetTheArrowsScrollWhatIsBeingAnswered()
    {
        var (window, editor, _) = await Replying();

        using (window)
        {
            var content = window.SubViews.OfType<PaintedView>().Single(view => view.Id == ShellWindow.ContentId);

            Assert.False(content.Scrolls);

            for (var pressed = 0; pressed < 10; pressed++)
            {
                window.NewKeyDownEvent(Key.CursorDown);
            }

            Assert.Null(content.Reclaimable);
            Assert.Equal(4, editor.Frame.Y);
        }
    }

    /// <summary>And it scrolls again the moment the reply is thrown away, since the feed is back underneath.</summary>
    [Fact]
    public async Task Back_LetsTheFeedScrollAgainOnceTheReplyIsGone()
    {
        var (window, shell) = await Opened(height: 20);

        using (window)
        {
            var content = window.SubViews.OfType<PaintedView>().Single(view => view.Id == ShellWindow.ContentId);

            shell.Reply();

            Assert.False(content.Scrolls);

            shell.Back();

            Assert.True(content.Scrolls);
        }
    }

    /// <summary>A window showing one post by somebody else, laid out and ready.</summary>
    private static async Task<(ShellWindow Window, Wooly.Tui.Shell.Shell Shell)> Opened(int height)
    {
        var built = new AShell
        {
            Timelines = FakeTimelineReader.Holding(APost.With(id: "220", account: "ben@hachyderm.io")),
        };

        var shell = await built.Opened();

        var window = new ShellWindow(shell, Themes.Plain, built.Clock, () => { }, FakePictures.DrawingNothing())
        {
            Width = 80,
            Height = height,
        };

        window.Layout();

        return (window, shell);
    }

    /// <summary>The same, with a reply to that post open in the editor.</summary>
    private static async Task<(ShellWindow Window, ComposeEditor Editor, ComposeScreen Compose)> Replying(
        int height = 20)
    {
        var (window, shell) = await Opened(height);

        shell.Reply();
        window.Layout();

        return (window, Editor(window), (ComposeScreen)shell.Screen);
    }

    private static ComposeEditor Editor(View window) => window.SubViews.OfType<ComposeEditor>().Single();
}
