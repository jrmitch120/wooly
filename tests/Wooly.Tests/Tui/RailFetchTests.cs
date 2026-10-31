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
        opened.Step(3);
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
    ///     The nine everybody has, in the order the contract lists, which is their rail groups' order (ADR-0021). The
    ///     shape of the rail is what #28 settled, and the ninth is the one fixed entry it has ever grown by, argued for
    ///     on those terms (ADR-0019, #181) — and with no tag sets written, those nine are all there is (ADR-0033).
    /// </summary>
    [Fact]
    public async Task Rail_ListsTheNineEverybodyHas_WhereThereAreNoTagSets()
    {
        var opened = await new AShell().Opened();

        Assert.Equal(
            [
                DestinationKind.Home,
                DestinationKind.Local,
                DestinationKind.Federated,
                DestinationKind.Discover,
                DestinationKind.Search,
                DestinationKind.Notifications,
                DestinationKind.Messages,
                DestinationKind.Requests,
                DestinationKind.Profile,
            ],
            opened.Rail.Destinations.Select(destination => destination.Kind));
        Assert.DoesNotContain(RailGroup.Hashtags, opened.Rail.Destinations.Select(destination => destination.Group));
    }

    /// <summary>
    ///     Each tag set is a destination of its own, called by its name, in the Hashtags group between Timelines and
    ///     Explore and in the order the file wrote them (ADR-0033).
    /// </summary>
    [Fact]
    public async Task Rail_ListsEachTagSetBetweenTheTimelinesAndExplore_InTheOrderWritten()
    {
        var opened = await Sets().Opened();

        Assert.Equal(
            [
                "Home", "Local", "Federated", "zebras", "dotnet", "Discover", "Search", "Notifications",
                "Direct messages", "Follow requests",
            ],
            opened.Rail.Destinations.SkipLast(1).Select(destination => destination.Label));
        Assert.Equal(
            [RailGroup.Hashtags, RailGroup.Hashtags],
            opened.Rail.Destinations.Where(destination => destination.Kind == DestinationKind.TagSet)
                .Select(destination => destination.Group));
    }

    /// <summary>The rail's order is its groups' order: no group is drawn in two places (ADR-0021).</summary>
    [Fact]
    public async Task Rail_DrawsEachGroupTogetherInTheGroupsOrder()
    {
        var opened = await Sets().Opened();

        var groups = opened.Rail.Destinations.Select(destination => destination.Group).ToList();

        Assert.Equal(groups.Order(), groups);
    }

    /// <summary>Home, local, federated and each tag set are all reachable, and each reads its own timeline.</summary>
    [Theory]
    [InlineData(1, TimelineScope.Local)]
    [InlineData(2, TimelineScope.Federated)]
    [InlineData(3, TimelineScope.TagSet)]
    [InlineData(4, TimelineScope.TagSet)]
    public async Task Step_ReachesEachOfTheTimelines(int steps, TimelineScope expected)
    {
        var shell = Sets();
        var opened = await shell.Opened();

        opened.Step(steps);
        shell.Host.Settle();

        Assert.Equal(expected, shell.Timelines.Reads[^1].Timeline.Scope);
    }

    /// <summary>
    ///     A set read is the set the reader wrote, whole, and arriving at it resets the stack to its timeline with its
    ///     name as the breadcrumb, as arriving anywhere does.
    /// </summary>
    [Theory]
    [InlineData(3, "zebras")]
    [InlineData(4, "dotnet")]
    public async Task Step_ReadsTheTagSetTheReaderWrote_UnderItsName(int steps, string name)
    {
        var shell = Sets();
        var opened = await shell.Opened();

        await opened.Enter();
        shell.Host.Drain();
        Assert.NotEqual("Home", opened.Breadcrumb);

        opened.Step(steps);
        shell.Host.Settle();

        Assert.Equal(Timeline.AnyOf(SetsWritten.Single(set => set.Name == name)), shell.Timelines.Reads[^1].Timeline);
        Assert.IsType<FeedScreen>(opened.Screen);
        Assert.Equal(name, opened.Breadcrumb);
    }

    /// <summary>
    ///     A set's timeline is held for the same minute every timeline is: walking from one set to the other and back
    ///     reads each once, and each is held apart from the other (#510).
    /// </summary>
    [Fact]
    public async Task Step_DrawsARecentlyReadTagSetWithoutAskingForItAgain()
    {
        var shell = Sets();
        var opened = await shell.Opened();

        opened.Step(3);
        shell.Host.Settle();
        opened.Step(1);
        shell.Host.Settle();

        var afterBoth = shell.Timelines.Reads.Count;

        opened.Step(-1);
        shell.Host.Settle();
        opened.Step(1);
        shell.Host.Settle();

        Assert.Equal(afterBoth, shell.Timelines.Reads.Count);
        Assert.Equal("dotnet", opened.Breadcrumb);

        shell.Clock.Advance(shell.Timing.CacheFor + TimeSpan.FromSeconds(1));

        opened.Step(-1);
        shell.Host.Settle();

        Assert.Equal(afterBoth + 1, shell.Timelines.Reads.Count);
        Assert.Equal(Timeline.AnyOf(SetsWritten[0]), shell.Timelines.Reads[^1].Timeline);
    }

    /// <summary>
    ///     A set's timeline carries no badge, however much is on it: the rail's counts mean only the Inbox.
    /// </summary>
    [Fact]
    public async Task Step_PutsNoCountOnATagSet()
    {
        var shell = Sets();
        shell.Timelines = FakeTimelineReader.Holding(APost.With(id: "110"), APost.With(id: "111"));
        var opened = await shell.Opened();

        opened.Step(3);
        shell.Host.Settle();

        Assert.All(
            opened.Rail.Destinations.Where(destination => destination.Kind == DestinationKind.TagSet),
            destination => Assert.Equal(0, destination.Unread));
    }

    /// <summary>
    ///     Each destination that lists something of its own arrives at its own screen, not at somebody else's.
    /// </summary>
    [Theory]
    [InlineData(4, typeof(SearchScreen))]
    [InlineData(5, typeof(NotificationsScreen))]
    [InlineData(6, typeof(DirectMessagesScreen))]
    [InlineData(7, typeof(FollowRequestsScreen))]
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
    [InlineData("Federated", 1, "zebras")]
    [InlineData("zebras", 1, "dotnet")]
    [InlineData("dotnet", 1, "Discover")]
    [InlineData("Discover", -1, "dotnet")]
    [InlineData("Notifications", -1, "Search")]
    [InlineData("Profile", 1, "Home")]
    [InlineData("Home", -1, "Profile")]
    public async Task Step_WalksTheRailInItsShownOrder(string from, int by, string to)
    {
        var shell = Sets();
        var opened = await shell.Opened();

        opened.Rail.GoTo(Place(opened.Rail, from));
        opened.Step(by);
        shell.Host.Settle();

        Assert.Equal(Place(opened.Rail, to), opened.Rail.Showing);
    }

    /// <summary>
    ///     With no sets, the last timeline steps straight into Explore: there is nothing between them to land on.
    /// </summary>
    [Fact]
    public async Task Step_FromTheLastTimelineLandsOnDiscover_WhereThereAreNoTagSets()
    {
        var shell = new AShell();
        var opened = await shell.Opened();

        opened.Rail.GoTo(opened.Rail.Of(DestinationKind.Federated));
        opened.Step(1);
        shell.Host.Settle();

        Assert.Equal(DestinationKind.Discover, opened.Rail.Showing.Kind);
    }

    /// <summary>
    ///     <c>`</c> lands on the first destination of the next rail group from anywhere in the one it is in, and the
    ///     last group's next is the first (ADR-0021).
    /// </summary>
    [Theory]
    [InlineData("Home", "zebras")]
    [InlineData("Local", "zebras")]
    [InlineData("Federated", "zebras")]
    [InlineData("zebras", "Discover")]
    [InlineData("dotnet", "Discover")]
    [InlineData("Discover", "Notifications")]
    [InlineData("Search", "Notifications")]
    [InlineData("Requests", "Profile")]
    [InlineData("Profile", "Home")]
    public async Task Backtick_LandsOnTheFirstDestinationOfTheNextGroup(string from, string to)
    {
        var shell = Sets();
        var opened = await shell.Opened();

        opened.Rail.GoTo(Place(opened.Rail, from));
        opened.Press(ShellKey.Backtick);
        shell.Host.Settle();

        Assert.Equal(Place(opened.Rail, to), opened.Rail.Showing);
    }

    /// <summary>With no sets there is no Hashtags group to land on, and the timelines' next group is Explore.</summary>
    [Fact]
    public async Task Backtick_FromTheTimelinesLandsOnDiscover_WhereThereAreNoTagSets()
    {
        var shell = new AShell();
        var opened = await shell.Opened();

        opened.Press(ShellKey.Backtick);
        shell.Host.Settle();

        Assert.Equal(DestinationKind.Discover, opened.Rail.Showing.Kind);
    }

    /// <summary>
    ///     <c>~</c> walks the same groups backwards, and lands on the first of the group before rather than the last
    ///     of it — so from inside a group it goes to the group before, not to the top of its own.
    /// </summary>
    [Theory]
    [InlineData("Home", "Profile")]
    [InlineData("Federated", "Profile")]
    [InlineData("zebras", "Home")]
    [InlineData("dotnet", "Home")]
    [InlineData("Discover", "zebras")]
    [InlineData("Search", "zebras")]
    [InlineData("Messages", "Discover")]
    [InlineData("Profile", "Notifications")]
    public async Task Tilde_LandsOnTheFirstDestinationOfThePreviousGroup(string from, string to)
    {
        var shell = Sets();
        var opened = await shell.Opened();

        opened.Rail.GoTo(Place(opened.Rail, from));
        opened.Press(ShellKey.Tilde);
        shell.Host.Settle();

        Assert.Equal(Place(opened.Rail, to), opened.Rail.Showing);
    }

    /// <summary>And backwards from Explore with no sets is the timelines.</summary>
    [Fact]
    public async Task Tilde_FromExploreLandsOnHome_WhereThereAreNoTagSets()
    {
        var shell = new AShell();
        var opened = await shell.Opened();

        opened.Rail.GoTo(opened.Rail.Of(DestinationKind.Discover));
        opened.Press(ShellKey.Tilde);
        shell.Host.Settle();

        Assert.Equal(DestinationKind.Home, opened.Rail.Showing.Kind);
    }

    /// <summary>
    ///     The two tag sets every test of them here is written with: <c>zebras</c> first, as the file wrote it.
    /// </summary>
    private static readonly IReadOnlyList<TagSet> SetsWritten =
        [TagSet.Of("zebras", ["zebras", "zebra"]), TagSet.Of("dotnet", ["dotnet", "csharp", "fsharp"])];

    /// <summary>A shell whose config file writes <see cref="SetsWritten" />.</summary>
    private static AShell Sets() => new() { TagSets = SetsWritten };

    /// <summary>
    ///     The rail's destination <paramref name="name" /> names: a tag set by its name, any other by its kind.
    /// </summary>
    private static Destination Place(Rail rail, string name) =>
        rail.Destinations.Single(destination => destination.Kind == DestinationKind.TagSet
            ? destination.Label == name
            : destination.Kind.ToString() == name);

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
        var inboxReadsWhenOpened = shell.Notifications.GroupReads.Count;

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
        Assert.Equal(inboxReadsWhenOpened + 1, shell.Notifications.GroupReads.Count);
        Assert.Equal(readsWhenOpened, shell.Timelines.Reads.Count);
        Assert.Empty(shell.Suggestions.Reads);
    }
}
