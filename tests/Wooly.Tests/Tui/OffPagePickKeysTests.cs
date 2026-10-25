using System.Drawing;
using Terminal.Gui.Input;
using Wooly.Core.Notifications;
using Wooly.Core.Posts;
using Wooly.Tests.Fakes;
using Wooly.Tui.Screens;
using Wooly.Tui.Shell;
using Wooly.Tui.Theme;
using Wooly.Tui.Views;

namespace Wooly.Tests.Tui;

/// <summary>
///     The keys that act on the pick, pressed once the pick has been scrolled off the page: the first press only
///     reclaims, by the rule <c>j</c> and <c>k</c> reclaim by, and the next acts on what it reclaimed (#476). Pressed
///     and wheeled on the whole shell drawn headless, and read back off what is picked and what the fakes were asked.
/// </summary>
public class OffPagePickKeysTests
{
    /// <summary>Short enough that six posts run well past the foot of the page.</summary>
    private const int Short = 16;

    /// <summary>A column inside the content panel, clear of the pick's gutter.</summary>
    private const int OverContent = ContentClicks.OverContent;

    /// <summary>A row inside the content panel, below its top edge.</summary>
    private const int Inside = 3;

    /// <summary>The post whose top is the page's first row once the pick is left off it.</summary>
    private const string Reclaimed = "220";

    /// <summary>The first <c>⏎</c> picks the post on the page and opens nothing; the second reads it.</summary>
    [Fact]
    public async Task TheFirstEnterReclaimsAndTheSecondReads()
    {
        using var drawn = await Feed();

        drawn.Press(Key.Enter);
        drawn.Built.Host.Drain();

        Assert.Equal(1, drawn.Shell.Depth);
        Assert.Equal(Reclaimed, drawn.Shell.Screen.Picked?.Id);

        drawn.Press(Key.Enter);
        drawn.Built.Host.Drain();

        Assert.Equal(Reclaimed, Assert.IsType<PostScreen>(drawn.Shell.Screen).Post.Id);
    }

    /// <summary>The first <c>f</c> or <c>b</c> marks nothing; the second marks the post it reclaimed.</summary>
    [Theory]
    [InlineData('f', PostMark.Favorite)]
    [InlineData('b', PostMark.Boost)]
    public async Task TheFirstMarkReclaimsAndTheSecondMarksIt(char key, PostMark mark)
    {
        using var drawn = await Feed();

        drawn.Press(new Key(key));
        drawn.Built.Host.Drain();

        Assert.Empty(drawn.Built.Engagement.Marks);
        Assert.Equal(Reclaimed, drawn.Shell.Screen.Picked?.Id);

        drawn.Press(new Key(key));
        drawn.Built.Host.Drain();

        var marked = Assert.Single(drawn.Built.Engagement.Marks);
        Assert.Equal(Reclaimed, marked.PostId);
        Assert.Equal(mark, marked.Mark);
    }

    /// <summary>The first <c>r</c> opens no reply; the second replies to the post it reclaimed.</summary>
    [Fact]
    public async Task TheFirstReplyReclaimsAndTheSecondReplies()
    {
        using var drawn = await Feed();

        drawn.Press(new Key('r'));

        Assert.IsNotType<ComposeScreen>(drawn.Shell.Screen);
        Assert.Equal(Reclaimed, drawn.Shell.Screen.Picked?.Id);

        drawn.Press(new Key('r'));

        Assert.Equal(Reclaimed, Assert.IsType<ComposeScreen>(drawn.Shell.Screen).About?.Id);
    }

    /// <summary>
    ///     Every key the status row offers that needs a pick, on a feed and on notifications alike, only reclaims on the
    ///     first press: nothing is asked of the instance, nothing is opened and nothing is asked of the reader.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EveryKeyThatNeedsAPickOnlyReclaims(bool notifications)
    {
        var marked = await MarkedKeys(notifications);

        Assert.Superset(new HashSet<string>(["⏎", notifications ? "d" : "f"]), marked.ToHashSet());

        foreach (var key in marked)
        {
            using var drawn = notifications ? await Notifications() : await Feed();
            var screen = drawn.Shell.Screen;
            var requests = drawn.Built.Requests;

            drawn.Press(key == "⏎" ? Key.Enter : new Key(key[0]));
            drawn.Built.Host.Drain();

            Assert.Same(screen, drawn.Shell.Screen);
            Assert.Equal(Reclaimed, drawn.Shell.Screen.Picked?.Id);
            Assert.Equal(requests, drawn.Built.Requests);
            Assert.Null(drawn.Shell.Asking);
            Assert.Null(drawn.Shell.StatusRow.Notice);
        }
    }

    /// <summary>
    ///     A digit and <c>v</c> act on the picked post's poll, so the first press of either only reclaims: no answer is
    ///     toggled on the poll off the page, and no vote is asked about.
    /// </summary>
    [Theory]
    [InlineData('1')]
    [InlineData('0')]
    [InlineData('v')]
    public async Task TheFirstVotingKeyOnlyReclaims(char key)
    {
        using var drawn = await Feed([.. Six().Select(post => post with { Poll = APost.APoll() })]);

        drawn.Press(new Key(key));
        drawn.Built.Host.Drain();

        Assert.Equal(Reclaimed, drawn.Shell.Screen.Picked?.Id);
        Assert.Null(drawn.Shell.Asking);
        Assert.Null(drawn.Shell.StatusRow.Notice);
    }

    /// <summary>
    ///     <c>d</c>, <c>p</c> and <c>e</c> on somebody else's post, and <c>x</c> on one with nothing hidden, are off the
    ///     status row; pressed with the pick off the page they still only reclaim, rather than being refused about a post
    ///     the reader cannot see.
    /// </summary>
    [Theory]
    [InlineData('d')]
    [InlineData('p')]
    [InlineData('e')]
    [InlineData('x')]
    public async Task AKeyOffTheRowStillOnlyReclaims(char key)
    {
        using var drawn = await Feed();
        var screen = drawn.Shell.Screen;

        drawn.Press(new Key(key));
        drawn.Built.Host.Drain();

        Assert.Same(screen, drawn.Shell.Screen);
        Assert.Equal(Reclaimed, drawn.Shell.Screen.Picked?.Id);
        Assert.Null(drawn.Shell.Asking);
        Assert.Null(drawn.Shell.StatusRow.Notice);
    }

    /// <summary>
    ///     With a follow notification picked, which carries no post, the post keys are off the row; pressed once it is off
    ///     the page, <c>f</c> reclaims the post on the page and marks nothing, and the next <c>f</c> marks that post.
    /// </summary>
    [Fact]
    public async Task APostKeyReclaimsFromAThingWithNoPost()
    {
        using var drawn = await Notifications(
        [
            .. Six().Take(5).Select((post, at) => ANotification.With(id: $"{at + 1}", post: post)),
            ANotification.Follow(id: "6"),
        ]);

        drawn.Press(new Key('f'));
        drawn.Built.Host.Drain();

        Assert.Empty(drawn.Built.Engagement.Marks);
        Assert.Equal(Reclaimed, drawn.Shell.Screen.Picked?.Id);

        drawn.Press(new Key('f'));
        drawn.Built.Host.Drain();

        Assert.Equal(Reclaimed, Assert.Single(drawn.Built.Engagement.Marks).PostId);
    }

    /// <summary>
    ///     A question open on the status row takes the next key before anything reclaims: <c>f</c> declines it, and
    ///     neither reclaims nor marks.
    /// </summary>
    [Fact]
    public async Task AnOpenQuestionTakesTheKeyBeforeAnyReclaim()
    {
        using var drawn = await Feed();
        var agreed = false;

        drawn.Shell.StatusRow.Ask(new Confirmation("Go ahead?", () => Task.FromResult(agreed = true)));

        drawn.Press(new Key('f'));
        drawn.Built.Host.Drain();

        Assert.Null(drawn.Shell.Asking);
        Assert.False(agreed);
        Assert.Empty(drawn.Built.Engagement.Marks);
        Assert.Equal("660", drawn.Shell.Screen.Picked?.Id);
    }

    /// <summary>With the foot of the pick still on the page, <c>f</c> favourites it at once.</summary>
    [Fact]
    public async Task APickWithItsFootShowingIsActedOnAtOnce()
    {
        using var drawn = await DrawnShell.Of(80, Short, Themes.Plain, new AShell { Timelines = FakeTimelineReader.Holding(Six()) });

        drawn.Press(Key.K);
        TopOffBy(drawn, item: 1, rows: 1);

        drawn.Press(new Key('f'));
        drawn.Built.Host.Drain();

        Assert.Equal("220", Assert.Single(drawn.Built.Engagement.Marks).PostId);
    }

    /// <summary><c>c</c> writes a fresh post with the pick off the page, reclaiming nothing.</summary>
    [Fact]
    public async Task ComposeIsUnaffected()
    {
        using var drawn = await Feed();

        drawn.Press(new Key('c'));

        Assert.Null(Assert.IsType<ComposeScreen>(drawn.Shell.Screen).About);
    }

    /// <summary><c>?</c> opens the keys with the pick off the page, reclaiming nothing.</summary>
    [Fact]
    public async Task HelpIsUnaffected()
    {
        using var drawn = await Feed();

        drawn.Press(new Key('?'));
        drawn.Press(Key.Esc);

        Assert.Equal("660", drawn.Shell.Screen.Picked?.Id);
    }

    /// <summary><c>g</c>, <c>esc</c> and <c>tab</c> reclaim nothing either.</summary>
    [Fact]
    public async Task TheFrameKeysAreUnaffected()
    {
        using var drawn = await Feed();

        drawn.Press(Key.Esc);
        drawn.Press(Key.Tab);

        Assert.Equal("660", drawn.Shell.Screen.Picked?.Id);
        Assert.Equal(1, drawn.Shell.Rail.Cursor);

        drawn.Press(new Key('g'));

        Assert.NotEqual(Reclaimed, drawn.Shell.Screen.Picked?.Id);
    }

    /// <summary>The status row is the same with the pick on the page and scrolled off it.</summary>
    [Fact]
    public async Task TheStatusRowStaysStillAsThePageMoves()
    {
        using var drawn = await DrawnShell.Of(80, Short, Themes.Plain, new AShell { Timelines = FakeTimelineReader.Holding(Six()) });

        drawn.Press(Key.End);
        var picked = drawn.Rows()[^1];

        TopOffBy(drawn, item: 1, rows: 0);

        Assert.Equal(picked, drawn.Rows()[^1]);
    }

    /// <summary>The help screen's <i>Everywhere</i> list says that the first key picks what is on the page.</summary>
    [Fact]
    public async Task HelpSaysTheFirstKeyPicksWhatIsOnThePage()
    {
        using var drawn = await DrawnShell.Of(80, 80, Themes.Plain);

        drawn.Press(new Key('?'));

        Assert.Contains(
            drawn.Rows(),
            row => row.Contains("off-page pick   the first key picks what is on the page", StringComparison.Ordinal));
    }

    /// <summary>
    ///     The keys the status row offers on a feed or on notifications that mean something there needing a pick
    ///     (<see cref="Keymap.NeedsAPick" />).
    /// </summary>
    private static async Task<IReadOnlyList<string>> MarkedKeys(bool notifications)
    {
        using var drawn = notifications ? await Notifications() : await Feed();

        return
        [
            .. drawn.Shell.Keys
                .Select(hint => hint.Key)
                .Where(key => key == "⏎" || key.Length == 1)
                .Where(key => ShellKeys.Of(key == "⏎" ? Key.Enter : new Key(key[0])) is { } pressed
                              && Keymap.NeedsAPick(Keymap.Means(pressed, drawn.Shell.Screen), drawn.Shell.Screen))
                .Distinct(),
        ];
    }

    /// <summary>Six posts, from 110 up to 660.</summary>
    private static Post[] Six() =>
        [.. new[] { "110", "220", "330", "440", "550", "660" }.Select(id => APost.With(id: id))];

    /// <summary>The shell drawn short over a feed, the last post picked and the second post's top on the page's first row.</summary>
    private static async Task<DrawnShell> Feed() => await Feed(Six());

    /// <summary>The same over <paramref name="posts" />.</summary>
    private static async Task<DrawnShell> Feed(Post[] posts)
    {
        var drawn = await DrawnShell.Of(80, Short, Themes.Plain, new AShell { Timelines = FakeTimelineReader.Holding(posts) });

        LeaveThePickOff(drawn);

        return drawn;
    }

    /// <summary>The same on notifications, one notification on each of the six posts.</summary>
    private static Task<DrawnShell> Notifications() =>
        Notifications([.. Six().Select((post, at) => ANotification.With(id: $"{at + 1}", post: post))]);

    /// <summary>The same over <paramref name="notifications" />.</summary>
    private static async Task<DrawnShell> Notifications(Notification[] notifications)
    {
        var built = new AShell
        {
            Timelines = FakeTimelineReader.Holding(APost.With(id: "1")),
            Notifications = FakeNotificationInbox.Holding(notifications),
        };

        var drawn = await DrawnShell.Of(80, Short, Themes.Plain, built);

        drawn.Shell.Rail.GoTo(DestinationKind.Notifications);
        built.Host.Drain();
        drawn.Redraw();

        LeaveThePickOff(drawn);

        return drawn;
    }

    /// <summary>The last thing picked, and the page wheeled back up to the second thing's top.</summary>
    private static void LeaveThePickOff(DrawnShell drawn)
    {
        drawn.Press(Key.End);
        TopOffBy(drawn, item: 1, rows: 0);
    }

    /// <summary>
    ///     The page wheeled to where the <paramref name="item" />th thing's top is <paramref name="rows" /> rows above
    ///     its first row: nought for that top on the page's first row.
    /// </summary>
    private static void TopOffBy(DrawnShell drawn, int item, int rows)
    {
        while (drawn.Content.Top > 0)
        {
            drawn.Wheel(OverContent, Inside, down: false);
        }

        var first = new Point(OverContent, drawn.Content.ViewportToScreen(Point.Empty).Y);

        for (var notch = 0; drawn.Content.ItemAt(first) != item; notch++)
        {
            Assert.True(notch < 100, $"No page begins on thing {item}:\n{string.Join('\n', drawn.Rows())}");

            drawn.Wheel(OverContent, Inside);
        }

        for (var notch = 0; notch < rows; notch++)
        {
            drawn.Wheel(OverContent, Inside);
        }
    }
}
