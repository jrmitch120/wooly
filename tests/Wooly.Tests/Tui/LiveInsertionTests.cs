using Wooly.Core.Notifications;
using Wooly.Tests.Fakes;
using Wooly.Tui.Rendering;
using Wooly.Tui.Screens;
using Wooly.Tui.Shell;
using Wooly.Tui.Theme;

namespace Wooly.Tests.Tui;

/// <summary>
///     A re-read that finds something new for the <b>Inbox</b> screen in front puts it at the top of that screen, without
///     the picked row moving — and on Notifications moves the <b>Read marker</b> past it, so the badge stays clear while
///     the reader looks, drawing it unread for the visit (GLOSSARY.md, ADR-0027, #400). Driven by the poll here; a stream
///     event re-reads through the same path.
/// </summary>
public class LiveInsertionTests
{
    /// <summary>Where the rail's notifications destination is, counting from Home; Messages and Requests follow it.</summary>
    private const int ToNotifications = 6;

    private static readonly TimeSpan Minute = TimeSpan.FromSeconds(60);

    [Fact]
    public async Task OnNotifications_APollFindingANewOnePutsItAtTheTopUnreadAndMovesTheMarkerPastIt()
    {
        var shell = new AShell
        {
            Notifications = FakeNotificationInbox.Holding(
                ANotification.With(id: "102"),
                ANotification.With(id: "101")),
        };
        shell.Notifications.Marker = "102";
        var opened = await shell.Opened();

        opened.Step(ToNotifications);
        shell.Host.Settle();

        shell.Notifications.NowHolding(
            ANotification.With(id: "103"),
            ANotification.With(id: "102"),
            ANotification.With(id: "101"));
        Passes(shell, Minute);

        var screen = Assert.IsType<NotificationsScreen>(opened.Screen);
        Assert.Equal(["103", "102", "101"], screen.Groups.Select(group => group.NewestId));
        Assert.True(screen.IsUnread(screen.Groups[0]));
        Assert.Equal("103", shell.Notifications.MarkerMoves[^1].NotificationId);
        Assert.Equal(0, Badge(opened, DestinationKind.Notifications).Unread);
    }

    [Fact]
    public async Task OnNotifications_ThePickedRowStaysTheSameNotification()
    {
        var shell = new AShell
        {
            Notifications = FakeNotificationInbox.Holding(
                ANotification.With(id: "102"),
                ANotification.With(id: "101")),
        };
        shell.Notifications.Marker = "102";
        var opened = await shell.Opened();

        opened.Step(ToNotifications);
        shell.Host.Settle();
        opened.Screen.Move(1);

        shell.Notifications.NowHolding(
            ANotification.With(id: "104"),
            ANotification.With(id: "103"),
            ANotification.With(id: "102"),
            ANotification.With(id: "101"));
        Passes(shell, Minute);

        var screen = Assert.IsType<NotificationsScreen>(opened.Screen);
        Assert.Equal("101", screen.PickedGroup?.NewestId);
        Assert.Equal(3, screen.At);
    }

    /// <summary>
    ///     A re-read that came back full may have more unread past what it read, which are on no screen: the marker is
    ///     left where it was rather than moved past them unseen, and the badge goes on saying at least that many.
    /// </summary>
    [Fact]
    public async Task OnNotifications_AFullPollPutsWhatItReadOnTopButLeavesTheMarker()
    {
        var shell = new AShell { Notifications = FakeNotificationInbox.Holding(ANotification.With(id: "100")) };
        shell.Notifications.Marker = "100";
        var opened = await shell.Opened();

        opened.Step(ToNotifications);
        shell.Host.Settle();
        var moves = shell.Notifications.MarkerMoves.Count;

        shell.Notifications.NowHolding(
            [.. Enumerable.Range(0, 45).Select(offset => ANotification.With(id: $"{145 - offset}")),
                ANotification.With(id: "100")]);
        Passes(shell, Minute);

        var screen = Assert.IsType<NotificationsScreen>(opened.Screen);
        Assert.Equal(41, screen.Groups.Count);
        Assert.Equal("145", screen.Groups[0].NewestId);
        Assert.Equal(moves, shell.Notifications.MarkerMoves.Count);
        Assert.Equal("100", shell.Notifications.Marker);
        Assert.True(Badge(opened, DestinationKind.Notifications).Full);
    }

    /// <summary>
    ///     A re-read that read everything unread — as many as it counts up to, and no more past them — put all of it
    ///     on screen, so the marker moves past it and the badge clears, as for any re-read that is not full.
    /// </summary>
    [Fact]
    public async Task OnNotifications_APollReadingExactlyAsManyAsItCountsMovesTheMarkerPastThemAll()
    {
        var shell = new AShell { Notifications = FakeNotificationInbox.Holding(ANotification.With(id: "100")) };
        shell.Notifications.Marker = "100";
        var opened = await shell.Opened();

        opened.Step(ToNotifications);
        shell.Host.Settle();

        shell.Notifications.NowHolding(
            [.. Enumerable.Range(0, 40).Select(offset => ANotification.With(id: $"{140 - offset}")),
                ANotification.With(id: "100")]);
        Passes(shell, Minute);

        var screen = Assert.IsType<NotificationsScreen>(opened.Screen);
        Assert.Equal(41, screen.Groups.Count);
        Assert.Equal("140", shell.Notifications.Marker);
        Assert.Equal(0, Badge(opened, DestinationKind.Notifications).Unread);
        Assert.False(Badge(opened, DestinationKind.Notifications).Full);
    }

    /// <summary>
    ///     A re-read landing while the arrival at Notifications is still reading puts nothing on the screen waiting for
    ///     it and moves no marker: the arrival is about to draw the list and read the marker itself (story 11). What it
    ///     found is not lost for that — the arrival's list was read before it came, so it is put on top once the list
    ///     is drawn.
    /// </summary>
    [Fact]
    public async Task OnNotifications_APollLandingWhileTheArrivalIsReadingLeavesTheArrivalToIt()
    {
        var shell = new AShell
        {
            Notifications = FakeNotificationInbox.Holding(
                ANotification.With(id: "102"),
                ANotification.With(id: "101")),
        };
        shell.Notifications.Marker = "102";
        var opened = await shell.Opened();
        var arriving = new TaskCompletionSource();
        shell.Notifications.ReadsWaitFor = arriving;

        opened.Step(ToNotifications);
        shell.Host.Settle();

        shell.Notifications.NowHolding(
            ANotification.With(id: "103"),
            ANotification.With(id: "102"),
            ANotification.With(id: "101"));
        Passes(shell, Minute);

        Assert.Empty(Assert.IsType<NotificationsScreen>(opened.Screen).Groups);
        Assert.Empty(shell.Notifications.MarkerMoves);

        arriving.SetResult();
        shell.Host.Settle();

        var screen = Assert.IsType<NotificationsScreen>(opened.Screen);
        Assert.Equal(["103", "102", "101"], screen.Groups.Select(group => group.NewestId));
        Assert.True(screen.IsUnread(screen.Groups[0]));
        Assert.Equal("103", Assert.Single(shell.Notifications.MarkerMoves).NotificationId);
    }

    /// <summary>
    ///     Only the destination's own list read holds a re-read back: one landing while something else is in flight —
    ///     a dismiss here — is put on screen as ever (stories 20–23).
    /// </summary>
    [Fact]
    public async Task OnNotifications_APollLandingWhileADismissIsInFlightPutsWhatItFoundOnTop()
    {
        var shell = new AShell
        {
            Notifications = FakeNotificationInbox.Holding(
                ANotification.With(id: "102"),
                ANotification.With(id: "101")),
        };
        shell.Notifications.Marker = "102";
        var opened = await shell.Opened();

        opened.Step(ToNotifications);
        shell.Host.Settle();

        shell.Notifications.DismissalsWaitFor = new TaskCompletionSource();
        opened.Move(1);
        opened.Press(ShellKey.D);
        shell.Host.Drain();
        Assert.True(opened.Fetching);

        shell.Notifications.NowHolding(
            ANotification.With(id: "103"),
            ANotification.With(id: "102"),
            ANotification.With(id: "101"));
        Passes(shell, Minute);

        var screen = Assert.IsType<NotificationsScreen>(opened.Screen);
        Assert.Equal("103", screen.Groups[0].NewestId);
        Assert.True(screen.IsUnread(screen.Groups[0]));
        Assert.Equal("103", shell.Notifications.MarkerMoves[^1].NotificationId);
    }

    /// <summary>
    ///     A conversation new to the screen, or one spoken in since, goes to the top — the changed one moved rather
    ///     than drawn twice — with its unread flag as the instance has it.
    /// </summary>
    [Fact]
    public async Task OnMessages_APollFindingANewOrChangedConversationPutsItAtTheTop()
    {
        var shell = new AShell
        {
            Messages = FakeDirectMessages.Holding(
                AConversation.With(id: "c2", unread: false),
                AConversation.With(id: "c1", unread: false)),
        };
        var opened = await shell.Opened();

        opened.Step(ToNotifications + 1);
        shell.Host.Settle();

        shell.Messages.NowHolding(
            AConversation.With(id: "c3", unread: true),
            AConversation.With(id: "c1", unread: true),
            AConversation.With(id: "c2", unread: false));
        Passes(shell, Minute);

        var screen = Assert.IsType<DirectMessagesScreen>(opened.Screen);
        Assert.Equal(["c3", "c1", "c2"], screen.Conversations.Select(conversation => conversation.Id));
        Assert.Equal([true, true, false], screen.Conversations.Select(conversation => conversation.Unread));
        Assert.Equal("c2", screen.PickedConversation?.Id);
    }

    /// <summary>
    ///     The conversation the reader is on changing is no reason for it to jump to the top (story 24): it is
    ///     brought up to date where it stands, and what is new goes in above it as usual.
    /// </summary>
    [Fact]
    public async Task OnMessages_ThePickedConversationChangingIsUpdatedWhereItStands()
    {
        var shell = new AShell
        {
            Messages = FakeDirectMessages.Holding(
                AConversation.With(id: "c2", unread: false),
                AConversation.With(id: "c1", unread: false)),
        };
        var opened = await shell.Opened();

        opened.Step(ToNotifications + 1);
        shell.Host.Settle();
        opened.Screen.Move(1);

        shell.Messages.NowHolding(
            AConversation.With(id: "c3", unread: true),
            AConversation.With(id: "c1", unread: true),
            AConversation.With(id: "c2", unread: false));
        Passes(shell, Minute);

        var screen = Assert.IsType<DirectMessagesScreen>(opened.Screen);
        Assert.Equal(["c3", "c2", "c1"], screen.Conversations.Select(conversation => conversation.Id));
        Assert.Equal([true, false, true], screen.Conversations.Select(conversation => conversation.Unread));
        Assert.Equal("c1", screen.PickedConversation?.Id);
        Assert.Equal(2, screen.At);
    }

    [Fact]
    public async Task OnRequests_APollFindingANewRequestPutsItAtTheTop()
    {
        var shell = new AShell
        {
            Accounts = FakeAccountRelationships.Holding(
                subject: null,
                AnAccount.With(id: "a2", address: "two@mas.to"),
                AnAccount.With(id: "a1", address: "one@mas.to")),
        };
        var opened = await shell.Opened();

        opened.Step(ToNotifications + 2);
        shell.Host.Settle();
        opened.Screen.Move(1);

        shell.Accounts.NowListing(
            AnAccount.With(id: "a3", address: "three@mas.to"),
            AnAccount.With(id: "a2", address: "two@mas.to"),
            AnAccount.With(id: "a1", address: "one@mas.to"));
        Passes(shell, Minute);

        var screen = Assert.IsType<FollowRequestsScreen>(opened.Screen);
        Assert.Equal(["a3", "a2", "a1"], screen.Waiting.Select(account => account.Id));
        Assert.Equal("a1", screen.PickedAccount?.Id);
    }

    /// <summary>
    ///     A request put on top is read the way arriving reads one — with whether the reader already follows the person
    ///     asking, which may be the most useful thing on the row (#204).
    /// </summary>
    [Fact]
    public async Task OnRequests_ARequestAPollPutsAtTheTopSaysWhetherYouFollowThePersonAsking()
    {
        var shell = new AShell
        {
            Accounts = FakeAccountRelationships.Holding(subject: null, AnAccount.With(id: "a1", address: "one@mas.to")),
        };
        shell.Accounts.Stands = AnAccount.Standing(following: true);
        var opened = await shell.Opened();

        opened.Step(ToNotifications + 2);
        shell.Host.Settle();

        shell.Accounts.NowListing(
            AnAccount.With(id: "a2", address: "two@mas.to"),
            AnAccount.With(id: "a1", address: "one@mas.to"));
        Passes(shell, Minute);

        var screen = Assert.IsType<FollowRequestsScreen>(opened.Screen);
        Assert.Equal("a2", screen.Waiting[0].Id);
        Assert.True(screen.Waiting[0].Standing?.Following);
    }

    /// <summary>
    ///     A screen that is not in front is told nothing: the poll only counts, and the marker stays where arriving
    ///     put it.
    /// </summary>
    [Fact]
    public async Task ElsewhereOnTheRail_APollPutsNothingOnTheNotificationsListAndLeavesTheMarker()
    {
        var shell = new AShell { Notifications = FakeNotificationInbox.Holding(ANotification.With(id: "102")) };
        shell.Notifications.Marker = "101";
        var opened = await shell.Opened();

        opened.Step(ToNotifications);
        shell.Host.Settle();
        var left = Assert.IsType<NotificationsScreen>(opened.Screen);
        opened.Step(-1);
        shell.Host.Settle();
        var moves = shell.Notifications.MarkerMoves.Count;

        shell.Notifications.NowHolding(ANotification.With(id: "103"), ANotification.With(id: "102"));
        Passes(shell, Minute);

        Assert.Equal(["102"], left.Groups.Select(group => group.NewestId));
        Assert.Equal(moves, shell.Notifications.MarkerMoves.Count);
        Assert.Equal(1, Badge(opened, DestinationKind.Notifications).Unread);
    }

    /// <summary>
    ///     Coming back inside the cache window after a re-read raised the count reads the list again, rather than
    ///     drawing the list held from before it and putting that visit's cleared badge back over what arrived since.
    /// </summary>
    [Fact]
    public async Task ComingBackAfterAPollRaisedTheCount_ReadsTheListAgainRatherThanTheHeldOne()
    {
        // Held longer than a poll takes to come round, so that the poll lands inside the window.
        var shell = new AShell
        {
            Notifications = FakeNotificationInbox.Holding(ANotification.With(id: "102")),
            Timing = ShellTiming.Default with { CacheFor = TimeSpan.FromMinutes(5) },
        };
        shell.Notifications.Marker = "101";
        var opened = await shell.Opened();

        opened.Step(ToNotifications);
        shell.Host.Settle();
        opened.Step(-1);
        shell.Host.Settle();

        shell.Notifications.NowHolding(ANotification.With(id: "103"), ANotification.With(id: "102"));
        Passes(shell, Minute);

        opened.Step(1);
        shell.Host.Settle();

        var screen = Assert.IsType<NotificationsScreen>(opened.Screen);
        Assert.Equal(["103", "102"], screen.Groups.Select(group => group.NewestId));
        Assert.True(screen.IsUnread(screen.Groups[0]));
        Assert.Equal("103", shell.Notifications.MarkerMoves[^1].NotificationId);
    }

    /// <summary>
    ///     Nothing jumps: what arrived goes in above, and the page moves with it, so the picked row is drawn on the
    ///     same row of the terminal as before.
    /// </summary>
    [Fact]
    public async Task OnNotifications_ThePickedRowIsDrawnWhereItWasBefore()
    {
        var built = new AShell
        {
            Notifications = FakeNotificationInbox.Holding(
                ANotification.With(id: "102", author: "Bertha"),
                ANotification.With(id: "101", author: "Cuthbert")),
        };
        built.Notifications.Marker = "102";
        using var drawn = await DrawnShell.Of(100, 40, Themes.Plain, built);

        drawn.Shell.Step(ToNotifications);
        built.Host.Settle();
        drawn.Redraw();
        drawn.Shell.Screen.Move(1);
        drawn.Redraw();
        var before = RowOf(drawn, "Cuthbert");

        built.Notifications.NowHolding(
            ANotification.With(id: "103", author: "Ottoline"),
            ANotification.With(id: "102", author: "Bertha"),
            ANotification.With(id: "101", author: "Cuthbert"));
        built.Clock.Advance(Minute);
        drawn.Settle();

        Assert.Equal("101", Assert.IsType<NotificationsScreen>(drawn.Shell.Screen).PickedGroup?.NewestId);
        Assert.Equal(before, RowOf(drawn, "Cuthbert"));
    }

    /// <summary>The terminal row the first mention of <paramref name="author" /> is drawn on.</summary>
    private static int RowOf(DrawnShell drawn, string author) =>
        Array.FindIndex(drawn.Rows(), row => row.Contains($"{author} mentioned you", StringComparison.Ordinal));

    /// <summary>Moves the shell's clock on by <paramref name="howLong" />, and lets whatever that asked for land.</summary>
    private static void Passes(AShell shell, TimeSpan howLong)
    {
        shell.Clock.Advance(howLong);
        shell.Host.Drain();
    }

    private static Destination Badge(Shell shell, DestinationKind kind) =>
        shell.Rail.Destinations.Single(destination => destination.Kind == kind);
}
