using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Wooly.Tests.Fakes;
using Wooly.Tui.Rendering;
using Wooly.Tui.Screens;
using Wooly.Tui.Theme;
using Wooly.Tui.Views;

namespace Wooly.Tests.Tui;

/// <summary>
///     Walking back out of a screen and finding the page where it was left (#133). The pick already survives a pop —
///     the stack hands back the very screen that was drilled off — and this is the other half of standing in the same
///     place: the row the content region begins on.
/// </summary>
/// <remarks>
///     Real frames either side of every key, because the offset is settled inside the draw: a window that is only laid
///     out never runs the follow or the clamp that an actual frame runs, which is the same reason the <c>G_</c> tests
///     in <see cref="ShellRefreshTests" /> draw.
/// </remarks>
public class ShellResumeTests
{
    /// <summary>
    ///     The whole of the complaint: drill into a post from well down a feed, press <c>esc</c>, and the feed comes
    ///     back with the picked post drawn where it was rather than pinned to the bottom row.
    /// </summary>
    [Fact]
    public async Task Esc_PutsThePageBackWhereTheReaderLeftIt()
    {
        var (window, shell, built) = await Laid();

        using (window)
        {
            var content = Content(window);

            window.Draw();

            WalkDown(window);

            var left = content.Top;
            var picked = shell.Screen.Picked?.Id;

            Assert.NotEqual(0, left);

            window.NewKeyDownEvent(Key.Enter);
            built.Host.Drain();
            window.Draw();

            Assert.IsType<PostScreen>(shell.Screen);

            window.NewKeyDownEvent(Key.Esc);
            window.Draw();

            Assert.IsType<FeedScreen>(shell.Screen);
            Assert.Equal(picked, shell.Screen.Picked?.Id);
            Assert.Equal(left, content.Top);
        }
    }

    /// <summary>
    ///     A page the reader had walked away from the selection with <c>↓</c> comes back walked away, rather than
    ///     snapped back onto the pick — where they were looking is as much of what they left as the row is.
    /// </summary>
    [Fact]
    public async Task Esc_ComesBackScrolledAwayFromTheSelectionWhereItWasLeft()
    {
        var (window, _, built) = await Laid();

        using (window)
        {
            var content = Content(window);

            window.Draw();

            for (var walked = 0; walked < 6; walked++)
            {
                window.NewKeyDownEvent(Key.CursorDown);
                window.Draw();
            }

            var left = content.Top;

            // The pick is above the page: the scroll being the reader's is the only reason this row is on screen.
            Assert.NotEqual(0, left);
            Assert.NotNull(content.Reclaimable);

            window.NewKeyDownEvent(Key.Enter);
            built.Host.Drain();
            window.Draw();

            window.NewKeyDownEvent(Key.Esc);
            window.Draw();

            Assert.Equal(left, content.Top);
            Assert.NotNull(content.Reclaimable);
        }
    }

    /// <summary>
    ///     Three levels down and three back up: each screen has a page of its own to be put back to, not only the one
    ///     at the bottom of the stack — a feed, a thread and an account, walked out of in that order.
    /// </summary>
    [Fact]
    public async Task Esc_PutsEveryScreenWalkedBackToWhereItWas()
    {
        var (window, shell, built) = await Laid();

        using (window)
        {
            var content = Content(window);

            window.Draw();

            WalkDown(window);

            var feed = content.Top;

            window.NewKeyDownEvent(Key.Enter);
            built.Host.Drain();
            window.Draw();

            // The thread is long enough to scroll, so the post screen has a page of its own that is not row nought.
            for (var walked = 0; walked < 4; walked++)
            {
                window.NewKeyDownEvent(Key.CursorDown);
                window.Draw();
            }

            var thread = content.Top;

            Assert.NotEqual(0, thread);

            window.NewKeyDownEvent(Key.A);
            built.Host.Drain();
            window.Draw();

            Assert.IsType<AccountScreen>(shell.Screen);

            // And the account holds posts of its own, so it is a third page to be left and put back.
            WalkDown(window);

            var account = content.Top;

            Assert.NotEqual(0, account);

            window.NewKeyDownEvent(Key.Enter);
            built.Host.Drain();
            window.Draw();

            Assert.IsType<PostScreen>(shell.Screen);

            window.NewKeyDownEvent(Key.Esc);
            window.Draw();

            Assert.IsType<AccountScreen>(shell.Screen);
            Assert.Equal(account, content.Top);

            window.NewKeyDownEvent(Key.Esc);
            window.Draw();

            Assert.IsType<PostScreen>(shell.Screen);
            Assert.Equal(thread, content.Top);

            window.NewKeyDownEvent(Key.Esc);
            window.Draw();

            Assert.IsType<FeedScreen>(shell.Screen);
            Assert.Equal(feed, content.Top);
        }
    }

    /// <summary>
    ///     Nothing about this is a feed's: the page is put back on whatever screen was drilled off, and whatever was
    ///     pushed over it. Here that is the inbox with the keymap over it, which is the pair that share no code with
    ///     the pair above — a screen holding notifications rather than posts, and a screen holding nothing but prose.
    /// </summary>
    [Fact]
    public async Task Esc_PutsThePageBackOnAScreenThatIsNotAFeed()
    {
        var (window, shell, built) = await Laid();

        using (window)
        {
            var content = Content(window);

            window.Draw();

            // Round the rail to the inbox, which is a destination like any other and several presses away. Bounded by
            // the rail's own length, so a rail that stopped holding an inbox fails here rather than spinning.
            for (var pressed = 0;
                 pressed < shell.Rail.Destinations.Count && shell.Screen is not NotificationsScreen;
                 pressed++)
            {
                window.NewKeyDownEvent(Key.Tab);
                built.Host.Settle();
                built.Host.Drain();
                window.Draw();
            }

            Assert.IsType<NotificationsScreen>(shell.Screen);

            WalkDown(window);

            var left = content.Top;

            Assert.NotEqual(0, left);

            window.NewKeyDownEvent(new Key('?'));
            window.Draw();

            Assert.IsType<HelpScreen>(shell.Screen);

            window.NewKeyDownEvent(Key.Esc);
            window.Draw();

            Assert.IsType<NotificationsScreen>(shell.Screen);
            Assert.Equal(left, content.Top);
        }
    }

    /// <summary>
    ///     A page that no longer fits the rows there are now is clamped rather than left past the end of them. Here
    ///     the reader deletes the post they drilled into, which takes it off the feed underneath while they are away —
    ///     the one case where a remembered offset goes stale without the terminal being touched.
    /// </summary>
    /// <remarks>
    ///     Tolerated rather than handled, which the clamp already in the draw is the whole of: the page comes back
    ///     wrong by at most the height of what went, and <c>j</c> reclaims. Still strictly closer than starting again.
    /// </remarks>
    [Fact]
    public async Task Esc_ClampsAPageThatNoLongerFitsTheRowsThereAreNow()
    {
        var (window, shell, built) = await Laid();

        using (window)
        {
            var content = Content(window);

            window.Draw();

            // The reader's own post is the last one on the feed, and the page is walked to the very foot of the list.
            window.NewKeyDownEvent(Key.End);
            window.Draw();

            for (var walked = 0; walked < 10; walked++)
            {
                window.NewKeyDownEvent(Key.CursorDown);
                window.Draw();
            }

            var left = content.Top;

            Assert.Equal(Rows(shell, content, built).Count - 1, left);

            window.NewKeyDownEvent(Key.Enter);
            built.Host.Drain();
            window.Draw();

            Assert.IsType<PostScreen>(shell.Screen);

            // Deleted from inside the thread, which walks out of the screen it was about and takes the post off the
            // feed the reader is walked back to.
            window.NewKeyDownEvent(Key.D);
            window.Draw();

            Assert.NotNull(shell.Asking);

            window.NewKeyDownEvent(Key.Y);
            built.Host.Drain();
            window.Draw();

            var feed = Assert.IsType<FeedScreen>(shell.Screen);

            Assert.DoesNotContain(feed.Posts, post => post.Id == "210");
            Assert.Equal(Rows(shell, content, built).Count - 1, content.Top);
            Assert.True(content.Top < left);
        }
    }

    /// <summary>
    ///     A screen being <em>pushed</em> still opens at the top: the rows are somebody else's, and an offset made on
    ///     the feed says nothing about a thread.
    /// </summary>
    [Fact]
    public async Task Enter_StillOpensThePushedScreenAtTheTop()
    {
        var (window, shell, built) = await Laid();

        using (window)
        {
            var content = Content(window);

            window.Draw();

            WalkDown(window);

            Assert.NotEqual(0, content.Top);

            window.NewKeyDownEvent(Key.Enter);
            built.Host.Drain();
            window.Draw();

            Assert.IsType<PostScreen>(shell.Screen);

            // The post this thread is around has no ancestors, so following the pick leaves the page at its first row.
            Assert.Equal(0, content.Top);
        }
    }

    /// <summary>
    ///     And arriving at a destination still opens at the top, including arriving back at the one just left: an
    ///     arrival builds a screen, and a screen nobody has read yet remembers nothing.
    /// </summary>
    [Fact]
    public async Task Arriving_StillOpensAtTheTop()
    {
        var (window, shell, built) = await Laid();

        using (window)
        {
            var content = Content(window);

            window.Draw();

            WalkDown(window);

            var feed = shell.Screen;

            Assert.NotEqual(0, content.Top);

            // Away to the next destination on the rail and back again, which is two arrivals. Settled first: a run of
            // rail presses is one fetch, and nothing is asked for until the pressing stops (ADR-0014).
            window.NewKeyDownEvent(Key.Tab);
            built.Host.Settle();
            built.Host.Drain();
            window.Draw();

            Assert.NotSame(feed, shell.Screen);
            Assert.Equal(0, content.Top);

            window.NewKeyDownEvent(Key.Tab.WithShift);
            built.Host.Settle();
            built.Host.Drain();
            window.Draw();

            // Home again, which is a destination arrived at rather than a screen walked back to: a fresh screen, at
            // the top, whatever the last one was showing.
            Assert.NotSame(feed, shell.Screen);
            Assert.Equal(0, content.Top);
        }
    }

    /// <summary>
    ///     Walks the pick well down the feed and then one post back up, which leaves the page a long way down the list
    ///     with the picked post drawn part way <em>up</em> it.
    /// </summary>
    /// <remarks>
    ///     The step back is what makes these tests say anything. Walking straight down leaves the pick against the
    ///     bottom of the page, which is the very row a page starting again at nought scrolls to on its first frame —
    ///     so a screen that had forgotten where it was would land on the same number and pass. A post back up is a
    ///     page only the reader's own offset explains.
    ///     <para>
    ///         A frame per press, which is what a terminal does: between two presses with no draw the region's offset
    ///         is the one the last frame settled, so <c>k</c> sees a selection it thinks has scrolled off and reclaims
    ///         the topmost post instead of stepping.
    ///     </para>
    /// </remarks>
    private static void WalkDown(ShellWindow window)
    {
        for (var walked = 0; walked < 8; walked++)
        {
            window.NewKeyDownEvent(Key.K);
            window.Draw();
        }

        window.NewKeyDownEvent(Key.J);
        window.Draw();
    }

    /// <summary>The content region, which is the one that scrolls and so the one with a page to put back.</summary>
    private static PaintedView Content(ShellWindow window) =>
        window.SubViews.OfType<PaintedView>().Single(view => view.Id == ShellWindow.ContentId);

    /// <summary>
    ///     The rows the screen on top is drawing, at the width the region is drawing them, so that a test can say
    ///     "the last row there is" rather than a number that goes stale the moment a byline changes.
    /// </summary>
    private static IReadOnlyList<Line> Rows(Wooly.Tui.Shell.Shell shell, PaintedView content, AShell built) =>
        shell.Screen.Lines(content.Viewport.Width, built.Clock.GetUtcNow());

    /// <summary>
    ///     A shell over a feed long enough to scroll, on a window with room for eighteen rows — and a thread with
    ///     replies enough that the post screen scrolls too, since one of these tests walks two levels down.
    /// </summary>
    private static async Task<(ShellWindow Window, Wooly.Tui.Shell.Shell Shell, AShell Built)> Laid()
    {
        var built = new AShell
        {
            // Twenty of somebody else's and then one of the reader's own, which is the one that can be deleted.
            Timelines = FakeTimelineReader.Holding(
                [
                    .. Enumerable.Range(1, 20).Select(at => APost.With(id: $"{at}0", account: "ben@hachyderm.io")),
                    APost.With(id: "210"),
                ]),

            Engagement = FakePostEngagement.Answered(
                APost.With(id: "110"),
                APost.With(id: "111"),
                APost.With(id: "222"),
                APost.With(id: "333"),
                APost.With(id: "444"),
                APost.With(id: "555")),

            Accounts = FakeAccountRelationships.Holding(AnAccount.With(address: "ben@hachyderm.io")),

            Notifications = FakeNotificationInbox.Holding(
                [.. Enumerable.Range(1, 20).Select(at => ANotification.With(id: $"{at}0"))]),
        };

        var shell = await built.Opened();

        var window = new ShellWindow(shell, Themes.Plain, built.Clock, () => { }, FakePictures.DrawingNothing())
        {
            Width = 80,
            Height = 20,
        };

        window.Layout();

        return (window, shell, built);
    }
}
