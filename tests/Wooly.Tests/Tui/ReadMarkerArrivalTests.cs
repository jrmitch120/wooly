using Wooly.Core.Notifications;
using Wooly.Tests.Fakes;
using Wooly.Tui.Rendering;
using Wooly.Tui.Screens;
using Wooly.Tui.Shell;

namespace Wooly.Tests.Tui;

/// <summary>
///     Arriving at Notifications reads them: the account's <b>Read marker</b> moves to the newest notification shown,
///     so the badge clears as the reader looks — on the web too, since the marker is the instance's — while the rows
///     that were unread stay marked for that visit (CONTEXT.md, ADR-0027, #397).
/// </summary>
public class ReadMarkerArrivalTests
{
    /// <summary>Where the rail's notifications destination is, counting from Home.</summary>
    private const int ToNotifications = 6;

    [Fact]
    public async Task Arriving_MovesTheMarkerToTheNewestNotificationAndClearsTheBadge()
    {
        var shell = new AShell { Notifications = Holding(count: 5, newest: 144) };
        shell.Notifications.Marker = "141";

        var opened = await shell.Opened();

        opened.Step(ToNotifications);
        shell.Host.Settle();

        Assert.Equal("144", Assert.Single(shell.Notifications.MarkerMoves).NotificationId);
        Assert.Equal(0, Badge(opened).Unread);
    }

    /// <summary>
    ///     The badge has cleared, but which rows were new is still worth knowing while the reader is here — said with a
    ///     word, so it reads with no colour (ADR-0014). Reading never dismisses: every row stays.
    /// </summary>
    [Fact]
    public async Task Arriving_DrawsTheRowsNewerThanTheOldMarkerUnreadAndNoOthers()
    {
        var shell = new AShell { Notifications = Holding(count: 5, newest: 144) };
        shell.Notifications.Marker = "142";

        var opened = await shell.Opened();

        opened.Step(ToNotifications);
        shell.Host.Settle();

        Assert.Equal(["144", "143"], Unread(opened));
        Assert.Equal(5, Assert.IsType<NotificationsScreen>(opened.Screen).Notifications.Count);
        Assert.Empty(shell.Notifications.Dismissals);

        var rows = Assert.IsType<NotificationsScreen>(opened.Screen)
                         .Lines(new Drawing(Width: 60, Now: AShell.Now))
                         .Select(line => line.Text)
                         .Where(text => text.Contains("mentioned you", StringComparison.Ordinal))
                         .ToList();
        Assert.Equal(5, rows.Count);
        Assert.Equal([true, true, false, false, false], rows.Select(row => row.Contains("unread", StringComparison.Ordinal)));
    }

    /// <summary>
    ///     Coming back inside the cache window is another visit: what was unread on the first has been read, so nothing
    ///     is marked, and nothing more is asked of the instance.
    /// </summary>
    [Fact]
    public async Task ComingBack_ToTheHeldListMarksNothingUnread()
    {
        var shell = new AShell { Notifications = Holding(count: 5, newest: 144) };
        shell.Notifications.Marker = "142";

        var opened = await shell.Opened();

        opened.Step(ToNotifications);
        shell.Host.Settle();
        opened.Step(-1);
        shell.Host.Settle();
        opened.Step(1);
        shell.Host.Settle();

        Assert.Empty(Unread(opened));
        Assert.Single(shell.Notifications.MarkerMoves);
        Assert.Equal(0, Badge(opened).Unread);
    }

    /// <summary>Nothing newer than the marker is nothing to tell the instance.</summary>
    [Fact]
    public async Task Arriving_WithNothingUnreadDoesNotAskToMoveTheMarker()
    {
        var shell = new AShell { Notifications = Holding(count: 5, newest: 144) };
        shell.Notifications.Marker = "144";

        var opened = await shell.Opened();

        opened.Step(ToNotifications);
        shell.Host.Settle();

        Assert.Empty(shell.Notifications.MarkerMoves);
    }

    /// <summary>
    ///     An account that has never had a marker gets one where the reader has looked — so a later arrival has
    ///     something to count from, and what arrives after it counts.
    /// </summary>
    [Fact]
    public async Task Arriving_WithNoMarkerSetsOneAtTheNewestAndLaterArrivalsCountFromIt()
    {
        var shell = new AShell { Notifications = Holding(count: 5, newest: 144) };
        shell.Notifications.Marker = null;

        var opened = await shell.Opened();

        opened.Step(ToNotifications);
        shell.Host.Settle();

        Assert.Equal("144", Assert.Single(shell.Notifications.MarkerMoves).NotificationId);
        Assert.Equal(0, Badge(opened).Unread);

        shell.Notifications.NowHolding(
            [ANotification.With(id: "146"), ANotification.With(id: "145"), .. Ids(count: 5, newest: 144)]);
        opened.Step(-1);
        shell.Host.Settle();
        shell.Clock.Advance(shell.Timing.CacheFor + TimeSpan.FromSeconds(1));
        opened.Step(1);
        shell.Host.Settle();

        Assert.Equal("146", shell.Notifications.MarkerMoves[^1].NotificationId);
        Assert.Equal(["146", "145"], Unread(opened));
    }

    /// <summary>
    ///     A move the instance did not take costs the reader nothing they can see: the list is drawn, nothing is said,
    ///     and the badge says what was waiting, which is still true.
    /// </summary>
    [Fact]
    public async Task Arriving_WhereTheMoveFailsDrawsTheScreenAndSaysNothing()
    {
        var shell = new AShell { Notifications = Holding(count: 5, newest: 144) };
        shell.Notifications.Marker = "141";
        shell.Notifications.TurnsMarkerMovesDown = true;

        var opened = await shell.Opened();

        opened.Step(ToNotifications);
        shell.Host.Settle();

        var screen = Assert.IsType<NotificationsScreen>(opened.Screen);
        Assert.Equal(5, screen.Notifications.Count);
        Assert.Null(screen.Notice);
        Assert.Null(opened.Notice);
        Assert.Equal(3, Badge(opened).Unread);
    }

    /// <summary>
    ///     Only Notifications reads on arrival: a conversation stays unread until it is opened or marked, and a request
    ///     waits until it is answered — so their badges stand, and the notifications marker is left where it is.
    /// </summary>
    [Theory]
    [InlineData(ToNotifications + 1, DestinationKind.Messages)]
    [InlineData(ToNotifications + 2, DestinationKind.Requests)]
    public async Task Arriving_AtMessagesOrRequestsReadsNothing(int steps, DestinationKind kind)
    {
        var shell = new AShell
        {
            Notifications = Holding(count: 5, newest: 144),
            Messages = FakeDirectMessages.Holding(AConversation.With(id: "c1", unread: true)),
            Accounts = FakeAccountRelationships.Holding(subject: null, AnAccount.With(address: "asker@mas.to")),
        };
        shell.Notifications.Marker = "141";

        var opened = await shell.Opened();

        opened.Step(steps);
        shell.Host.Settle();

        Assert.Empty(shell.Notifications.MarkerMoves);
        Assert.Empty(shell.Messages.MarkedRead);
        Assert.Equal(1, opened.Rail.Destinations.Single(destination => destination.Kind == kind).Unread);
        Assert.Equal(3, Badge(opened).Unread);
    }

    private static IEnumerable<Notification> Ids(int count, int newest) =>
        Enumerable.Range(0, count).Select(offset => ANotification.With(id: $"{newest - offset}"));

    private static IEnumerable<string> Unread(Shell shell) =>
        Assert.IsType<NotificationsScreen>(shell.Screen).Notifications
              .Where(notification => Assert.IsType<NotificationsScreen>(shell.Screen).IsUnread(notification))
              .Select(notification => notification.Id);

    private static FakeNotificationInbox Holding(int count, int newest) =>
        FakeNotificationInbox.Holding(
            [.. Enumerable.Range(0, count).Select(offset => ANotification.With(id: $"{newest - offset}"))]);

    private static Destination Badge(Shell shell) =>
        shell.Rail.Destinations.Single(destination => destination.Kind == DestinationKind.Notifications);
}
