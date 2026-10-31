using System.Net;
using Wooly.Core.Errors;
using Wooly.Core.Notifications;
using Wooly.Core.Paging;
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
    private const int ToNotifications = 5;

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
    ///     A poll with Notifications not in front only counts: the groups are read for the screen that draws them, and
    ///     nothing else does (#458).
    /// </summary>
    [Fact]
    public async Task ElsewhereOnTheRail_APollReadsNoNotificationGroups()
    {
        var shell = new AShell { Notifications = FakeNotificationInbox.Holding(ANotification.With(id: "102")) };
        shell.Notifications.Marker = "101";
        var opened = await shell.Opened();
        var reads = shell.Notifications.GroupReads.Count;

        shell.Notifications.NowHolding(ANotification.With(id: "103"), ANotification.With(id: "102"));
        Passes(shell, Minute);

        Assert.Equal(reads, shell.Notifications.GroupReads.Count);
        Assert.Equal(2, Badge(opened, DestinationKind.Notifications).Unread);
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

    /// <summary>
    ///     A notification arriving for a group already on screen joins it (#458): the group's count, actors and newest
    ///     come from the re-read, it is drawn unread, and it goes to the top — one row, not a second one beside it.
    /// </summary>
    [Fact]
    public async Task OnNotifications_ANewFavoriteJoinsItsGroupOnScreenAndRaisesIt()
    {
        var (shell, opened) = await OnGroups(
            ANotification.Group(key: "follow-493000", kind: NotificationKind.Follow, newestId: "102", noPost: true),
            ANotification.Group(key: "favourite-110-493000", newestId: "101", actors: ["Alice"]));

        Arrives(
            shell,
            ANotification.Group(key: "favourite-110-493000", count: 2, newestId: "103", actors: ["Bob", "Alice"]),
            ANotification.Group(key: "follow-493000", kind: NotificationKind.Follow, newestId: "102", noPost: true));

        var screen = Assert.IsType<NotificationsScreen>(opened.Screen);
        Assert.Equal(["favourite-110-493000", "follow-493000"], screen.Groups.Select(group => group.Key));
        Assert.Equal(2, screen.Groups[0].Count);
        Assert.Equal(["Bob", "Alice"], screen.Groups[0].Actors.Select(actor => actor.Author));
        Assert.Equal("103", screen.Groups[0].NewestId);
        Assert.True(screen.IsUnread(screen.Groups[0]));
        Assert.False(screen.IsUnread(screen.Groups[1]));
        Assert.Equal("103", shell.Notifications.MarkerMoves[^1].NotificationId);
    }

    /// <summary>A notification for a group that is not on screen is a new group, put on top unread.</summary>
    [Fact]
    public async Task OnNotifications_ANotificationForAGroupNotOnScreenIsAddedAtTheTop()
    {
        var (shell, opened) = await OnGroups(
            ANotification.Group(key: "favourite-110-493000", newestId: "102", actors: ["Alice"]));

        Arrives(
            shell,
            ANotification.Group(key: "reblog-110-493000", kind: NotificationKind.Boost, newestId: "103", actors: ["Bob"]),
            ANotification.Group(key: "favourite-110-493000", newestId: "102", actors: ["Alice"]));

        var screen = Assert.IsType<NotificationsScreen>(opened.Screen);
        Assert.Equal(["reblog-110-493000", "favourite-110-493000"], screen.Groups.Select(group => group.Key));
        Assert.True(screen.IsUnread(screen.Groups[0]));
    }

    /// <summary>
    ///     A group the reader had read turns unread when a new actor joins it, so the unread mark always means
    ///     something new arrived (story 17 of #448).
    /// </summary>
    [Fact]
    public async Task OnNotifications_AReadGroupTurnsUnreadWhenANewActorJoinsIt()
    {
        var (shell, opened) = await OnGroups(
            ANotification.Group(key: "favourite-110-493000", newestId: "102", actors: ["Alice"]));
        var screen = Assert.IsType<NotificationsScreen>(opened.Screen);
        Assert.False(screen.IsUnread(screen.Groups[0]));

        Arrives(
            shell,
            ANotification.Group(key: "favourite-110-493000", count: 2, newestId: "103", actors: ["Bob", "Alice"]));

        Assert.Single(screen.Groups);
        Assert.True(screen.IsUnread(screen.Groups[0]));
    }

    /// <summary>The pick stays on the group it was on while another moves above it.</summary>
    [Fact]
    public async Task OnNotifications_ThePickStaysOnItsGroupWhenAnotherJoinsAndRises()
    {
        var (shell, opened) = await OnGroups(
            ANotification.Group(key: "follow-493000", kind: NotificationKind.Follow, newestId: "102", noPost: true),
            ANotification.Group(key: "favourite-110-493000", newestId: "101", actors: ["Alice"]));

        Arrives(
            shell,
            ANotification.Group(key: "favourite-110-493000", count: 2, newestId: "103", actors: ["Bob", "Alice"]),
            ANotification.Group(key: "follow-493000", kind: NotificationKind.Follow, newestId: "102", noPost: true));

        var screen = Assert.IsType<NotificationsScreen>(opened.Screen);
        Assert.Equal("follow-493000", screen.PickedGroup?.Key);
        Assert.Equal(1, screen.At);
    }

    /// <summary>
    ///     The group joined is the group picked: it moves to the top like any other, and the pick goes with it — the
    ///     pick follows the group it was on (#458), so the screen reads as a fresh read would.
    /// </summary>
    [Fact]
    public async Task OnNotifications_ThePickedGroupJoinedMovesToTheTopAndThePickGoesWithIt()
    {
        var (shell, opened) = await OnGroups(
            ANotification.Group(key: "follow-493000", kind: NotificationKind.Follow, newestId: "102", noPost: true),
            ANotification.Group(key: "favourite-110-493000", newestId: "101", actors: ["Alice"]));
        opened.Screen.Move(1);

        Arrives(
            shell,
            ANotification.Group(key: "favourite-110-493000", count: 2, newestId: "103", actors: ["Bob", "Alice"]),
            ANotification.Group(key: "follow-493000", kind: NotificationKind.Follow, newestId: "102", noPost: true));

        var screen = Assert.IsType<NotificationsScreen>(opened.Screen);
        Assert.Equal(["favourite-110-493000", "follow-493000"], screen.Groups.Select(group => group.Key));
        Assert.Equal("favourite-110-493000", screen.PickedGroup?.Key);
        Assert.Equal(0, screen.At);
        Assert.Equal(2, screen.PickedGroup?.Count);
        Assert.True(screen.IsUnread(screen.PickedGroup!));
    }

    /// <summary>
    ///     Arrival after arrival, the picked group joined in some and others raised past it, the list stays newest
    ///     first — as a fresh read would draw it — and the pick stays on its group throughout.
    /// </summary>
    [Fact]
    public async Task OnNotifications_RepeatedArrivalsKeepTheListNewestFirst()
    {
        var (shell, opened) = await OnGroups(
            ANotification.Group(key: "follow-493000", kind: NotificationKind.Follow, newestId: "103", noPost: true),
            ANotification.Group(key: "reblog-110-493000", kind: NotificationKind.Boost, newestId: "102", actors: ["Carol"]),
            ANotification.Group(key: "favourite-110-493000", newestId: "101", actors: ["Alice"]));
        opened.Screen.Move(2);

        Arrives(
            shell,
            ANotification.Group(key: "favourite-110-493000", count: 2, newestId: "104", actors: ["Bob", "Alice"]),
            ANotification.Group(key: "follow-493000", kind: NotificationKind.Follow, newestId: "103", noPost: true),
            ANotification.Group(key: "reblog-110-493000", kind: NotificationKind.Boost, newestId: "102", actors: ["Carol"]));
        Arrives(
            shell,
            ANotification.Group(key: "reblog-110-493000", kind: NotificationKind.Boost, count: 2, newestId: "105", actors: ["Dan", "Carol"]),
            ANotification.Group(key: "favourite-110-493000", count: 2, newestId: "104", actors: ["Bob", "Alice"]),
            ANotification.Group(key: "follow-493000", kind: NotificationKind.Follow, newestId: "103", noPost: true));
        Arrives(
            shell,
            ANotification.Group(key: "favourite-110-493000", count: 3, newestId: "107", actors: ["Eve", "Bob", "Alice"]),
            ANotification.Group(key: "mention-106", kind: NotificationKind.Mention, newestId: "106", actors: ["Fay"]),
            ANotification.Group(key: "reblog-110-493000", kind: NotificationKind.Boost, count: 2, newestId: "105", actors: ["Dan", "Carol"]),
            ANotification.Group(key: "follow-493000", kind: NotificationKind.Follow, newestId: "103", noPost: true));

        var screen = Assert.IsType<NotificationsScreen>(opened.Screen);
        Assert.Equal(["107", "106", "105", "103"], screen.Groups.Select(group => group.NewestId));
        Assert.Equal("favourite-110-493000", screen.PickedGroup?.Key);
        Assert.Equal(3, screen.PickedGroup?.Count);
    }

    /// <summary>
    ///     A count begun elsewhere on the rail reads no groups. Arriving at Notifications counts again and makes it out
    ///     of date — but where that count could not be read, the old one lands on the screen with something unread.
    ///     What it found is never made into groups of one here: on an instance that groups, an
    ///     <c>ungrouped-&lt;id&gt;</c> group for a favorite is a group the instance does not have, and <c>d</c> on it
    ///     would dismiss nothing. The count is read again, groups and all, and the favorite joins its real group.
    /// </summary>
    [Fact]
    public async Task OnNotifications_ACountBegunElsewhereReadsTheGroupsAgainRatherThanMakingUpGroupsOfOne()
    {
        var favorite = ANotification.Group(key: "favourite-110-493000", newestId: "101", actors: ["Alice"]);
        var shell = new AShell { Notifications = FakeNotificationInbox.Holding(ANotification.With(id: "101")) };
        shell.Notifications.Groups = [favorite];
        shell.Notifications.Marker = "101";
        var opened = await shell.Opened();

        // The poll's count of what is unread waits, begun on Home; arriving's own count fails; every count after
        // them answers at once.
        var late = new TaskCompletionSource();
        var counts = 0;
        Notification[] holding = [ANotification.With(id: "101")];
        shell.Notifications.ByProfile["personal"] = async () =>
        {
            switch (counts++)
            {
                case 0:
                    await late.Task;

                    break;

                case 1:
                    throw new InstanceFailedException("mastodon.social", HttpStatusCode.BadGateway);
            }

            return Fetch<Notification>.Complete(holding);
        };
        Passes(shell, Minute);

        opened.Step(ToNotifications);
        shell.Host.Settle();

        holding = [ANotification.With(id: "103"), ANotification.With(id: "101")];
        shell.Notifications.NowHolding(holding);
        shell.Notifications.Groups =
            [ANotification.Group(key: "favourite-110-493000", count: 2, newestId: "103", actors: ["Bob", "Alice"])];
        shell.Notifications.Members["favourite-110-493000"] = ["103", "101"];
        late.SetResult();
        shell.Host.Drain();

        var screen = Assert.IsType<NotificationsScreen>(opened.Screen);
        Assert.Equal(["favourite-110-493000"], screen.Groups.Select(group => group.Key));
        Assert.Equal(2, screen.Groups[0].Count);
        Assert.True(screen.IsUnread(screen.Groups[0]));

        opened.Press(ShellKey.D);
        shell.Host.Drain();

        Assert.Equal("favourite-110-493000", Assert.Single(shell.Notifications.GroupDismissals).Key);
    }

    /// <summary>
    ///     Notifications open on <paramref name="groups" />, every one of them read: the read marker is at the newest,
    ///     and each group's newest notification is held so the unread count can see past it.
    /// </summary>
    private static async Task<(AShell Shell, Shell Opened)> OnGroups(params NotificationGroup[] groups)
    {
        var shell = new AShell
        {
            Notifications = FakeNotificationInbox.Holding([.. groups.Select(Newest)]),
        };
        shell.Notifications.Groups = groups;
        shell.Notifications.Marker = groups.Max(group => long.Parse(group.NewestId)).ToString();
        var opened = await shell.Opened();

        opened.Step(ToNotifications);
        shell.Host.Settle();

        return (shell, opened);
    }

    /// <summary>
    ///     The instance now groups what it holds as <paramref name="groups" />, and a poll re-reads it — as a stream
    ///     event would.
    /// </summary>
    private static void Arrives(AShell shell, params NotificationGroup[] groups)
    {
        shell.Notifications.NowHolding([.. groups.Select(Newest)]);
        shell.Notifications.Groups = groups;
        Passes(shell, Minute);
    }

    /// <summary>The newest notification in <paramref name="group" />, as the unread count reads it.</summary>
    private static Notification Newest(NotificationGroup group) =>
        ANotification.With(id: group.NewestId);

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
