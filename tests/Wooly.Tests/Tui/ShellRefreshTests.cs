using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Wooly.Core.Errors;
using Wooly.Core.Paging;
using Wooly.Core.Posts;
using Wooly.Core.Search;
using Wooly.Core.Timelines;
using Wooly.Tests.Fakes;
using Wooly.Tui.Screens;
using Wooly.Tui.Shell;
using Wooly.Tui.Theme;
using Wooly.Tui.Views;

namespace Wooly.Tests.Tui;

/// <summary>
///     Asking a destination for fresh posts by hand: <c>g</c> evicts what the destination last held, puts the same
///     question its own arrival puts, and opens the answer at the top — on what has just arrived, which is what the
///     key is for (<c>docs/tui-shell.md</c>, #84).
/// </summary>
/// <remarks>
///     No terminal, except where the question is which key a reader pressed: what a refresh costs, what it draws and
///     where it leaves the pick are all facts about the shell. The <c>G_</c> tests draw real frames, because the row
///     the page begins on is settled inside <c>Rows()</c> on the draw and a window that is only laid out never runs
///     it.
/// </remarks>
public class ShellRefreshTests
{
    /// <summary>
    ///     The whole point of the key: a destination fetched a moment ago draws from what it held, and this is what
    ///     asks the instance anyway.
    /// </summary>
    [Fact]
    public async Task Refresh_EvictsWhatTheDestinationHeldAndAsksAgain()
    {
        var shell = new AShell { Timelines = FakeTimelineReader.Holding(APost.With(id: "110")) };
        var opened = await shell.Opened();
        var readsWhenOpened = shell.Timelines.Reads.Count;

        shell.Timelines.NowHolding(APost.With(id: "111"), APost.With(id: "110"));

        await opened.Refresh();
        shell.Host.Drain();

        Assert.Equal(readsWhenOpened + 1, shell.Timelines.Reads.Count);

        var feed = Assert.IsType<FeedScreen>(opened.Screen);

        Assert.Equal(["111", "110"], feed.Posts.Select(post => post.Id));
    }

    /// <summary>
    ///     The reader is put back at the top of the list, on whatever has arrived since — which is what a refresh is
    ///     for. Keeping them where they were would leave the new posts above the page: fetched, and invisible.
    /// </summary>
    [Fact]
    public async Task Refresh_PutsTheReaderAtTheTopOfWhatArrived()
    {
        var shell = new AShell
        {
            Timelines = FakeTimelineReader.Holding(
                APost.With(id: "110"),
                APost.With(id: "220"),
                APost.With(id: "330")),
        };

        var opened = await shell.Opened();

        opened.Move(1);

        Assert.Equal("220", opened.Screen.Picked?.Id);

        shell.Timelines.NowHolding(
            APost.With(id: "105"),
            APost.With(id: "110"),
            APost.With(id: "220"),
            APost.With(id: "330"));

        await opened.Refresh();

        shell.Host.Drain();

        // The newest post, which is the one they pressed g to see — not the 220 they were standing on.
        Assert.Equal("105", opened.Screen.Picked?.Id);
    }

    /// <summary>
    ///     A screen replaced rather than changed, which is what puts the scroll offset back to nought — the offset
    ///     starts again whenever the screen is replaced (<c>docs/tui-shell.md</c>), and the view notices by identity.
    /// </summary>
    [Fact]
    public async Task Refresh_PutsAFreshScreenUpRatherThanChangingTheOneOnTheStack()
    {
        var shell = new AShell();
        var opened = await shell.Opened();
        var before = opened.Screen;

        await opened.Refresh();
        shell.Host.Drain();

        Assert.NotSame(before, opened.Screen);
        Assert.Equal(1, opened.Depth);
    }

    /// <summary>
    ///     A refresh that fails leaves the reader reading. An arrival puts an empty screen up at once because what was
    ///     on screen is about somewhere else; here it is about exactly where they are, so it stands until there is
    ///     something fresher to put in its place — and a rate limit or a refusal is a notice over the list rather than
    ///     an empty screen where it used to be.
    /// </summary>
    [Fact]
    public async Task Refresh_LeavesTheListUpWhereTheInstanceRefuses()
    {
        var reads = 0;

        var shell = new AShell
        {
            Timelines = FakeTimelineReader.Awaiting(_ => reads++ == 0
                ? Task.FromResult(Fetch<Post>.Complete([APost.With(id: "110"), APost.With(id: "220")]))
                : Task.FromException<Fetch<Post>>(new AuthenticationException("No."))),
        };

        var opened = await shell.Opened();

        opened.Move(1);

        await opened.Refresh();
        shell.Host.Drain();

        var feed = Assert.IsType<FeedScreen>(opened.Screen);

        Assert.Equal(["110", "220"], feed.Posts.Select(post => post.Id));
        Assert.Equal("220", opened.Screen.Picked?.Id);
        Assert.Equal("No.", opened.Notice);
        Assert.True(opened.NoticeIsError);
    }

    /// <summary>The badge moves with the list it is drawn beside, the same as at any other arrival.</summary>
    [Fact]
    public async Task Refresh_MovesTheBadgeWithTheCountItRedrawsFrom()
    {
        var shell = new AShell { Notifications = FakeNotificationInbox.Holding(ANotification.With(id: "1")) };
        var opened = await shell.Opened();

        opened.Step(4);
        shell.Host.Settle();

        Assert.Equal(1, Badge(opened, DestinationKind.Notifications));

        shell.Notifications.NowHolding(ANotification.With(id: "1"), ANotification.With(id: "2"));

        await opened.Refresh();
        shell.Host.Drain();

        var notifications = Assert.IsType<NotificationsScreen>(opened.Screen);

        Assert.Equal(2, notifications.Notifications.Count);
        Assert.Equal(2, Badge(opened, DestinationKind.Notifications));
    }

    /// <summary>Each of the seven destinations that read a list is refreshed by the arrival it already arrives by.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task Refresh_AsksTheTimelineOfWhicheverFeedTheReaderIsOn(int steps)
    {
        var shell = new AShell { Hashtag = "dotnet" };
        var opened = await shell.Opened();

        opened.Step(steps);
        shell.Host.Settle();

        var asked = shell.Timelines.Reads[^1].Timeline;
        var reads = shell.Timelines.Reads.Count;

        await opened.Refresh();

        Assert.Equal(reads + 1, shell.Timelines.Reads.Count);
        Assert.Equal(asked, shell.Timelines.Reads[^1].Timeline);
    }

    /// <summary>The three inbox destinations too, each through its own port.</summary>
    [Fact]
    public async Task Refresh_AsksTheInboxDestinationsAgain()
    {
        var shell = new AShell
        {
            Notifications = FakeNotificationInbox.Holding(ANotification.With(id: "1")),
            Messages = FakeDirectMessages.Holding(AConversation.With(id: "7")),
            Accounts = FakeAccountRelationships.Holding(listing: AnAccount.With(id: "9", address: "ada@hachyderm.io")),
        };

        var opened = await shell.Opened();

        opened.Step(4);
        shell.Host.Settle();

        var notifications = shell.Notifications.Reads.Count;

        await opened.Refresh();

        Assert.Equal(notifications + 1, shell.Notifications.Reads.Count);

        opened.Step(1);
        shell.Host.Settle();

        var listings = shell.Messages.Listings.Count;

        await opened.Refresh();

        Assert.Equal(listings + 1, shell.Messages.Listings.Count);

        opened.Step(1);
        shell.Host.Settle();

        var pending = shell.Accounts.Lists.Count;

        await opened.Refresh();

        Assert.Equal(pending + 1, shell.Accounts.Lists.Count);
    }

    /// <summary>
    ///     The post screen is not reached through an arrival, so its refresh is the same <c>Replies</c> call <c>⏎</c>
    ///     already runs — and the post it is about stays the post it is about.
    /// </summary>
    [Fact]
    public async Task Refresh_ReadsTheAnswersToAPostAgain()
    {
        var shell = new AShell
        {
            Timelines = FakeTimelineReader.Holding(APost.With(id: "110")),
            Engagement = FakePostEngagement.Answered(APost.With(id: "110"), APost.With(id: "111")),
        };

        var opened = await shell.Opened();

        await opened.Enter();
        shell.Host.Drain();

        shell.Engagement.NowAnswered(APost.With(id: "111"), APost.With(id: "112"));

        await opened.Refresh();
        shell.Host.Drain();

        var post = Assert.IsType<PostScreen>(opened.Screen);

        Assert.Equal("110", post.Post.Id);
        Assert.Equal(["111", "112"], post.Replies.Select(reply => reply.Id));
        Assert.Equal(2, shell.Engagement.ThreadsRead.Count);

        // Still one level in: a refresh redraws the screen the reader is on rather than putting them somewhere else.
        Assert.Equal(2, opened.Depth);
    }

    /// <summary>The account screen re-runs both of the calls that opened it, and stays where it is on the stack.</summary>
    [Fact]
    public async Task Refresh_ReadsBothOfTheAccountScreensCallsAgain()
    {
        var shell = new AShell
        {
            Timelines = FakeTimelineReader.Holding(APost.With(id: "110", account: "ben@hachyderm.io")),
            Accounts = FakeAccountRelationships.Holding(AnAccount.With(address: "ben@hachyderm.io")),
        };

        var opened = await shell.Opened();

        await opened.OpenAuthor();
        shell.Host.Drain();

        var reads = shell.Accounts.Reads.Count;
        var timelines = shell.Timelines.Reads.Count;

        await opened.Refresh();
        shell.Host.Drain();

        var account = Assert.IsType<AccountScreen>(opened.Screen);

        Assert.Equal("ben@hachyderm.io", account.Account.Address);
        Assert.Equal(reads + 1, shell.Accounts.Reads.Count);
        Assert.Equal(timelines + 1, shell.Timelines.Reads.Count);
        Assert.Equal(2, opened.Depth);
    }

    /// <summary>
    ///     A second press while the first is still in flight does nothing at all — no second question, and no
    ///     in-flight UI beyond the <c>fetching…</c> marker the breadcrumb already carries.
    /// </summary>
    [Fact]
    public async Task Refresh_DoesNothingWhileAQuestionIsAlreadyInFlight()
    {
        var held = new TaskCompletionSource<Fetch<Post>>();
        var reads = 0;

        var shell = new AShell
        {
            Timelines = FakeTimelineReader.Awaiting(_ => reads++ == 0
                ? Task.FromResult(Fetch<Post>.Complete([APost.With(id: "110")]))
                : held.Task),
        };

        var opened = await shell.Opened();

        var refreshing = opened.Refresh();
        var inFlight = shell.Timelines.Reads.Count;

        shell.Host.Drain();

        Assert.True(opened.Fetching);

        await opened.Refresh();
        shell.Host.Drain();

        Assert.Equal(inFlight, shell.Timelines.Reads.Count);

        held.SetResult(Fetch<Post>.Complete([APost.With(id: "111")]));

        await refreshing;
        shell.Host.Drain();

        var feed = Assert.IsType<FeedScreen>(opened.Screen);

        Assert.Equal(["111"], feed.Posts.Select(post => post.Id));
    }

    /// <summary>
    ///     A refresh is an enquiry like any other, so an answer the reader has walked away from is dropped rather than
    ///     drawn underneath them.
    /// </summary>
    [Fact]
    public async Task Refresh_DiscardsAnAnswerTheReaderHasArrivedSomewhereElseFrom()
    {
        var held = new TaskCompletionSource<Fetch<Post>>();
        var reads = 0;

        var shell = new AShell
        {
            Timelines = FakeTimelineReader.Awaiting(timeline => timeline.Scope switch
            {
                TimelineScope.Home when reads++ > 0 => held.Task,
                _ => Task.FromResult(Fetch<Post>.Complete([APost.With(id: "110")])),
            }),
        };

        var opened = await shell.Opened();

        var refreshing = opened.Refresh();

        opened.Step(1);
        shell.Host.Settle();

        held.SetResult(Fetch<Post>.Complete([APost.With(id: "stale")]));

        await refreshing;

        var feed = Assert.IsType<FeedScreen>(opened.Screen);

        Assert.Equal("local", opened.Breadcrumb);
        Assert.DoesNotContain(feed.Posts, post => post.Id == "stale");
    }

    /// <summary>
    ///     The same rule for the two screens no arrival reaches: they are not overtaken by an arrival, so each rechecks
    ///     that the reader is still standing on it — the idiom <c>Find</c> and <c>OpenResult</c> already use.
    /// </summary>
    [Fact]
    public async Task Refresh_DropsAnswersTheReaderHasWalkedOutOfThePostScreenBefore()
    {
        var held = new TaskCompletionSource<PostThread>();
        var reads = 0;

        var shell = new AShell
        {
            Timelines = FakeTimelineReader.Holding(APost.With(id: "110")),
            Engagement = FakePostEngagement.Awaiting(() => reads++ == 0
                ? Task.FromResult(new PostThread([], [APost.With(id: "111")]))
                : held.Task),
        };

        var opened = await shell.Opened();

        await opened.Enter();

        var refreshing = opened.Refresh();

        opened.Back();

        held.SetResult(new PostThread([], [APost.With(id: "112")]));

        await refreshing;

        Assert.IsType<FeedScreen>(opened.Screen);
        Assert.Equal(1, opened.Depth);
    }

    /// <summary>
    ///     A screen with no refresh does nothing at all when the key is pressed — the conversation screen and a search's
    ///     results are each their own question and deliberately out of scope here.
    /// </summary>
    [Fact]
    public async Task Refresh_DoesNothingOnAScreenThatOffersNone()
    {
        var shell = new AShell
        {
            Messages = FakeDirectMessages.Holding(AConversation.With(id: "7")),
        };

        var opened = await shell.Opened();

        opened.Step(5);
        shell.Host.Settle();

        await opened.OpenConversation();
        shell.Host.Drain();

        var conversation = Assert.IsType<ConversationScreen>(opened.Screen);
        var shown = shell.Messages.Shown.Count;

        await opened.Refresh();
        shell.Host.Drain();

        Assert.Same(conversation, opened.Screen);
        Assert.Equal(shown, shell.Messages.Shown.Count);
    }

    /// <summary>
    ///     A hashtag walked to from a search is a screen on the stack rather than the destination the rail keeps a
    ///     place for, and refresh is the destination's. Out of scope with the search results it was opened from.
    /// </summary>
    [Fact]
    public async Task Refresh_DoesNothingOnAHashtagWalkedToFromASearch()
    {
        var shell = new AShell
        {
            Search = FakeInstanceSearch.Finding(accounts: [], hashtags: [AHashtag.With(name: "dotnet")], posts: []),
        };

        var opened = await shell.Opened();

        opened.Search();

        shell.Host.Drain();

        opened.Type('d');

        await opened.Find();
        shell.Host.Drain();
        await opened.OpenResult();
        shell.Host.Drain();

        var tag = Assert.IsType<FeedScreen>(opened.Screen);
        var reads = shell.Timelines.Reads.Count;

        Assert.False(tag.Refreshes);

        await opened.Refresh();
        shell.Host.Drain();

        Assert.Same(tag, opened.Screen);
        Assert.Equal(reads, shell.Timelines.Reads.Count);
    }

    /// <summary>
    ///     The status row and the key agree, in both directions: every screen that answers to <c>g</c> says so, and no
    ///     screen says so that does not. A key announced and then refused reads as a shell that missed the press.
    /// </summary>
    [Theory]
    [MemberData(nameof(Screens))]
    public void Keys_SayRefreshOnExactlyTheScreensThatAnswerToIt(string kind, bool refreshes)
    {
        var screen = Of(kind);

        Assert.Equal(refreshes, screen.Refreshes);
        Assert.Equal(refreshes, screen.Keys.Contains(Screen.Refreshing));

        // And the words on the row are the contract's, not each screen's own.
        Assert.Equal("g:refresh", Screen.Refreshing.ToString());
    }

    /// <summary>Every screen there is, and whether <c>g</c> means anything on it (<c>docs/tui-shell.md</c>).</summary>
    public static TheoryData<string, bool> Screens => new()
    {
        { "feed", true },
        { "hashtag", false },
        { "notifications", true },
        { "messages", true },
        { "requests", true },
        { "post", true },
        { "account", true },
        { "conversation", false },
        { "search", false },
        { "compose", false },
        { "notice", false },
        { "help", false },
    };

    /// <summary>And the key itself, asked of the window that binds it.</summary>
    [Fact]
    public async Task G_AsksTheDestinationAgain()
    {
        var (window, shell, built) = await Laid();

        using (window)
        {
            var reads = built.Timelines.Reads.Count;

            window.NewKeyDownEvent(Key.G);

            Assert.Equal(reads + 1, built.Timelines.Reads.Count);
            Assert.IsType<FeedScreen>(shell.Screen);
        }
    }

    /// <summary>
    ///     <c>g</c> takes the reader to the top of the freshly read list, which is what the key is for: they have
    ///     asked to see what is new, and what is new is at the top (#84).
    /// </summary>
    /// <remarks>
    ///     <c>PgDn</c> first, so the page starts well down the list and "went to the top" is a real movement rather
    ///     than a page that never left. Asserted on <see cref="PaintedView.Top" /> — the row the page begins on, the
    ///     one fact the region owns outright — and on the pick, since both halves have to arrive at the top together.
    ///     <para>
    ///         Real frames are drawn either side, because <c>_top</c> is settled inside <c>Rows()</c> on the draw: a
    ///         window that is only laid out never runs the clamping or the follow that every actual frame runs.
    ///     </para>
    /// </remarks>
    [Fact]
    public async Task G_TakesTheReaderToTheTopOfTheFreshList()
    {
        var (window, shell, built) = await Laid();

        using (window)
        {
            var content = window.SubViews.OfType<PaintedView>().Single(view => view.Id == ShellWindow.ContentId);

            window.Draw();
            window.NewKeyDownEvent(Key.PageDown);
            window.Draw();

            Assert.NotEqual(0, content.Top);

            window.NewKeyDownEvent(Key.G);

            built.Host.Drain();
            window.Draw();

            var feed = Assert.IsType<FeedScreen>(shell.Screen);

            Assert.Equal(0, content.Top);
            Assert.Equal(feed.Posts[0].Id, shell.Screen.Picked?.Id);
        }
    }

    /// <summary>
    ///     And the posts that arrived are the ones now in front of the reader, which is the whole point of the key.
    /// </summary>
    [Fact]
    public async Task G_ShowsThePostsThatArrivedSinceAtTheTop()
    {
        var (window, shell, built) = await Laid();

        using (window)
        {
            var content = window.SubViews.OfType<PaintedView>().Single(view => view.Id == ShellWindow.ContentId);

            window.Draw();
            window.NewKeyDownEvent(Key.PageDown);
            window.NewKeyDownEvent(Key.PageDown);
            window.Draw();

            built.Timelines.NowHolding(
                APost.With(id: "990"),
                APost.With(id: "880"),
                APost.With(id: "110"),
                APost.With(id: "220"),
                APost.With(id: "330"),
                APost.With(id: "440"));

            window.NewKeyDownEvent(Key.G);

            built.Host.Drain();
            window.Draw();

            Assert.Equal(0, content.Top);
            Assert.Equal("990", shell.Screen.Picked?.Id);
        }
    }

    /// <summary>
    ///     The answer to <c>g</c> lands after the task that asked it has finished: nothing goes up until the terminal
    ///     gets round to it.
    /// </summary>
    /// <remarks>
    ///     Pinned as a fact about <em>when</em> rather than what. A fake host that ran the callback inline would put
    ///     the screen up here, and every assertion after it would be about an order the terminal never runs in.
    /// </remarks>
    [Fact]
    public async Task G_PutsNothingUpUntilTheTerminalGetsRoundToIt()
    {
        var (window, shell, built) = await Laid();

        using (window)
        {
            var showing = shell.Screen;

            window.NewKeyDownEvent(Key.G);

            Assert.Same(showing, shell.Screen);

            built.Host.Drain();

            Assert.NotSame(showing, shell.Screen);
        }
    }

    /// <summary>
    ///     And the same on the post screen, which no arrival reaches: <c>Freshened</c> puts the fresher copy on the
    ///     stack rather than <c>Arrival.Landed</c>, and both leave the reader at the top of it.
    /// </summary>
    [Fact]
    public async Task G_TakesTheReaderToTheTopOfAPostScreenToo()
    {
        var (window, shell, built) = await Laid();

        using (window)
        {
            var content = window.SubViews.OfType<PaintedView>().Single(view => view.Id == ShellWindow.ContentId);

            window.NewKeyDownEvent(Key.Enter);

            built.Host.Drain();
            window.Draw();

            var post = Assert.IsType<PostScreen>(shell.Screen);

            window.NewKeyDownEvent(Key.PageDown);
            window.Draw();

            Assert.NotEqual(0, content.Top);

            window.NewKeyDownEvent(Key.G);

            built.Host.Drain();
            window.Draw();

            Assert.NotSame(post, shell.Screen);
            Assert.IsType<PostScreen>(shell.Screen);
            Assert.Equal(0, content.Top);
        }
    }

    /// <summary>A shell of four posts on a window with room for eighteen rows, laid out and ready for keys.</summary>
    private static async Task<(ShellWindow Window, Wooly.Tui.Shell.Shell Shell, AShell Built)> Laid()
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
            Height = 20,
        };

        window.Layout();

        return (window, shell, built);
    }

    private static int Badge(Wooly.Tui.Shell.Shell shell, DestinationKind kind) =>
        shell.Rail.Destinations.First(destination => destination.Kind == kind).Unread;

    /// <summary>One of each screen, built with as little as it takes to have one.</summary>
    /// <remarks>
    ///     Every kind named, including the last: a kind nobody said what to build would otherwise be whichever screen
    ///     the fall-through happened to name, and a typo in the table above would pass green over the wrong screen.
    /// </remarks>
    private static Screen Of(string kind) => kind switch
    {
        "feed" => new FeedScreen(
            new Destination(DestinationKind.Home, "Home", Timeline.Home),
            [APost.With(id: "110")],
            refreshes: true),

        "hashtag" => new FeedScreen(
            new Destination(DestinationKind.Hashtag, "#dotnet", Timeline.Tag("dotnet")),
            [APost.With(id: "110")]),

        "notifications" => new NotificationsScreen([ANotification.With()]),
        "messages" => new DirectMessagesScreen([AConversation.With()]),
        "requests" => new FollowRequestsScreen([AnAccount.With()]),
        "post" => new PostScreen(APost.With(id: "110"), PostThread.Alone),
        "account" => new AccountScreen(AnAccount.With(), [APost.With(id: "110")]),
        "conversation" => new ConversationScreen(AConversation.Thread()),
        "search" => Searched(),
        "compose" => new ComposeScreen(ComposeFor.Post),
        "notice" => new NoticeScreen("hashtag", "No hashtag is set for the rail."),
        "help" => new HelpScreen(new PostScreen(APost.With(id: "110"), PostThread.Alone)),
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "No screen of that kind."),
    };

    /// <summary>A search that has been answered, which is the screen its results are on rather than its prompt.</summary>
    private static SearchScreen Searched()
    {
        var search = new SearchScreen();

        search.Found("dotnet", new SearchResults { Accounts = [], Hashtags = [], Posts = [APost.With(id: "110")] });

        return search;
    }
}
