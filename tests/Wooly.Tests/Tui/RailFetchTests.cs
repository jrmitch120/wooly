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
    ///     The measurement from the ADR, as a test. Six tabs walking Home → Follow requests are six cursor moves, one
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
        Assert.Equal("federated", opened.Breadcrumb);
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
        var held = new TaskCompletionSource<TimelineFetch>();
        var shell = new AShell
        {
            Timelines = FakeTimelineReader.Awaiting(timeline => timeline.Scope switch
            {
                TimelineScope.Local => held.Task,
                _ => Task.FromResult(TimelineFetch.Complete([APost.With(id: $"{timeline.Scope}")])),
            }),
        };

        var opened = await shell.Opened();

        // Local is asked for and its answer is held up; the reader walks on to Federated, which lands first.
        opened.Step(1);
        shell.Host.Settle();

        opened.Step(1);
        shell.Host.Settle();

        // Local's answer finally lands, two destinations too late.
        held.SetResult(TimelineFetch.Complete([APost.With(id: "stale")]));

        Assert.Equal(TimelineScope.Federated, opened.Rail.Showing.Timeline?.Scope);
        Assert.Equal("federated", opened.Breadcrumb);

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
        var held = new TaskCompletionSource<TimelineFetch>();
        var shell = new AShell
        {
            Timelines = FakeTimelineReader.Awaiting(timeline => timeline.Scope switch
            {
                TimelineScope.Local => held.Task,
                _ => Task.FromResult(TimelineFetch.Complete([APost.With()])),
            }),
        };

        var opened = await shell.Opened();

        opened.Step(1);
        shell.Host.Settle();

        // On to search, which is a prompt and asks the instance for nothing at all until something is typed into it.
        opened.Step(6);
        shell.Host.Settle();

        Assert.IsType<SearchScreen>(opened.Screen);

        held.SetResult(TimelineFetch.Complete([APost.With(id: "stale")]));

        Assert.IsType<SearchScreen>(opened.Screen);
        Assert.Equal("search", opened.Breadcrumb);
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
        Assert.Equal("local", opened.Breadcrumb);
    }

    /// <summary>
    ///     Nine of them, in the order the contract lists, including the four whose screens are #29's and #30's. The
    ///     shape of the rail is what this ticket settles, and a rail that grows four entries later is a different rail.
    /// </summary>
    [Fact]
    public async Task Rail_ListsAllNineDestinations()
    {
        var shell = new AShell { Hashtag = "dotnet" };
        var opened = await shell.Opened();

        Assert.Equal(
            [
                DestinationKind.Home,
                DestinationKind.Local,
                DestinationKind.Federated,
                DestinationKind.Hashtag,
                DestinationKind.Notifications,
                DestinationKind.Messages,
                DestinationKind.Requests,
                DestinationKind.Search,
                DestinationKind.Profile,
            ],
            opened.Rail.Destinations.Select(destination => destination.Kind));
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
    }

    /// <summary>Each destination that lists something of its own arrives at its own screen, not at somebody else's.</summary>
    [Theory]
    [InlineData(4, typeof(NotificationsScreen))]
    [InlineData(5, typeof(DirectMessagesScreen))]
    [InlineData(6, typeof(FollowRequestsScreen))]
    [InlineData(7, typeof(SearchScreen))]
    public async Task Step_ArrivesAtTheScreenItsDestinationOpensOnto(int steps, Type screen)
    {
        var shell = new AShell();
        var opened = await shell.Opened();

        opened.Step(steps);
        shell.Host.Settle();

        Assert.Equal(steps, opened.Rail.Current);
        Assert.IsType(screen, opened.Screen);
    }
}
