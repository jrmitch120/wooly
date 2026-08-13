using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
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
///     Asking a destination for fresh posts by hand: <c>g</c> evicts what the destination last held and puts the same
///     question its own arrival puts, keeping the reader on the post they were reading (<c>docs/tui-shell.md</c>, #84).
/// </summary>
/// <remarks>
///     No terminal, except where the question is which key a reader pressed: what a refresh costs, what it draws and
///     where it leaves the pick are all facts about the shell.
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

        Assert.Equal(readsWhenOpened + 1, shell.Timelines.Reads.Count);

        var feed = Assert.IsType<FeedScreen>(opened.Screen);

        Assert.Equal(["111", "110"], feed.Posts.Select(post => post.Id));
    }

    /// <summary>
    ///     The reader is left on the post they were reading, not on whatever has arrived above it since — which is the
    ///     difference between a refresh and a reload.
    /// </summary>
    [Fact]
    public async Task Refresh_KeepsTheReaderOnThePostTheyWereOn()
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

        Assert.Equal("220", opened.Screen.Picked?.Id);
    }

    /// <summary>A post taken down while it was being read leaves the pick at the same ordinal rather than at the top.</summary>
    [Fact]
    public async Task Refresh_ClampsAtTheSameOrdinalWhereThePostIsGone()
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

        shell.Timelines.NowHolding(APost.With(id: "110"), APost.With(id: "330"));

        await opened.Refresh();

        Assert.Equal("330", opened.Screen.Picked?.Id);
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

        Assert.NotSame(before, opened.Screen);
        Assert.Equal(1, opened.Depth);
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

        shell.Engagement.NowAnswered(APost.With(id: "111"), APost.With(id: "112"));

        await opened.Refresh();

        var post = Assert.IsType<PostScreen>(opened.Screen);

        Assert.Equal("110", post.Post.Id);
        Assert.Equal(["111", "112"], post.Replies.Select(reply => reply.Id));
        Assert.Equal(2, shell.Engagement.RepliesRead.Count);

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

        var reads = shell.Accounts.Reads.Count;
        var timelines = shell.Timelines.Reads.Count;

        await opened.Refresh();

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

        Assert.True(opened.Fetching);

        await opened.Refresh();

        Assert.Equal(inFlight, shell.Timelines.Reads.Count);

        held.SetResult(Fetch<Post>.Complete([APost.With(id: "111")]));

        await refreshing;

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
        var held = new TaskCompletionSource<IReadOnlyList<Post>>();
        var reads = 0;

        var shell = new AShell
        {
            Timelines = FakeTimelineReader.Holding(APost.With(id: "110")),
            Engagement = FakePostEngagement.Awaiting(() => reads++ == 0
                ? Task.FromResult<IReadOnlyList<Post>>([APost.With(id: "111")])
                : held.Task),
        };

        var opened = await shell.Opened();

        await opened.Enter();

        var refreshing = opened.Refresh();

        opened.Back();

        held.SetResult([APost.With(id: "112")]);

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

        var conversation = Assert.IsType<ConversationScreen>(opened.Screen);
        var shown = shell.Messages.Shown.Count;

        await opened.Refresh();

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
        opened.Type('d');

        await opened.Find();
        await opened.OpenResult();

        var tag = Assert.IsType<FeedScreen>(opened.Screen);
        var reads = shell.Timelines.Reads.Count;

        Assert.False(tag.Refreshes);

        await opened.Refresh();

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
        var built = new AShell { Timelines = FakeTimelineReader.Holding(APost.With(id: "110")) };
        var shell = await built.Opened();

        using var window = new ShellWindow(shell, Themes.Plain, built.Clock, () => { }, FakePictures.DrawingNothing())
        {
            Width = 80,
            Height = 20,
        };

        window.Layout();

        var reads = built.Timelines.Reads.Count;

        window.NewKeyDownEvent(Key.G);

        Assert.Equal(reads + 1, built.Timelines.Reads.Count);
    }

    private static int Badge(Wooly.Tui.Shell.Shell shell, DestinationKind kind) =>
        shell.Rail.Destinations.First(destination => destination.Kind == kind).Unread;

    /// <summary>One of each screen, built with as little as it takes to have one.</summary>
    private static Screen Of(string kind)
    {
        var post = APost.With(id: "110");

        switch (kind)
        {
            case "feed":
                return new FeedScreen(new Destination(DestinationKind.Home, "Home", Timeline.Home), [post], refreshes: true);

            case "hashtag":
                return new FeedScreen(
                    new Destination(DestinationKind.Hashtag, "#dotnet", Timeline.Tag("dotnet")),
                    [post]);

            case "notifications":
                return new NotificationsScreen([ANotification.With()]);

            case "messages":
                return new DirectMessagesScreen([AConversation.With()]);

            case "requests":
                return new FollowRequestsScreen([AnAccount.With()]);

            case "post":
                return new PostScreen(post, []);

            case "account":
                return new AccountScreen(AnAccount.With(), [post]);

            case "conversation":
                return new ConversationScreen(AConversation.Thread());

            case "search":
                var search = new SearchScreen();

                search.Found("dotnet", new SearchResults { Accounts = [], Hashtags = [], Posts = [post] });

                return search;

            case "compose":
                return new ComposeScreen(ComposeFor.Post);

            case "notice":
                return new NoticeScreen("hashtag", "No hashtag is set for the rail.");

            default:
                return new HelpScreen(new PostScreen(post, []));
        }
    }
}
