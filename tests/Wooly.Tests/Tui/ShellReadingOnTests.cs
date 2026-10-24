using Wooly.Core.Posts;
using Wooly.Core.Timelines;
using Wooly.Tests.Fakes;
using Wooly.Tui.Rendering;
using Wooly.Tui.Screens;
using Wooly.Tui.Shell;

namespace Wooly.Tests.Tui;

/// <summary>
///     Reading on, on Home (#469): as the page nears the end of the posts held, the shell asks the instance for the
///     older posts behind them and adds them below. Where the page is comes from the window, which is the only thing
///     that knows (<see cref="Shell.Viewed" />); what the shell does about it is what these say.
/// </summary>
public class ShellReadingOnTests
{
    /// <summary>The height of the content region these tests say the page is, in rows.</summary>
    private const int Screenful = 20;

    /// <summary>
    ///     The page within a screenful of the end of Home: the posts behind the last one held are asked for and put
    ///     under it, in the instance's order, with the pick left on the post it was on.
    /// </summary>
    [Fact]
    public async Task Viewed_WithinAScreenfulOfTheEnd_PutsTheOlderPostsUnderThoseHeld()
    {
        var built = Home(posts: 100);
        var opened = await built.Opened();

        opened.Walk(3, reclaiming: null);
        opened.Viewed(under: Screenful, screenful: Screenful);
        built.Host.Drain();

        var feed = Assert.IsType<FeedScreen>(opened.Screen);
        Assert.Single(built.Timelines.ReadsOn);
        Assert.Equal(Ids(1, 80), feed.Posts.Select(post => post.Id));
        Assert.Equal("4", feed.Picked?.Id);
    }

    /// <summary>And it goes on from there the next time, rather than asking again for what it already has.</summary>
    [Fact]
    public async Task Viewed_NearingTheEndAgain_ReadsOnFromWhereTheLastReadOnStopped()
    {
        var built = Home(posts: 100);
        var opened = await built.Opened();

        opened.Viewed(under: 0, screenful: Screenful);
        built.Host.Drain();
        opened.Viewed(under: 0, screenful: Screenful);
        built.Host.Drain();

        Assert.Equal(Ids(1, 100), Assert.IsType<FeedScreen>(opened.Screen).Posts.Select(post => post.Id));
    }

    /// <summary>Further than a screenful from the end, nothing is asked: a list nobody walks to the end of is never read on.</summary>
    [Fact]
    public async Task Viewed_FurtherThanAScreenfulFromTheEnd_AsksNothing()
    {
        var built = Home(posts: 100);
        var opened = await built.Opened();

        opened.Viewed(under: Screenful + 1, screenful: Screenful);
        built.Host.Drain();

        Assert.Empty(built.Timelines.ReadsOn);
        Assert.Equal(40, Assert.IsType<FeedScreen>(opened.Screen).Posts.Count);
    }

    /// <summary>One read in flight per screen: walking about near the end while it is out asks for nothing more.</summary>
    [Fact]
    public async Task Viewed_WhileAReadOnIsInFlight_AsksForNoSecond()
    {
        var built = Home(posts: 100);
        var opened = await built.Opened();

        built.Timelines.HoldReadsOn();
        opened.Viewed(under: 0, screenful: Screenful);
        built.Host.Drain();
        opened.Viewed(under: 0, screenful: Screenful);
        built.Host.Drain();

        Assert.Single(built.Timelines.ReadsOn);

        built.Timelines.LetReadsOnGo();
        built.Host.Drain();

        Assert.Equal(80, Assert.IsType<FeedScreen>(opened.Screen).Posts.Count);
    }

    /// <summary>The foot of the list says a read is in flight, dimly, under the last post held.</summary>
    [Fact]
    public async Task Foot_SaysReadingOnWhileAReadIsInFlight()
    {
        var built = Home(posts: 100);
        var opened = await built.Opened();

        built.Timelines.HoldReadsOn();
        opened.Viewed(under: 0, screenful: Screenful);
        built.Host.Drain();

        Assert.Equal("reading on…", Foot(opened.Screen));
    }

    /// <summary>Between reads, with more to come, the foot says nothing at all.</summary>
    [Fact]
    public async Task Foot_SaysNothingBetweenReads()
    {
        var built = Home(posts: 100);
        var opened = await built.Opened();

        var drawn = Rows(opened.Screen);

        Assert.DoesNotContain("reading on…", drawn);
        Assert.DoesNotContain("that's everything", drawn);
    }

    /// <summary>
    ///     Once the instance has nothing behind what is held, the list is whole: the foot says so, and nothing more is
    ///     asked of it however near the end the page is.
    /// </summary>
    [Fact]
    public async Task Foot_SaysThatsEverythingOnceTheListIsWhole_AndNothingMoreIsAsked()
    {
        var built = Home(posts: 50);
        var opened = await built.Opened();

        opened.Viewed(under: 0, screenful: Screenful);
        built.Host.Drain();
        opened.Viewed(under: 0, screenful: Screenful);
        built.Host.Drain();

        Assert.Single(built.Timelines.ReadsOn);
        Assert.Equal("that's everything", Foot(opened.Screen));
    }

    /// <summary>A first read that already held everything is whole straight away, and says so before anybody walks.</summary>
    [Fact]
    public async Task Foot_SaysThatsEverythingWhereTheFirstReadHeldItAll()
    {
        var built = Home(posts: 3);
        var opened = await built.Opened();

        Assert.Equal("that's everything", Foot(opened.Screen));
    }

    /// <summary>
    ///     Drilling in while a read is in flight drops its answer, under the stale rule every enquiry keeps: nothing
    ///     lands on a screen the reader is not looking at.
    /// </summary>
    [Fact]
    public async Task ReadOn_LandingAfterTheReaderDrilledIn_IsDropped()
    {
        var built = Home(posts: 100);
        var opened = await built.Opened();

        built.Timelines.HoldReadsOn();
        opened.Viewed(under: 0, screenful: Screenful);
        built.Host.Drain();
        opened.Press(ShellKey.Enter);
        built.Host.Drain();

        built.Timelines.LetReadsOnGo();
        built.Host.Drain();

        Assert.IsType<PostScreen>(opened.Screen);

        opened.Press(ShellKey.Escape);

        Assert.Equal(40, Assert.IsType<FeedScreen>(opened.Screen).Posts.Count);
    }

    /// <summary>
    ///     And walking back out with the page still near the end reads on again, from the same place — the dropped
    ///     read cost the reader nothing.
    /// </summary>
    [Fact]
    public async Task ReadOn_DroppedByADrill_ReadsOnAgainOnWalkingBackOut()
    {
        var built = Home(posts: 100);
        var opened = await built.Opened();

        built.Timelines.HoldReadsOn();
        opened.Viewed(under: 0, screenful: Screenful);
        built.Host.Drain();
        opened.Press(ShellKey.Enter);
        built.Host.Drain();
        built.Timelines.LetReadsOnGo();
        built.Host.Drain();

        opened.Press(ShellKey.Escape);
        opened.Viewed(under: 0, screenful: Screenful);
        built.Host.Drain();

        Assert.Equal(2, built.Timelines.ReadsOn.Count);
        Assert.Equal(built.Timelines.ReadsOn[0].From, built.Timelines.ReadsOn[1].From);
        Assert.Equal(Ids(1, 80), Assert.IsType<FeedScreen>(opened.Screen).Posts.Select(post => post.Id));
    }

    /// <summary>
    ///     A timeline that does not read on yet never asks, and draws no foot: Local is the same screen over a
    ///     different timeline, and reading on reaches it in a ticket of its own (#464).
    /// </summary>
    [Fact]
    public async Task Viewed_OnASubjectThatDoesNotReadOn_AsksNothing()
    {
        var built = Home(posts: 100);
        var opened = await built.Opened();

        opened.Step(1);
        built.Host.Settle();

        Assert.Equal("Local", opened.Screen.Crumb);

        opened.Viewed(under: 0, screenful: Screenful);
        built.Host.Drain();

        Assert.Empty(built.Timelines.ReadsOn);
        Assert.DoesNotContain("that's everything", Rows(opened.Screen));
    }

    /// <summary>Every row <paramref name="screen" /> draws, as text, the gutter and all.</summary>
    private static IReadOnlyList<string> Rows(Screen screen) =>
        [.. screen.Lines(new Drawing(61, AShell.Now)).Select(line => line.Text)];

    /// <summary>The last row <paramref name="screen" /> draws, which is the foot of its list where it has one.</summary>
    private static string Foot(Screen screen) => Rows(screen)[^1];

    /// <summary>Home holding <paramref name="posts" /> posts, newest first, ids counting up from 1, which reads on.</summary>
    private static AShell Home(int posts) =>
        new() { Timelines = FakeTimelineReader.ReadingOn([.. Ids(1, posts).Select(id => APost.With(id: id))]) };

    /// <summary>The ids from <paramref name="first" /> to <paramref name="last" />, in order.</summary>
    private static IEnumerable<string> Ids(int first, int last) =>
        Enumerable.Range(first, last - first + 1).Select(at => $"{at}");
}
