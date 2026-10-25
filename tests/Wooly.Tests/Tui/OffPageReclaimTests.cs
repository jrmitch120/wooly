using System.Drawing;
using Terminal.Gui.Input;
using Wooly.Core.Paging;
using Wooly.Core.Posts;
using Wooly.Core.Timelines;
using Wooly.Tests.Fakes;
using Wooly.Tui.Shell;
using Wooly.Tui.Theme;

namespace Wooly.Tests.Tui;

/// <summary>
///     What <c>j</c>, <c>k</c>, <c>[</c> and <c>]</c> pick once the pick has been scrolled off the page: the first thing
///     whose top is on it, falling back to the topmost thing with any row showing where no thing begins there (#472).
///     Pressed and wheeled on the whole shell drawn headless, and read back off what is picked.
/// </summary>
public class OffPageReclaimTests
{
    /// <summary>Short enough that six posts run well past the foot of the page.</summary>
    private const int Short = 16;

    /// <summary>Whoever the account screen is about in these tests.</summary>
    private const string Whose = "ben@hachyderm.io";

    /// <summary>A column inside the content panel, clear of the pick's gutter.</summary>
    private const int OverContent = ContentClicks.OverContent;

    /// <summary>A row inside the content panel, below its top edge.</summary>
    private const int Inside = 3;

    /// <summary>
    ///     With the foot of a post filling the top of the page, both keys pick the next post down, the first one the
    ///     reader can see beginning, rather than the half-gone one above it.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task APostsFootAtTheTopOfThePageIsPassedOver(bool next)
    {
        using var drawn = await Feed(Six());

        LeaveThePickBelow(drawn);
        TopOffBy(drawn, item: 1, rows: 1);

        drawn.Press(next ? Key.K : Key.J);

        Assert.Equal("330", drawn.Shell.Screen.Picked?.Id);
    }

    /// <summary>
    ///     A boosted post begins at its boost line: with that line on the page the post counts as begun, and with
    ///     only the line gone, byline and all still showing, it does not.
    /// </summary>
    [Theory]
    [InlineData(0, "220")]
    [InlineData(1, "330")]
    public async Task ABoostLineIsTheTopOfTheBoostedPost(int scrolledOff, string picked)
    {
        var posts = Six();
        posts[1] = APost.With(id: "220", boosted: APost.With(id: "221", content: "Shorn"));

        using var drawn = await Feed(posts);

        LeaveThePickBelow(drawn);
        TopOffBy(drawn, item: 1, rows: scrolledOff);

        drawn.Press(Key.K);

        Assert.Equal(picked, drawn.Shell.Screen.Picked?.Id);
    }

    /// <summary>A notification begins at its strip, the same way on its screen as a post does on a feed.</summary>
    [Theory]
    [InlineData(0, "220")]
    [InlineData(1, "330")]
    public async Task ANotificationsStripIsItsTop(int scrolledOff, string picked)
    {
        var built = new AShell
        {
            Timelines = FakeTimelineReader.Holding(APost.With(id: "1")),
            Notifications = FakeNotificationInbox.Holding(
                [
                    .. new[] { "110", "220", "330", "440", "550", "660" }.Select(
                        (id, at) => ANotification.With(id: $"{at + 1}", post: APost.With(id: id))),
                ]),
        };

        using var drawn = await DrawnShell.Of(80, Short, Themes.Plain, built);

        drawn.Shell.Rail.GoTo(DestinationKind.Notifications);
        built.Host.Drain();
        drawn.Redraw();

        LeaveThePickBelow(drawn);
        TopOffBy(drawn, item: 1, rows: scrolledOff);

        drawn.Press(Key.K);

        Assert.Equal(picked, drawn.Shell.Screen.Picked?.Id);
    }

    /// <summary>
    ///     An account screen's header block is a thing with a top like any other: scrolled back up to it, the press
    ///     picks it, and with its first row gone, the first post under it.
    /// </summary>
    [Fact]
    public async Task AnAccountsHeaderBlockHasATop()
    {
        using var drawn = await Account(pinned: [], posts: [.. Six().Select(post => post with { Account = Whose })]);

        LeaveThePickBelow(drawn);
        TopOffBy(drawn, item: 0, rows: 0);

        drawn.Press(Key.K);

        Assert.Null(drawn.Shell.Screen.Picked);

        LeaveThePickBelow(drawn);
        TopOffBy(drawn, item: 0, rows: 1);

        drawn.Press(Key.K);

        Assert.Equal("110", drawn.Shell.Screen.Picked?.Id);
    }

    /// <summary>
    ///     A post taller than the page filling it, its top scrolled off and no other thing beginning on the page, is
    ///     the post being read, and both keys pick it: <c>↓ ↓ ↓ j</c> on a long post keeps the reader where they are.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task APostFillingThePageIsPickedWhereNothingBeginsOnIt(bool next)
    {
        var tall = string.Concat(Enumerable.Range(1, 40).Select(line => $"<p>Line {line} of the fleece</p>"));

        using var drawn = await Feed([APost.With(id: "110"), APost.With(id: "220", content: tall), APost.With(id: "330")]);

        LeaveThePickBelow(drawn);
        TopOffBy(drawn, item: 1, rows: 5);

        drawn.Press(next ? Key.K : Key.J);

        Assert.Equal("220", drawn.Shell.Screen.Picked?.Id);
    }

    /// <summary>A page scrolled past the last thing, with the pick left at the top, takes that last thing.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task PastTheLastThingThePressPicksIt(bool next)
    {
        using var drawn = await Feed(Six());

        // The pick is the first post, and the wheel runs the page as far down as it goes.
        for (var notch = 0; notch < 100; notch++)
        {
            drawn.Wheel(OverContent, Inside);
        }

        Assert.Null(drawn.Content.ItemAt(new Point(OverContent, Short - 2)));

        drawn.Press(next ? Key.K : Key.J);

        Assert.Equal("660", drawn.Shell.Screen.Picked?.Id);
    }

    /// <summary>With only the foot of the pick still on the page it is not off the page, and the keys move on from it.</summary>
    [Theory]
    [InlineData(true, "330")]
    [InlineData(false, "110")]
    public async Task APickWithItsFootShowingIsMovedFrom(bool next, string picked)
    {
        using var drawn = await Feed(Six());

        drawn.Press(Key.K);
        TopOffBy(drawn, item: 1, rows: 1);

        drawn.Press(next ? Key.K : Key.J);

        Assert.Equal(picked, drawn.Shell.Screen.Picked?.Id);
    }

    /// <summary>The reclaiming press does nothing else, and the one after it moves on from what it reclaimed.</summary>
    [Fact]
    public async Task ThePressAfterTheReclaimMovesOnFromIt()
    {
        using var drawn = await Feed(Six());

        LeaveThePickBelow(drawn);
        TopOffBy(drawn, item: 1, rows: 1);

        drawn.Press(Key.J);
        drawn.Press(Key.K);

        Assert.Equal("440", drawn.Shell.Screen.Picked?.Id);
    }

    /// <summary>
    ///     <c>[</c> and <c>]</c> jump from the thing the same rule reclaims: on an account scrolled so that the foot of
    ///     the last pinned post fills the top of the page and their first post begins below it, <c>[</c> goes back to
    ///     the first pinned post, where from the half-gone pinned one it would have had nowhere to go but the header.
    /// </summary>
    [Fact]
    public async Task TheBracketsJumpFromWhatIsReclaimed()
    {
        using var drawn = await Account(
            pinned: [APost.With(id: "110", account: Whose), APost.With(id: "220", account: Whose)],
            posts: [APost.With(id: "330", account: Whose), APost.With(id: "440", account: Whose), APost.With(id: "550", account: Whose)]);

        LeaveThePickBelow(drawn);
        TopOffBy(drawn, item: 2, rows: 1);

        drawn.Press(new Key('['));

        Assert.Equal("110", drawn.Shell.Screen.Picked?.Id);
    }

    /// <summary>Six posts, from 110 up to 660.</summary>
    private static Post[] Six() =>
        [.. new[] { "110", "220", "330", "440", "550", "660" }.Select(id => APost.With(id: id))];

    /// <summary>The shell drawn short on an account screen, with <paramref name="pinned" /> above <paramref name="posts" />.</summary>
    private static async Task<DrawnShell> Account(IReadOnlyList<Post> pinned, IReadOnlyList<Post> posts)
    {
        var built = new AShell
        {
            Timelines = FakeTimelineReader.Answering(timeline => timeline.Scope switch
            {
                TimelineScope.Pinned => Fetch<Post>.Complete(pinned),
                TimelineScope.Account => Fetch<Post>.Complete(posts),
                _ => Fetch<Post>.Complete([APost.With(id: "1", account: Whose)]),
            }),
            Accounts = FakeAccountRelationships.Holding(AnAccount.With(address: Whose)),
        };

        var drawn = await DrawnShell.Of(80, Short, Themes.Plain, built);

        await drawn.Shell.OpenAuthor();
        built.Host.Drain();
        drawn.Redraw();

        return drawn;
    }

    /// <summary>The shell drawn short over a feed of <paramref name="posts" />.</summary>
    private static Task<DrawnShell> Feed(Post[] posts) =>
        DrawnShell.Of(80, Short, Themes.Plain, new AShell { Timelines = FakeTimelineReader.Holding(posts) });

    /// <summary>The last thing picked, which brings the page down to it, well below where these tests read.</summary>
    private static void LeaveThePickBelow(DrawnShell drawn) => drawn.Press(Key.End);

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

        // A notch at a time, so the first page beginning on the thing is the one beginning on its top.
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
