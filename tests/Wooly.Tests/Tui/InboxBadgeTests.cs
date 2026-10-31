using Wooly.Core.Notifications;
using Wooly.Tests.Fakes;
using Wooly.Tui.Screens;
using Wooly.Tui.Shell;

namespace Wooly.Tests.Tui;

/// <summary>
///     Every Inbox badge says what the instance says is waiting — its <b>Unread count</b> (GLOSSARY.md, ADR-0027):
///     notifications newer than the <b>Read marker</b>, conversations flagged unread, follow requests not yet answered.
///     Read the same way when the shell opens and when the reader arrives at the destination, so the two can never
///     disagree (#396).
/// </summary>
public class InboxBadgeTests
{
    /// <summary>Where the rail's notifications destination is, counting from Home.</summary>
    private const int ToNotifications = 5;

    /// <summary>
    ///     The badge that always said 40 (#396): an account with more notifications than a screen asks for, every one
    ///     of them read on the web, has nothing waiting.
    /// </summary>
    [Fact]
    public async Task Open_DrawsNoNotificationsBadgeWhereNothingIsNewerThanTheReadMarker()
    {
        var shell = new AShell { Notifications = Holding(count: 45, newest: 144) };
        shell.Notifications.Marker = "144";

        var opened = await shell.Opened();

        Assert.Equal(0, Badge(opened, DestinationKind.Notifications).Unread);
    }

    [Fact]
    public async Task Step_DrawsNoNotificationsBadgeWhereNothingIsNewerThanTheReadMarker()
    {
        var shell = new AShell { Notifications = Holding(count: 45, newest: 144) };
        shell.Notifications.Marker = "144";

        var opened = await shell.Opened();

        opened.Step(ToNotifications);
        shell.Host.Settle();

        Assert.Equal(0, Badge(opened, DestinationKind.Notifications).Unread);
    }

    [Fact]
    public async Task Open_CountsTheNotificationsNewerThanTheReadMarker()
    {
        var shell = new AShell { Notifications = Holding(count: 45, newest: 144) };
        shell.Notifications.Marker = "141";

        var opened = await shell.Opened();

        var badge = Badge(opened, DestinationKind.Notifications);
        Assert.Equal(3, badge.Unread);
        Assert.False(badge.Full);
    }

    /// <summary>
    ///     Arriving reads them and clears the badge (#397) — so the count it reads is seen where the instance turns
    ///     the marker's move down.
    /// </summary>
    [Fact]
    public async Task Step_CountsTheNotificationsNewerThanTheReadMarkerWhileListingThemAll()
    {
        var shell = new AShell { Notifications = Holding(count: 5, newest: 144) };
        shell.Notifications.Marker = "141";
        shell.Notifications.TurnsMarkerMovesDown = true;

        var opened = await shell.Opened();

        opened.Step(ToNotifications);
        shell.Host.Settle();

        Assert.Equal(5, Assert.IsType<NotificationsScreen>(opened.Screen).Groups.Count);
        Assert.Equal(3, Badge(opened, DestinationKind.Notifications).Unread);
    }

    /// <summary>
    ///     An account that has never had a marker has nothing this client can prove is unread, so it claims nothing —
    ///     on opening and on arriving alike.
    /// </summary>
    [Fact]
    public async Task Badge_SaysNothingWhereTheAccountHasNoReadMarker()
    {
        var shell = new AShell { Notifications = Holding(count: 5, newest: 144) };
        shell.Notifications.Marker = null;

        var opened = await shell.Opened();

        Assert.Equal(0, Badge(opened, DestinationKind.Notifications).Unread);

        opened.Step(ToNotifications);
        shell.Host.Settle();

        Assert.Equal(0, Badge(opened, DestinationKind.Notifications).Unread);
    }

    /// <summary>
    ///     With no marker, what arrives while the shell runs still counts (story 8): the newest notification at the
    ///     first read is where counting starts, and the instance's marker is left alone — the reader has looked at
    ///     nothing yet.
    /// </summary>
    [Fact]
    public async Task Badge_CountsWhatArrivesWhileTheShellRunsWhereTheAccountHasNoReadMarker()
    {
        var shell = new AShell { Notifications = Holding(count: 5, newest: 144) };
        shell.Notifications.Marker = null;

        var opened = await shell.Opened();

        shell.Notifications.NowHolding(
            [ANotification.With(id: "146"), ANotification.With(id: "145"), .. Enumerable.Range(0, 5).Select(offset => ANotification.With(id: $"{144 - offset}"))]);
        shell.Clock.Advance(shell.Timing.PollEvery);
        shell.Host.Drain();

        Assert.Equal(2, Badge(opened, DestinationKind.Notifications).Unread);
        Assert.Null(shell.Notifications.Marker);
        Assert.Empty(shell.Notifications.MarkerMoves);
    }

    /// <summary>
    ///     An account with no notifications at all and no marker counts everything that arrives after the shell
    ///     opened.
    /// </summary>
    [Fact]
    public async Task Badge_CountsWhatArrivesInAnEmptyInboxWithNoReadMarker()
    {
        var shell = new AShell { Notifications = FakeNotificationInbox.Holding() };
        shell.Notifications.Marker = null;

        var opened = await shell.Opened();

        shell.Notifications.NowHolding(ANotification.With(id: "7"));
        shell.Clock.Advance(shell.Timing.PollEvery);
        shell.Host.Drain();

        Assert.Equal(1, Badge(opened, DestinationKind.Notifications).Unread);
        Assert.Empty(shell.Notifications.MarkerMoves);
    }

    /// <summary>
    ///     More unread than one read counts is at least that many, and the badge says so rather than an exact number
    ///     that would be wrong. Seen on arriving where the marker's move is turned down, since a move clears it (#397).
    /// </summary>
    [Fact]
    public async Task Badge_IsFullWhereTheUnreadNotificationsFillTheRead()
    {
        var shell = new AShell { Notifications = Holding(count: 45, newest: 144) };
        shell.Notifications.TurnsMarkerMovesDown = true;

        var opened = await shell.Opened();

        AssertFull(Badge(opened, DestinationKind.Notifications));

        opened.Step(ToNotifications);
        shell.Host.Settle();

        AssertFull(Badge(opened, DestinationKind.Notifications));
    }

    [Fact]
    public async Task Badge_IsFullWhereEveryConversationReadIsUnread()
    {
        var shell = new AShell
        {
            Messages = FakeDirectMessages.Holding(
                [.. Enumerable.Range(0, 40).Select(n => AConversation.With(id: $"c{n}", unread: true))]),
        };

        var opened = await shell.Opened();

        AssertFull(Badge(opened, DestinationKind.Messages));

        opened.Step(ToNotifications + 1);
        shell.Host.Settle();

        AssertFull(Badge(opened, DestinationKind.Messages));
    }

    [Fact]
    public async Task Badge_IsFullWhereTheRequestsFillTheRead()
    {
        var shell = new AShell
        {
            Accounts = FakeAccountRelationships.Holding(
                subject: null,
                [.. Enumerable.Range(0, 40).Select(n => AnAccount.With(address: $"asker{n}@mas.to"))]),
        };

        var opened = await shell.Opened();

        AssertFull(Badge(opened, DestinationKind.Requests));

        opened.Step(ToNotifications + 2);
        shell.Host.Settle();

        AssertFull(Badge(opened, DestinationKind.Requests));
    }

    /// <summary>
    ///     A read notification taken off the list was not waiting, so the badge does not drop for it: the count is
    ///     the instance's, read again, rather than the badge less one. The marker's move is turned down, so that what
    ///     was unread on arriving is still unread beside the one dismissed (#397).
    /// </summary>
    [Fact]
    public async Task Dismiss_LeavesTheBadgeWhereTheNotificationWasAlreadyRead()
    {
        var shell = new AShell { Notifications = Holding(count: 3, newest: 144) };
        shell.Notifications.Marker = "142";
        shell.Notifications.TurnsMarkerMovesDown = true;

        var opened = await shell.Opened();

        opened.Step(ToNotifications);
        shell.Host.Settle();

        opened.Move(2);
        opened.Press(ShellKey.D);
        shell.Host.Drain();

        Assert.Equal("ungrouped-142", Assert.Single(shell.Notifications.GroupDismissals).Key);
        Assert.Equal(2, Badge(opened, DestinationKind.Notifications).Unread);
    }

    private static void AssertFull(Destination badge)
    {
        Assert.Equal(40, badge.Unread);
        Assert.True(badge.Full);
    }

    private static FakeNotificationInbox Holding(int count, int newest) =>
        FakeNotificationInbox.Holding(
            [.. Enumerable.Range(0, count).Select(offset => ANotification.With(id: $"{newest - offset}"))]);

    private static Destination Badge(Shell shell, DestinationKind kind) =>
        shell.Rail.Destinations.Single(destination => destination.Kind == kind);
}
