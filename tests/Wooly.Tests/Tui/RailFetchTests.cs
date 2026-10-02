using Wooly.Core.Paging;
using Wooly.Core.Posts;
using Wooly.Core.Timelines;
using Wooly.Tests.Fakes;
using Wooly.Tui.Screens;
using Wooly.Tui.Shell;

namespace Wooly.Tests.Tui;

/// <summary>
///     The rule ADR-0014 was written to settle, tested where it is decided: a run of rail presses is one selection and
///     one fetch. No terminal is involved — what a keypress costs is a fact about the shell, and the prototype's own
///     measurement (six fetches, five thrown away) was of exactly this and nothing about drawing.
/// </summary>
public class RailFetchTests
{
    /// <summary>
    ///     The measurement from the ADR, as a test. Six tabs walking Home → Notifications are six cursor moves, one
    ///     selection and one fetch — against six with five discarded before the settle rule.
    /// </summary>
    [Fact]
    public async Task Step_SendsOneFetchForARunOfPresses()
    {
        var shell = new AShell();
        var opened = await shell.Opened();
        var readsWhenOpened = shell.Timelines.Reads.Count;

        for (var press = 0; press < 6; press++)
        {
            opened.Step(1);
        }

        // Six presses left one wait outstanding, not six: each abandoned the one before it.
        Assert.Equal(1, shell.Host.Waiting);
        Assert.Equal(6, opened.Rail.Cursor);
        Assert.Equal(0, opened.Rail.Current);

        shell.Host.Settle();

        Assert.Equal(6, opened.Rail.Current);
        Assert.Equal(readsWhenOpened, shell.Timelines.Reads.Count);
    }

    /// <summary>
    ///     The cursor is the half that never waits: a key that draws nothing for a quarter of a second reads as lag
    ///     however much work it is saving.
    /// </summary>
    [Fact]
    public async Task Step_MovesTheCursorAtOnceAndTheSelectionOnlyWhenThePressingStops()
    {
        var shell = new AShell();
        var opened = await shell.Opened();

        opened.Step(1);

        Assert.Equal(1, opened.Rail.Cursor);
        Assert.Equal(0, opened.Rail.Current);

        shell.Host.Settle();

        Assert.Equal(1, opened.Rail.Cursor);
        Assert.Equal(1, opened.Rail.Current);
    }

    /// <summary>A walk that ends where it started has selected nothing, so it asks for nothing.</summary>
    [Fact]
    public async Task Step_AsksForNothingWhereTheWalkEndedWhereItBegan()
    {
        var shell = new AShell();
        var opened = await shell.Opened();
        var readsWhenOpened = shell.Timelines.Reads.Count;

        opened.Step(1);
        opened.Step(-1);
        shell.Host.Settle();

        Assert.Equal(0, opened.Rail.Current);
        Assert.Equal(readsWhenOpened, shell.Timelines.Reads.Count);
    }

    /// <summary>Landing on a destination is what asks the instance for it, and it asks for the one it landed on.</summary>
    [Fact]
    public async Task Step_FetchesTheDestinationTheCursorSettledOn()
    {
        var shell = new AShell();
        var opened = await shell.Opened();

        opened.Step(2);
        shell.Host.Settle();

        Assert.Equal(TimelineScope.Federated, shell.Timelines.Reads[^1].Timeline.Scope);
        Assert.Equal("Federated", opened.Breadcrumb);
    }

    /// <summary>
    ///     Walking out along the rail and back is one fetch per destination rather than one per arrival — which is
    ///     what pays for cycling (ADR-0014).
    /// </summary>
    [Fact]
    public async Task Step_DrawsARecentlyFetchedDestinationWithoutAskingForItAgain()
    {
        var shell = new AShell();
        var opened = await shell.Opened();

        opened.Step(1);
        shell.Host.Settle();

        var afterLocal = shell.Timelines.Reads.Count;

        opened.Step(1);
        shell.Host.Settle();
        opened.Step(-1);
        shell.Host.Settle();

        Assert.Equal(TimelineScope.Local, opened.Rail.Showing.Timeline?.Scope);

        // Federated cost one; going back to Local cost nothing.
        Assert.Equal(afterLocal + 1, shell.Timelines.Reads.Count);
    }

    /// <summary>A cache that never went stale would be a client showing yesterday's timeline for ever.</summary>
    [Fact]
    public async Task Step_FetchesADestinationAgainOnceWhatItHeldHasAged()
    {
        var shell = new AShell();
        var opened = await shell.Opened();

        opened.Step(1);
        shell.Host.Settle();

        var afterLocal = shell.Timelines.Reads.Count;

        opened.Step(1);
        shell.Host.Settle();

        shell.Clock.Advance(shell.Timing.CacheFor + TimeSpan.FromSeconds(1));

        opened.Step(-1);
        shell.Host.Settle();

        Assert.Equal(afterLocal + 2, shell.Timelines.Reads.Count);
    }

    /// <summary>
    ///     An answer overtaken before it landed is dropped rather than drawn: a reader two destinations further along
    ///     must not have a timeline they have left appear underneath them.
    /// </summary>
    [Fact]
    public async Task Step_DiscardsAnAnswerTheReaderHasAlreadyMovedOnFrom()
    {
        var held = new TaskCompletionSource<Fetch<Post>>();
        var shell = new AShell
        {
            Timelines = FakeTimelineReader.Awaiting(timeline => timeline.Scope switch
            {
                TimelineScope.Local => held.Task,
                _ => Task.FromResult(Fetch<Post>.Complete([APost.With(id: $"{timeline.Scope}")])),
            }),
        };

        var opened = await shell.Opened();

        // Local is asked for and its answer is held up; the reader walks on to Federated, which lands first.
        opened.Step(1);
        shell.Host.Settle();

        opened.Step(1);
        shell.Host.Settle();

        // Local's answer finally lands, two destinations too late.
        held.SetResult(Fetch<Post>.Complete([APost.With(id: "stale")]));

        Assert.Equal(TimelineScope.Federated, opened.Rail.Showing.Timeline?.Scope);
        Assert.Equal("Federated", opened.Breadcrumb);

        var feed = Assert.IsType<FeedScreen>(opened.Screen);
        Assert.DoesNotContain(feed.Posts, post => post.Id == "stale");
    }

    /// <summary>
    ///     A destination that asks the instance for nothing still overtakes what the last one asked for. Without that,
    ///     stepping from a timeline still in flight onto search — a prompt, which asks for nothing until something is
    ///     typed into it — would let the timeline land on top of the prompt a moment later.
    /// </summary>
    [Fact]
    public async Task Step_DiscardsAnAnswerOvertakenByADestinationThatFetchesNothing()
    {
        var held = new TaskCompletionSource<Fetch<Post>>();
        var shell = new AShell
        {
            Timelines = FakeTimelineReader.Awaiting(timeline => timeline.Scope switch
            {
                TimelineScope.Local => held.Task,
                _ => Task.FromResult(Fetch<Post>.Complete([APost.With()])),
            }),
        };

        var opened = await shell.Opened();

        opened.Step(1);
        shell.Host.Settle();

        // On to search, which is a prompt and asks the instance for nothing at all until something is typed into it.
        opened.Step(4);
        shell.Host.Settle();

        Assert.IsType<SearchScreen>(opened.Screen);

        held.SetResult(Fetch<Post>.Complete([APost.With(id: "stale")]));

        Assert.IsType<SearchScreen>(opened.Screen);
        Assert.Equal("Search", opened.Breadcrumb);
    }

    /// <summary>
    ///     The same rule for a drill: a reader who tabbed away while the replies were in flight is somewhere else, and
    ///     a post screen appearing over the destination they are on now is the same stale answer.
    /// </summary>
    [Fact]
    public async Task Enter_DiscardsRepliesTheReaderHasAlreadyTabbedAwayFrom()
    {
        var shell = new AShell();
        var opened = await shell.Opened();

        var drilling = opened.Enter();

        opened.Step(1);
        shell.Host.Settle();

        await drilling;

        Assert.IsType<FeedScreen>(opened.Screen);
        Assert.Equal("Local", opened.Breadcrumb);
    }

    /// <summary>
    ///     Ten of them, in the order the contract lists, which is their rail groups' order (ADR-0021), including the
    ///     four whose screens are #29's and #30's. The shape of the rail is what #28 settled, and a rail that grows four
    ///     entries later is a different rail — the tenth is the one entry it has ever grown by, and it was argued for on
    ///     those terms (ADR-0019, #181).
    /// </summary>
    [Fact]
    public async Task Rail_ListsAllTenDestinations()
    {
        var shell = new AShell { Hashtag = "dotnet" };
        var opened = await shell.Opened();

        Assert.Equal(
            [
                DestinationKind.Home,
                DestinationKind.Local,
                DestinationKind.Federated,
                DestinationKind.Hashtag,
                DestinationKind.Discover,
                DestinationKind.Search,
                DestinationKind.Notifications,
                DestinationKind.Messages,
                DestinationKind.Requests,
                DestinationKind.Profile,
            ],
            opened.Rail.Destinations.Select(destination => destination.Kind));
    }

    /// <summary>The rail's order is its groups' order: no group is drawn in two places (ADR-0021).</summary>
    [Fact]
    public async Task Rail_DrawsEachGroupTogetherInTheGroupsOrder()
    {
        var opened = await new AShell().Opened();

        var groups = opened.Rail.Destinations.Select(destination => destination.Group).ToList();

        Assert.Equal(groups.Order(), groups);
    }

    /// <summary>Home, local, federated and a hashtag are all reachable, and each reads its own timeline.</summary>
    [Theory]
    [InlineData(1, TimelineScope.Local)]
    [InlineData(2, TimelineScope.Federated)]
    [InlineData(3, TimelineScope.Tag)]
    public async Task Step_ReachesEachOfTheFourTimelines(int steps, TimelineScope expected)
    {
        var shell = new AShell { Hashtag = "dotnet" };
        var opened = await shell.Opened();

        opened.Step(steps);
        shell.Host.Settle();

        Assert.Equal(expected, shell.Timelines.Reads[^1].Timeline.Scope);
    }

    /// <summary>The tag the reader keeps a place for is the tag that destination reads.</summary>
    [Fact]
    public async Task Step_ReadsTheHashtagTheReaderNamed()
    {
        var shell = new AShell { Hashtag = "dotnet" };
        var opened = await shell.Opened();

        opened.Step(3);
        shell.Host.Settle();

        Assert.Equal("dotnet", shell.Timelines.Reads[^1].Timeline.Hashtag);
        Assert.Equal("#dotnet", opened.Rail.Destinations[3].Label);
    }

    /// <summary>
    ///     A destination that swallowed a keypress and drew the last screen again would read as a bug, so the one with
    ///     no tag set says so instead — and asks the instance for nothing.
    /// </summary>
    [Fact]
    public async Task Step_SaysSoRatherThanFetchingWhereNoHashtagHasBeenNamed()
    {
        var shell = new AShell();
        var opened = await shell.Opened();
        var readsWhenOpened = shell.Timelines.Reads.Count;

        opened.Step(3);
        shell.Host.Settle();

        Assert.IsType<NoticeScreen>(opened.Screen);
        Assert.Equal(readsWhenOpened, shell.Timelines.Reads.Count);

        // And it says where you are in the rail's own words. This is the one screen whose crumb is handed to it at
        // the call site rather than spelled on the class, so the breadcrumb is where the spelling is asserted (#216).
        Assert.Equal(opened.Rail.Showing.Label, opened.Breadcrumb);
        Assert.Equal("Hashtag", opened.Breadcrumb);
    }

    /// <summary>Each destination that lists something of its own arrives at its own screen, not at somebody else's.</summary>
    [Theory]
    [InlineData(5, typeof(SearchScreen))]
    [InlineData(6, typeof(NotificationsScreen))]
    [InlineData(7, typeof(DirectMessagesScreen))]
    [InlineData(8, typeof(FollowRequestsScreen))]
    public async Task Step_ArrivesAtTheScreenItsDestinationOpensOnto(int steps, Type screen)
    {
        var shell = new AShell();
        var opened = await shell.Opened();

        opened.Step(steps);
        shell.Host.Settle();

        Assert.Equal(steps, opened.Rail.Current);
        Assert.IsType(screen, opened.Screen);
    }

    /// <summary>
    ///     A timeline carries no badge, and arriving at one puts none there — nor disturbs the count a destination
    ///     that has one is already carrying. What a destination counts is its own to say (#100), so the four timelines
    ///     saying they count nothing is what leaves the rail alone here.
    /// </summary>
    [Fact]
    public async Task Step_PutsNoCountOnADestinationThatCountsNothing()
    {
        var shell = new AShell
        {
            Timelines = FakeTimelineReader.Holding(APost.With(id: "110"), APost.With(id: "111")),
            Notifications = FakeNotificationInbox.Holding(ANotification.With(id: "1")),
        };

        var opened = await shell.Opened();

        opened.Step(1);
        shell.Host.Settle();

        var feed = Assert.IsType<FeedScreen>(opened.Screen);

        Assert.Equal(2, feed.Posts.Count);
        Assert.All(
            opened.Rail.Destinations.Where(destination => destination.Timeline is not null),
            destination => Assert.Equal(0, destination.Unread));

        Assert.Equal(
            1,
            opened.Rail.Destinations.First(destination => destination.Kind == DestinationKind.Notifications).Unread);
    }

    /// <summary>
    ///     <c>tab</c> walks the shown order and nothing else: from the last timeline into Explore, and from the top of
    ///     the Inbox back up into Explore — wrapping at either end of the rail.
    /// </summary>
    [Theory]
    [InlineData(DestinationKind.Hashtag, 1, DestinationKind.Discover)]
    [InlineData(DestinationKind.Notifications, -1, DestinationKind.Search)]
    [InlineData(DestinationKind.Profile, 1, DestinationKind.Home)]
    [InlineData(DestinationKind.Home, -1, DestinationKind.Profile)]
    public async Task Step_WalksTheRailInItsShownOrder(DestinationKind from, int by, DestinationKind to)
    {
        var shell = new AShell { Hashtag = "dotnet" };
        var opened = await shell.Opened();

        opened.Rail.GoTo(from);
        opened.Step(by);
        shell.Host.Settle();

        Assert.Equal(to, opened.Rail.Showing.Kind);
    }

    /// <summary>
    ///     <c>`</c> lands on the first destination of the next rail group from anywhere in the one it is in, and the
    ///     last group's next is the first (ADR-0021).
    /// </summary>
    [Theory]
    [InlineData(DestinationKind.Home, DestinationKind.Discover)]
    [InlineData(DestinationKind.Local, DestinationKind.Discover)]
    [InlineData(DestinationKind.Federated, DestinationKind.Discover)]
    [InlineData(DestinationKind.Hashtag, DestinationKind.Discover)]
    [InlineData(DestinationKind.Discover, DestinationKind.Notifications)]
    [InlineData(DestinationKind.Search, DestinationKind.Notifications)]
    [InlineData(DestinationKind.Requests, DestinationKind.Profile)]
    [InlineData(DestinationKind.Profile, DestinationKind.Home)]
    public async Task Backtick_LandsOnTheFirstDestinationOfTheNextGroup(DestinationKind from, DestinationKind to)
    {
        var shell = new AShell { Hashtag = "dotnet" };
        var opened = await shell.Opened();

        opened.Rail.GoTo(from);
        opened.Press(ShellKey.Backtick);
        shell.Host.Settle();

        Assert.Equal(to, opened.Rail.Showing.Kind);
    }

    /// <summary>
    ///     <c>~</c> walks the same groups backwards, and lands on the first of the group before rather than the last
    ///     of it — so from inside a group it goes to the group before, not to the top of its own.
    /// </summary>
    [Theory]
    [InlineData(DestinationKind.Home, DestinationKind.Profile)]
    [InlineData(DestinationKind.Federated, DestinationKind.Profile)]
    [InlineData(DestinationKind.Discover, DestinationKind.Home)]
    [InlineData(DestinationKind.Search, DestinationKind.Home)]
    [InlineData(DestinationKind.Messages, DestinationKind.Discover)]
    [InlineData(DestinationKind.Profile, DestinationKind.Notifications)]
    public async Task Tilde_LandsOnTheFirstDestinationOfThePreviousGroup(DestinationKind from, DestinationKind to)
    {
        var shell = new AShell { Hashtag = "dotnet" };
        var opened = await shell.Opened();

        opened.Rail.GoTo(from);
        opened.Press(ShellKey.Tilde);
        shell.Host.Settle();

        Assert.Equal(to, opened.Rail.Showing.Kind);
    }

    /// <summary>
    ///     A run of group presses is what a run of tabs is: six cursor moves, one wait outstanding, one selection and
    ///     one fetch (ADR-0014).
    /// </summary>
    [Fact]
    public async Task Backtick_SendsOneFetchForARunOfPresses()
    {
        var shell = new AShell();
        var opened = await shell.Opened();
        var readsWhenOpened = shell.Timelines.Reads.Count;
        var inboxReadsWhenOpened = shell.Notifications.Reads.Count;

        // Explore, Inbox, You, Timelines, Explore, Inbox.
        for (var press = 0; press < 6; press++)
        {
            opened.Press(ShellKey.Backtick);
        }

        Assert.Equal(1, shell.Host.Waiting);
        Assert.Equal(DestinationKind.Notifications, opened.Rail.Destinations[opened.Rail.Cursor].Kind);
        Assert.Equal(0, opened.Rail.Current);

        shell.Host.Settle();

        Assert.Equal(DestinationKind.Notifications, opened.Rail.Showing.Kind);

        // The one it landed on was asked for once, and nothing the walk crossed was asked for on the way.
        Assert.Equal(inboxReadsWhenOpened + 1, shell.Notifications.Reads.Count);
        Assert.Equal(readsWhenOpened, shell.Timelines.Reads.Count);
        Assert.Empty(shell.Suggestions.Reads);
    }
}
