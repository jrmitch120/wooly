using Wooly.Core.Conversations;
using Wooly.Tests.Fakes;
using Wooly.Tui.Rendering;
using Wooly.Tui.Screens;
using Wooly.Tui.Shell;
using Person = Wooly.Core.Accounts.Account;

namespace Wooly.Tests.Tui;

/// <summary>
///     Reading on, on Direct messages and Follow requests (#466): as the page nears the end of what either holds, the
///     shell asks the instance for what stands behind it and adds it below — and sets the destination's <b>Unread
///     count</b> off the whole list held again, as every read of it does: exactly once the list is whole, and as "at
///     least" while it is not.
/// </summary>
public class ShellInboxReadingOnTests
{
    /// <summary>The height of the content region these tests say the page is, in rows.</summary>
    private const int Screenful = 20;

    /// <summary>Where the rail's Direct messages destination is, counting from Home.</summary>
    private const int ToMessages = 7;

    /// <summary>Where the rail's Follow requests destination is, counting from Home.</summary>
    private const int ToRequests = 8;

    /// <summary>
    ///     The page within a screenful of the end of the requests: the rest of those waiting are asked for and put under
    ///     the ones held, with the pick left where it was — and, nobody being left behind them, the foot says so.
    /// </summary>
    [Fact]
    public async Task Requests_ViewedNearTheEnd_PutTheRestWaitingUnderThoseHeld()
    {
        var built = new AShell { Accounts = FakeAccountRelationships.AskingReadingOn([.. Asking(55)]) };
        var opened = await AtRequests(built);

        opened.Walk(2, reclaiming: null);
        opened.Viewed(under: 0, screenful: Screenful);
        built.Host.Drain();

        var requests = Assert.IsType<FollowRequestsScreen>(opened.Screen);
        Assert.Single(built.Accounts.RequestsReadOn);
        Assert.Equal(Ids(55), requests.Waiting.Select(asking => asking.Id));
        Assert.Equal("a2", requests.PickedAccount?.Id);
        Assert.Equal("that's everything", Foot(requests));
    }

    /// <summary>
    ///     Story 30: requests that reach 55 by reading on, and are then whole, are counted off the list held — the badge
    ///     says 55, not the <c>40+</c> the first read could only say.
    /// </summary>
    [Fact]
    public async Task Requests_ReadOnToWhole_CountTheBadgeExactly()
    {
        var built = new AShell { Accounts = FakeAccountRelationships.AskingReadingOn([.. Asking(55)]) };
        var opened = await AtRequests(built);

        Assert.True(Badge(opened, DestinationKind.Requests).Full);

        opened.Viewed(under: 0, screenful: Screenful);
        built.Host.Drain();

        var badge = Badge(opened, DestinationKind.Requests);
        Assert.Equal(55, badge.Unread);
        Assert.False(badge.Full);
    }

    /// <summary>
    ///     Counting further down the list is not something arriving: the badge rising from <c>40+</c> to 55 rings no
    ///     bell, everyone counted having been waiting all along.
    /// </summary>
    [Fact]
    public async Task Requests_ReadOn_RingsNoBellForTheBadgeCountingHigher()
    {
        var built = new AShell { Accounts = FakeAccountRelationships.AskingReadingOn([.. Asking(55)]) };
        var opened = await AtRequests(built);

        opened.Viewed(under: 0, screenful: Screenful);
        built.Host.Drain();

        Assert.Equal(0, built.Host.Bells);
    }

    /// <summary>A list not whole yet still says "at least": 80 read on, with more behind them, is <c>80+</c>.</summary>
    [Fact]
    public async Task Requests_ReadOnButNotWhole_SayAtLeastAsManyAsAreHeld()
    {
        var built = new AShell { Accounts = FakeAccountRelationships.AskingReadingOn([.. Asking(100)]) };
        var opened = await AtRequests(built);

        opened.Viewed(under: 0, screenful: Screenful);
        built.Host.Drain();

        var badge = Badge(opened, DestinationKind.Requests);
        Assert.Equal(80, badge.Unread);
        Assert.True(badge.Full);
        Assert.DoesNotContain("that's everything", Rows(opened.Screen));
    }

    /// <summary>Answering one under an exact badge moves it by one, as it always has: 55 whole, one answered, is 54.</summary>
    [Fact]
    public async Task Requests_AnsweredOnceReadOnToWhole_MoveTheBadgeByOne()
    {
        var built = new AShell { Accounts = FakeAccountRelationships.AskingReadingOn([.. Asking(55)]) };
        var opened = await AtRequests(built);

        opened.Viewed(under: 0, screenful: Screenful);
        built.Host.Drain();
        opened.Press(ShellKey.A);
        built.Host.Drain();

        var badge = Badge(opened, DestinationKind.Requests);
        Assert.Equal(54, badge.Unread);
        Assert.False(badge.Full);
        Assert.Equal(54, Assert.IsType<FollowRequestsScreen>(opened.Screen).Waiting.Count);
    }

    /// <summary>
    ///     Somebody asking live still goes on top of a list read on, and the people read on stay under it: the two ends
    ///     never interfere.
    /// </summary>
    [Fact]
    public async Task Requests_ReadOn_StillTakeSomebodyAskingLiveOnTop()
    {
        var built = new AShell { Accounts = FakeAccountRelationships.AskingReadingOn([.. Asking(55)]) };
        var opened = await AtRequests(built);

        opened.Viewed(under: 0, screenful: Screenful);
        built.Host.Drain();
        built.Accounts.NowListing([AnAccount.With(id: "new", address: "newcomer@mas.to"), .. Asking(55)]);
        built.Clock.Advance(TimeSpan.FromSeconds(60));
        built.Host.Drain();

        var requests = Assert.IsType<FollowRequestsScreen>(opened.Screen);
        Assert.Equal(["new", .. Ids(55)], requests.Waiting.Select(asking => asking.Id));
    }

    /// <summary>Opens the shell and arrives at Follow requests.</summary>
    private static async Task<Shell> AtRequests(AShell built)
    {
        var opened = await built.Opened();

        opened.Step(ToRequests);
        built.Host.Settle();

        return opened;
    }

    /// <summary>Opens the shell and arrives at Direct messages.</summary>
    private static async Task<Shell> AtMessages(AShell built)
    {
        var opened = await built.Opened();

        opened.Step(ToMessages);
        built.Host.Settle();

        return opened;
    }

    /// <summary><paramref name="count" /> people asking to follow, ids <c>a0</c> on, in the order the instance lists them.</summary>
    private static IEnumerable<Person> Asking(int count) =>
        Enumerable.Range(0, count).Select(n => AnAccount.With(id: $"a{n}", address: $"asker{n}@mas.to"));

    /// <summary>The ids <see cref="Asking" /> hands out, for <paramref name="count" /> of them.</summary>
    private static IEnumerable<string> Ids(int count) => Enumerable.Range(0, count).Select(n => $"a{n}");

    /// <summary>
    ///     <paramref name="count" /> conversations, ids <c>c0</c> on, most recently spoken in first, those
    ///     <paramref name="unread" /> names flagged unread.
    /// </summary>
    private static IReadOnlyList<Conversation> Conversations(int count, Func<int, bool> unread) =>
        [.. Enumerable.Range(0, count).Select(n => AConversation.With(id: $"c{n}", unread: unread(n)))];

    /// <summary>Every row <paramref name="screen" /> draws, as text.</summary>
    private static IReadOnlyList<string> Rows(Screen screen) =>
        [.. screen.Lines(new Drawing(61, AShell.Now)).Select(line => line.Text)];

    /// <summary>The last row <paramref name="screen" /> draws, which is the foot of its list where it has one.</summary>
    private static string Foot(Screen screen) => Rows(screen)[^1];

    private static Destination Badge(Shell shell, DestinationKind kind) =>
        shell.Rail.Destinations.Single(destination => destination.Kind == kind);
}
