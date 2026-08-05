using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Wooly.Tests.Fakes;
using Wooly.Tui.Screens;
using Wooly.Tui.Theme;
using Wooly.Tui.Views;

namespace Wooly.Tests.Tui;

/// <summary>
///     Which key means which movement, asked of the window that binds them — the one thing between a keypress and the
///     shell, and the one place the two halves of #51 are told apart.
/// </summary>
/// <remarks>
///     Worth pinning here rather than at the shell, where a test says <c>Walk(1)</c> and proves nothing about what a
///     reader pressed. <c>k</c> being the next post and <c>j</c> the one before it is the opposite way round from vim
///     (<c>docs/tui-shell.md</c>), which is exactly the kind of thing that gets quietly reversed.
/// </remarks>
public class ShellKeyTests
{
    [Fact]
    public async Task K_WalksToTheNextPostAndJToTheOneBeforeIt()
    {
        var (window, shell) = await Opened();

        using (window)
        {
            window.NewKeyDownEvent(Key.K);

            Assert.Equal("220", shell.Screen.Picked?.Id);

            window.NewKeyDownEvent(Key.K);

            Assert.Equal("330", shell.Screen.Picked?.Id);

            window.NewKeyDownEvent(Key.J);

            Assert.Equal("220", shell.Screen.Picked?.Id);
        }
    }

    /// <summary>The arrows are the other movement: the screen walks and the selection stays where it was put.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task TheArrowsLeaveTheSelectionAlone(bool down)
    {
        var (window, shell) = await Opened();

        using (window)
        {
            window.NewKeyDownEvent(Key.K);

            for (var pressed = 0; pressed < 30; pressed++)
            {
                window.NewKeyDownEvent(down ? Key.CursorDown : Key.CursorUp);
            }

            Assert.Equal("220", shell.Screen.Picked?.Id);
        }
    }

    /// <summary>
    ///     <c>PgDn</c> is a screenful of the same movement, so it leaves the selection alone too — it used to walk it
    ///     ten posts, which is several screens on a feed with pictures on it.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ThePageKeysLeaveTheSelectionAlone(bool down)
    {
        var (window, shell) = await Opened();

        using (window)
        {
            window.NewKeyDownEvent(Key.K);
            window.NewKeyDownEvent(down ? Key.PageDown : Key.PageUp);

            Assert.Equal("220", shell.Screen.Picked?.Id);
        }
    }

    /// <summary><c>Home</c> and <c>End</c> are the ends of the list, which are things rather than places.</summary>
    [Theory]
    [InlineData(true, "440")]
    [InlineData(false, "110")]
    public async Task HomeAndEndPickOutTheFirstPostAndTheLast(bool end, string expected)
    {
        var (window, shell) = await Opened();

        using (window)
        {
            window.NewKeyDownEvent(end ? Key.End : Key.Home);

            Assert.Equal(expected, shell.Screen.Picked?.Id);
        }
    }

    /// <summary>
    ///     And the two together: arrows far enough to lose the selection, then <c>k</c>, which takes back the post on
    ///     screen rather than moving on from the one that is not.
    /// </summary>
    [Fact]
    public async Task K_ReclaimsThePostOnScreenAfterTheArrowsHaveWalkedPastIt()
    {
        var (window, shell) = await Opened();

        using (window)
        {
            // Far enough down that the first post has no row left on the page.
            for (var pressed = 0; pressed < 30; pressed++)
            {
                window.NewKeyDownEvent(Key.CursorDown);
            }

            window.NewKeyDownEvent(Key.K);

            // The last post, which is what is on screen down there — not the second, which is where a step from the
            // selection the arrows left behind would have landed.
            Assert.Equal("440", shell.Screen.Picked?.Id);
        }
    }

    /// <summary>A shell of four posts on a window with room for ten rows, laid out and ready for keys.</summary>
    private static async Task<(ShellWindow Window, Wooly.Tui.Shell.Shell Shell)> Opened()
    {
        var built = new AShell
        {
            Timelines = FakeTimelineReader.Holding(
                APost.With(id: "110"),
                APost.With(id: "220"),
                APost.With(id: "330"),
                APost.With(id: "440")),
        };

        var shell = await built.Opened();

        var window = new ShellWindow(shell, Themes.Plain, built.Clock, () => { }, FakePictures.DrawingNothing())
        {
            Width = 80,
            Height = 12,
        };

        window.Layout();

        return (window, shell);
    }
}
