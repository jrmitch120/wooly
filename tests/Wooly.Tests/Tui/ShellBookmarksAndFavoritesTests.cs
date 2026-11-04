using Terminal.Gui.Input;
using Wooly.Core.Paging;
using Wooly.Core.Posts;
using Wooly.Core.Timelines;
using Wooly.Tests.Fakes;
using Wooly.Tui.Rendering;
using Wooly.Tui.Screens;
using Wooly.Tui.Shell;
using Wooly.Tui.Theme;
using static Wooly.Tests.Tui.RailHeights;
using static Wooly.Tests.Tui.RailPlaces;

namespace Wooly.Tests.Tui;

/// <summary>
///     The <b>Bookmarks</b> and <b>Favorites</b> runs as destinations in the You group (#563, ADR-0014's amendment):
///     each is arrived at, read on and refreshed as a timeline, carries no unread count, is clicked and gone to like
///     any destination, and is the acting profile's own. A mark taken off on its own run leaves the row where it is,
///     as on the Pinned run.
/// </summary>
public class ShellBookmarksAndFavoritesTests
{
    /// <summary>The height of the content region these tests say the page is, in rows.</summary>
    private const int Screenful = 20;

    /// <summary>
    ///     Arriving reads the run as the instance orders it, newest mark first — which is not newest post first, so the
    ///     screen draws what came back in the order it came.
    /// </summary>
    [Theory]
    [InlineData(DestinationKind.Bookmarks, "Bookmarks")]
    [InlineData(DestinationKind.Favorites, "Favorites")]
    public async Task Arriving_ReadsTheRunInTheOrderTheInstanceKeepsIt(DestinationKind kind, string label)
    {
        var shell = new AShell
        {
            Timelines = FakeTimelineReader.Answering(timeline =>
                Fetch<Post>.Complete(timeline == Run(kind)
                    ? [APost.With(id: "12"), APost.With(id: "340"), APost.With(id: "7")]
                    : [])),
        };
        var opened = await shell.Opened();

        opened.StepTo(kind);
        shell.Host.Settle();

        var feed = Assert.IsType<FeedScreen>(opened.Screen);
        Assert.Equal(Run(kind), shell.Timelines.Reads[^1].Timeline);
        Assert.Equal(label, opened.Breadcrumb);
        Assert.Equal(["12", "340", "7"], feed.Posts.Select(post => post.Id));
    }

    /// <summary>The page within a screenful of the end reads on that run, under the posts its first read brought.</summary>
    [Theory]
    [InlineData(DestinationKind.Bookmarks)]
    [InlineData(DestinationKind.Favorites)]
    public async Task Viewed_NearTheEndOfTheRun_ReadsOnIt(DestinationKind kind)
    {
        var shell = new AShell { Timelines = FakeTimelineReader.ReadingOn([.. Ids(100).Select(Post)]) };
        var opened = await shell.Opened();

        opened.Rail.GoTo(opened.Rail.Of(kind));
        shell.Host.Drain();
        opened.Viewed(under: 0, screenful: Screenful);
        shell.Host.Drain();

        var feed = Assert.IsType<FeedScreen>(opened.Screen);
        Assert.Equal(Run(kind), Assert.Single(shell.Timelines.ReadsOn).Timeline);
        Assert.Equal(Ids(80), feed.Posts.Select(post => post.Id));
    }

    /// <summary><c>g</c> asks for the run again and draws what it holds now.</summary>
    [Theory]
    [InlineData(DestinationKind.Bookmarks)]
    [InlineData(DestinationKind.Favorites)]
    public async Task Refresh_ReadsTheRunAgain(DestinationKind kind)
    {
        var shell = new AShell { Timelines = FakeTimelineReader.Holding(APost.With(id: "110")) };
        var opened = await shell.Opened();

        opened.Rail.GoTo(opened.Rail.Of(kind));
        shell.Host.Drain();
        shell.Timelines.NowHolding(APost.With(id: "111"), APost.With(id: "110"));

        await opened.Refresh();
        shell.Host.Drain();

        var feed = Assert.IsType<FeedScreen>(opened.Screen);
        Assert.Equal(Run(kind), shell.Timelines.Reads[^1].Timeline);
        Assert.Equal(["111", "110"], feed.Posts.Select(post => post.Id));
    }

    /// <summary>
    ///     Neither run carries an unread count: a whole page read is a page of what the profile kept, not of anything
    ///     waiting for it, and the rail draws the label alone.
    /// </summary>
    [Theory]
    [InlineData(DestinationKind.Bookmarks, "Bookmarks")]
    [InlineData(DestinationKind.Favorites, "Favorites")]
    public async Task NeitherRunIsGivenAnUnreadCount(DestinationKind kind, string label)
    {
        using var drawn = await DrawnShell.Of(
            80,
            Framed,
            Themes.Plain,
            new AShell { Timelines = FakeTimelineReader.ReadingOn([.. Ids(100).Select(Post)]) });

        drawn.Shell.Rail.GoTo(drawn.Shell.Rail.Of(kind));
        drawn.Built.Host.Drain();
        drawn.Redraw();

        Assert.Equal(0, drawn.Shell.Rail.Of(kind).Unread);
        Assert.Equal($"│▶ {label,-15} │", drawn.Rail()[RailRowOf(drawn, label)]);
    }

    /// <summary>
    ///     A run with nothing on it says what it is that has not happened yet, rather than calling the run a timeline
    ///     with nothing on it.
    /// </summary>
    [Theory]
    [InlineData(DestinationKind.Bookmarks, "Nothing bookmarked yet.")]
    [InlineData(DestinationKind.Favorites, "Nothing favorited yet.")]
    public async Task AnEmptyRun_SaysNothingIsMarkedYet(DestinationKind kind, string said)
    {
        var shell = new AShell { Timelines = FakeTimelineReader.Holding() };
        var opened = await shell.Opened();

        opened.Rail.GoTo(opened.Rail.Of(kind));
        shell.Host.Drain();

        Assert.Equal(said, Assert.IsType<FeedScreen>(opened.Screen).Notice);
    }

    /// <summary>
    ///     Taking the run's own mark off a row leaves the row where it is with the mark gone, so the reader sees what
    ///     they did and can press the key again to undo it — the Pinned run's rule. The next read of the run is the
    ///     instance's, and leaves it out.
    /// </summary>
    [Theory]
    [InlineData(DestinationKind.Bookmarks, PostMark.Bookmark)]
    [InlineData(DestinationKind.Favorites, PostMark.Favorite)]
    public async Task TakingTheRunsMarkOff_LeavesTheRowWhereItIs_UntilTheRunIsReadAgain(
        DestinationKind kind,
        PostMark mark)
    {
        var marked = APost.With(id: "220", account: "ben@hachyderm.io", marks: APost.Marked(favorited: true, bookmarked: true));
        var shell = new AShell
        {
            Timelines = FakeTimelineReader.Holding(APost.With(id: "300"), marked),
            Engagement = FakePostEngagement.Answering(
                APost.With(
                    id: "220",
                    account: "ben@hachyderm.io",
                    marks: APost.Marked(
                        favorited: mark != PostMark.Favorite,
                        bookmarked: mark != PostMark.Bookmark))),
        };
        var opened = await shell.Opened();

        opened.Rail.GoTo(opened.Rail.Of(kind));
        shell.Host.Drain();
        opened.Screen.Pick(1);
        Assert.Equal("220", opened.Screen.Picked?.Id);

        await opened.Mark(mark);
        shell.Host.Drain();

        var feed = Assert.IsType<FeedScreen>(opened.Screen);
        Assert.False(Assert.Single(shell.Engagement.Marks).Wanted);
        Assert.Equal(["300", "220"], feed.Posts.Select(post => post.Id));
        Assert.Equal("220", feed.Picked?.Id);
        Assert.False(mark == PostMark.Bookmark ? feed.Picked?.Marks.Bookmarked : feed.Picked?.Marks.Favorited);

        shell.Timelines.NowHolding(APost.With(id: "300"));
        opened.StepTo(DestinationKind.Home);
        shell.Host.Settle();
        opened.StepTo(kind);
        shell.Host.Settle();

        Assert.Equal(["300"], Assert.IsType<FeedScreen>(opened.Screen).Posts.Select(post => post.Id));
    }

    /// <summary>
    ///     A post given the run's mark on another destination is on the run the next time the reader arrives there,
    ///     without a <c>g</c>: the run held from before is read again rather than handed back without it (#564).
    /// </summary>
    [Theory]
    [InlineData(DestinationKind.Bookmarks, PostMark.Bookmark)]
    [InlineData(DestinationKind.Favorites, PostMark.Favorite)]
    public async Task GivingAPostTheRunsMarkElsewhere_PutsItOnTheRunHeld(DestinationKind kind, PostMark mark)
    {
        var plain = APost.With(id: "220", account: "ben@hachyderm.io");
        var marked = plain with
        {
            Marks = APost.Marked(favorited: mark == PostMark.Favorite, bookmarked: mark == PostMark.Bookmark),
        };
        Post[] onTheRun = [APost.With(id: "300")];
        var shell = new AShell
        {
            Timelines = FakeTimelineReader.Answering(timeline =>
                Fetch<Post>.Complete(timeline == Run(kind) ? onTheRun : [plain])),
            Engagement = FakePostEngagement.Answering(marked),
        };
        var opened = await HomeHavingReadTheRun(shell, kind);

        await opened.Mark(mark);
        shell.Host.Drain();
        onTheRun = [marked, APost.With(id: "300")];
        opened.StepTo(kind);
        shell.Host.Settle();

        Assert.True(Assert.Single(shell.Engagement.Marks).Wanted);
        Assert.Equal(["220", "300"], Assert.IsType<FeedScreen>(opened.Screen).Posts.Select(post => post.Id));
    }

    /// <summary>
    ///     A post already carrying the run's mark, given the other mark, is not newly on the run: the run held is
    ///     handed back rather than read again — what the post gained, not what it has, is what clears a run.
    /// </summary>
    [Theory]
    [InlineData(DestinationKind.Bookmarks, PostMark.Favorite)]
    [InlineData(DestinationKind.Favorites, PostMark.Bookmark)]
    public async Task GivingAPostAlreadyOnTheRunTheOtherMark_LeavesTheRunHeld(DestinationKind kind, PostMark other)
    {
        var onItsRun = APost.With(
            id: "220",
            account: "ben@hachyderm.io",
            marks: APost.Marked(favorited: kind == DestinationKind.Favorites, bookmarked: kind == DestinationKind.Bookmarks));
        var shell = new AShell
        {
            Timelines = FakeTimelineReader.Answering(timeline =>
                Fetch<Post>.Complete(timeline == Run(kind) ? [APost.With(id: "300")] : [onItsRun])),
            Engagement = FakePostEngagement.Answering(
                onItsRun with { Marks = APost.Marked(favorited: true, bookmarked: true) }),
        };
        var opened = await HomeHavingReadTheRun(shell, kind);

        await opened.Mark(other);
        shell.Host.Drain();
        opened.StepTo(kind);
        shell.Host.Settle();

        Assert.True(Assert.Single(shell.Engagement.Marks).Wanted);
        Assert.Single(shell.Timelines.Reads, read => read.Timeline == Run(kind));
        Assert.Equal(["300"], Assert.IsType<FeedScreen>(opened.Screen).Posts.Select(post => post.Id));
    }

    /// <summary>
    ///     Taking the run's mark off a post on another destination is the rule every mark already follows: the run
    ///     held holds the post, so it is read again on the next arrival, and the instance leaves the post out.
    /// </summary>
    [Theory]
    [InlineData(DestinationKind.Bookmarks, PostMark.Bookmark)]
    [InlineData(DestinationKind.Favorites, PostMark.Favorite)]
    public async Task TakingTheRunsMarkOffElsewhere_LeavesThePostOffTheNextArrival(DestinationKind kind, PostMark mark)
    {
        var marked = APost.With(
            id: "220",
            account: "ben@hachyderm.io",
            marks: APost.Marked(favorited: mark == PostMark.Favorite, bookmarked: mark == PostMark.Bookmark));
        Post[] onTheRun = [marked, APost.With(id: "300")];
        var shell = new AShell
        {
            Timelines = FakeTimelineReader.Answering(timeline =>
                Fetch<Post>.Complete(timeline == Run(kind) ? onTheRun : [marked])),
            Engagement = FakePostEngagement.Answering(marked with { Marks = PostMarks.None }),
        };
        var opened = await HomeHavingReadTheRun(shell, kind);

        await opened.Mark(mark);
        shell.Host.Drain();
        onTheRun = [APost.With(id: "300")];
        opened.StepTo(kind);
        shell.Host.Settle();

        Assert.False(Assert.Single(shell.Engagement.Marks).Wanted);
        Assert.Equal(["300"], Assert.IsType<FeedScreen>(opened.Screen).Posts.Select(post => post.Id));
    }

    /// <summary>A click on either arrives at it at once, as a click on any destination does (#288).</summary>
    [Theory]
    [InlineData(DestinationKind.Bookmarks, "Bookmarks")]
    [InlineData(DestinationKind.Favorites, "Favorites")]
    public async Task ClickingTheRunArrivesAtItAtOnce(DestinationKind kind, string label)
    {
        using var drawn = await DrawnShell.Of(80, Framed, Themes.Plain);

        drawn.Click(RailLines.Width / 2, RailRowOf(drawn, label));
        drawn.Built.Host.Drain();
        drawn.Redraw();

        Assert.Equal(drawn.Shell.Rail.IndexOf(kind), drawn.Shell.Rail.Current);
        Assert.Equal(0, drawn.Built.Host.Waiting);
        Assert.IsType<FeedScreen>(drawn.Shell.Screen);
        Assert.Equal(Run(kind), drawn.Built.Timelines.Reads[^1].Timeline);
    }

    /// <summary>
    ///     Hidden, the rail cannot be walked, but going to either by name still arrives there, the breadcrumb saying
    ///     where it landed.
    /// </summary>
    [Theory]
    [InlineData(DestinationKind.Bookmarks, "Bookmarks")]
    [InlineData(DestinationKind.Favorites, "Favorites")]
    public async Task GoingToTheRunByNameArrivesWhileTheRailIsHidden(DestinationKind kind, string label)
    {
        using var drawn = await DrawnShell.Of(80, Framed, Themes.Plain);

        drawn.Press(new Key('\\'));
        drawn.Shell.Rail.GoTo(drawn.Shell.Rail.Of(kind));
        drawn.Built.Host.Drain();
        drawn.Redraw();

        Assert.Equal(RailState.Hidden, drawn.Shell.RailState);
        Assert.Equal([label], drawn.Shell.Crumbs);
        Assert.Equal(Run(kind), drawn.Built.Timelines.Reads[^1].Timeline);
        Assert.StartsWith($"╭ {label}", drawn.Rows()[0], StringComparison.Ordinal);
    }

    /// <summary>
    ///     The runs are always those of the profile being acted as: after a switch, arriving at either reads it as the
    ///     new profile, with its token.
    /// </summary>
    [Theory]
    [InlineData(DestinationKind.Bookmarks)]
    [InlineData(DestinationKind.Favorites)]
    public async Task AfterASwitch_TheRunIsTheNewProfilesOwn(DestinationKind kind)
    {
        var shell = new AShell
        {
            Profiles = FakeProfileRegistry.Holding(
                "personal",
                FakeProfileRegistry.Profile("personal", "mastodon.social", "jeff@mastodon.social"),
                FakeProfileRegistry.Profile("work", "hachyderm.io", "jeff@hachyderm.io")),
        };
        var opened = await shell.Opened();

        opened.Press(ShellKey.CtrlP);
        Assert.IsType<ProfilesScreen>(opened.Screen).Pick("work");
        Assert.True(opened.Press(ShellKey.Enter));
        shell.Host.Drain();
        shell.Timelines.Reads.Clear();
        shell.Timelines.Tokens.Clear();

        opened.StepTo(kind);
        shell.Host.Settle();

        var read = Assert.Single(shell.Timelines.Reads);
        Assert.Equal("work", read.Profile);
        Assert.Equal(Run(kind), read.Timeline);
        Assert.Equal("token-work", Assert.Single(shell.Timelines.Tokens));
    }

    /// <summary>
    ///     A shell standing on Home again, having read the run of <paramref name="kind" /> on the way — so the run is
    ///     held.
    /// </summary>
    private static async Task<Shell> HomeHavingReadTheRun(AShell shell, DestinationKind kind)
    {
        var opened = await shell.Opened();

        opened.StepTo(kind);
        shell.Host.Settle();
        opened.StepTo(DestinationKind.Home);
        shell.Host.Settle();

        return opened;
    }

    /// <summary>The run a destination of <paramref name="kind" /> reads.</summary>
    private static Timeline Run(DestinationKind kind) =>
        kind == DestinationKind.Bookmarks ? Timeline.Bookmarks : Timeline.Favorites;

    /// <summary>A post with <paramref name="id" />.</summary>
    private static Post Post(string id) => APost.With(id: id);

    /// <summary>The ids from 1 to <paramref name="last" />, in order.</summary>
    private static IEnumerable<string> Ids(int last) => Enumerable.Range(1, last).Select(at => $"{at}");
}
