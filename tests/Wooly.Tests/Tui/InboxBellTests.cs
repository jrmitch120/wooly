using Wooly.Core.Notifications;
using Wooly.Core.Paging;
using Wooly.Tests.Fakes;
using Wooly.Tui.Screens;
using Wooly.Tui.Shell;

namespace Wooly.Tests.Tui;

/// <summary>
///     The shell writes the terminal's bell once whenever a re-read raises an Inbox badge above the last count of the
///     same profile (#399) — never for the counts a profile is first read with, and not at all where the preference
///     turns it off.
/// </summary>
public class InboxBellTests
{
    private static readonly TimeSpan Minute = TimeSpan.FromSeconds(60);

    /// <summary>Where the rail's direct messages destination is, counting from Home.</summary>
    private const int ToMessages = 7;

    /// <summary>Where the rail's follow requests destination is, counting from Home.</summary>
    private const int ToRequests = 8;

    [Fact]
    public async Task APollRaisingTheNotificationsBadge_RingsOnce()
    {
        var shell = OneUnreadNotification();
        await shell.Opened();

        shell.Notifications.NowHolding(ANotification.With(id: "102"), ANotification.With(id: "101"));
        Passes(shell, Minute);

        Assert.Equal(1, shell.Host.Bells);
    }

    [Fact]
    public async Task APollRaisingTheMessagesBadge_RingsOnce()
    {
        var shell = new AShell { Messages = FakeDirectMessages.Holding(AConversation.With(id: "c1", unread: true)) };
        await shell.Opened();

        shell.Messages.NowHolding(
            AConversation.With(id: "c2", unread: true),
            AConversation.With(id: "c1", unread: true));
        Passes(shell, Minute);

        Assert.Equal(1, shell.Host.Bells);
    }

    [Fact]
    public async Task APollRaisingTheRequestsBadge_RingsOnce()
    {
        var shell = new AShell
        {
            Accounts = FakeAccountRelationships.Holding(subject: null, AnAccount.With(address: "asker@mas.to")),
        };
        await shell.Opened();

        shell.Accounts.NowListing(AnAccount.With(address: "asker@mas.to"), AnAccount.With(address: "other@mas.to"));
        Passes(shell, Minute);

        Assert.Equal(1, shell.Host.Bells);
    }

    /// <summary>One re-read is one sound, however many badges it raised.</summary>
    [Fact]
    public async Task APollRaisingTwoBadges_RingsOnce()
    {
        var shell = OneUnreadNotification();
        shell.Messages = FakeDirectMessages.Holding(AConversation.With(id: "c1", unread: true));
        await shell.Opened();

        shell.Notifications.NowHolding(ANotification.With(id: "102"), ANotification.With(id: "101"));
        shell.Messages.NowHolding(
            AConversation.With(id: "c2", unread: true),
            AConversation.With(id: "c1", unread: true));
        Passes(shell, Minute);

        Assert.Equal(1, shell.Host.Bells);
    }

    /// <summary>Each poll that raises a badge is its own sound.</summary>
    [Fact]
    public async Task TwoPollsEachRaisingABadge_RingTwice()
    {
        var shell = OneUnreadNotification();
        await shell.Opened();

        shell.Notifications.NowHolding(ANotification.With(id: "102"), ANotification.With(id: "101"));
        Passes(shell, Minute);
        shell.Notifications.NowHolding(
            ANotification.With(id: "103"),
            ANotification.With(id: "102"),
            ANotification.With(id: "101"));
        Passes(shell, Minute);

        Assert.Equal(2, shell.Host.Bells);
    }

    [Fact]
    public async Task APollLeavingTheBadgesEqualOrLower_RingsNothing()
    {
        var shell = new AShell
        {
            Notifications = FakeNotificationInbox.Holding(ANotification.With(id: "102"), ANotification.With(id: "101")),
            Messages = FakeDirectMessages.Holding(AConversation.With(id: "c1", unread: true)),
        };
        shell.Notifications.Marker = "100";
        await shell.Opened();

        shell.Notifications.Marker = "101";
        Passes(shell, Minute);
        Passes(shell, Minute);

        Assert.Equal(0, shell.Host.Bells);
    }

    /// <summary>Starting Wooly is not an arrival, however much is waiting.</summary>
    [Fact]
    public async Task TheCountsReadWhenTheShellOpens_RingNothing()
    {
        var shell = OneUnreadNotification();
        shell.Messages = FakeDirectMessages.Holding(AConversation.With(id: "c1", unread: true));

        await shell.Opened();

        Assert.Equal(0, shell.Host.Bells);
    }

    /// <summary>
    ///     A switch is not an arrival either, though the new profile's counts are higher than the old one's — and its
    ///     own next rise rings as the old one's would have.
    /// </summary>
    [Fact]
    public async Task TheCountsReadAfterASwitch_RingNothing_AndTheNewProfilesNextRiseRings()
    {
        var shell = PersonalAndWork();
        shell.Notifications.ByProfile["work"] = () => Task.FromResult(Fetch<Notification>.Complete(
            [ANotification.With(id: "3"), ANotification.With(id: "2"), ANotification.With(id: "1")]));
        var opened = await shell.Opened();

        SwitchTo(shell, opened, "work");

        Assert.Equal(3, Badge(opened, DestinationKind.Notifications).Unread);
        Assert.Equal(0, shell.Host.Bells);

        shell.Notifications.ByProfile["work"] = () => Task.FromResult(Fetch<Notification>.Complete(
            [ANotification.With(id: "4"), ANotification.With(id: "3"), ANotification.With(id: "2"), ANotification.With(id: "1")]));
        Passes(shell, Minute);

        Assert.Equal(1, shell.Host.Bells);
    }

    /// <summary>The reader on the destination may not be looking at the screen, so the bell rings anyway.</summary>
    [Fact]
    public async Task OnTheDestinationWhoseBadgeRose_ItStillRings()
    {
        var shell = new AShell { Messages = FakeDirectMessages.Holding(AConversation.With(id: "c1", unread: true)) };
        var opened = await shell.Opened();
        opened.Step(ToMessages);
        shell.Host.Settle();
        Assert.IsType<DirectMessagesScreen>(opened.Screen);

        shell.Messages.NowHolding(
            AConversation.With(id: "c2", unread: true),
            AConversation.With(id: "c1", unread: true));
        Passes(shell, Minute);

        Assert.Equal(1, shell.Host.Bells);
    }

    /// <summary>
    ///     A badge already at <c>40+</c> cannot count higher, but something newer arriving is still a rise — so it rings,
    ///     on the destination as anywhere, once per re-read; a re-read finding nothing newer rings nothing (story 27).
    /// </summary>
    [Fact]
    public async Task OnNotificationsWithTheBadgeFull_SomethingNewerArrivingStillRings()
    {
        var shell = new AShell { Notifications = FakeNotificationInbox.Holding(ANotification.With(id: "100")) };
        shell.Notifications.Marker = "100";
        var opened = await shell.Opened();
        opened.Step(ToMessages - 1);
        shell.Host.Settle();

        shell.Notifications.NowHolding([.. Unread(count: 45, newest: 145), ANotification.With(id: "100")]);
        Passes(shell, Minute);

        Assert.True(Badge(opened, DestinationKind.Notifications).Full);
        Assert.Equal(1, shell.Host.Bells);

        Passes(shell, Minute);

        Assert.Equal(1, shell.Host.Bells);

        shell.Notifications.NowHolding([.. Unread(count: 46, newest: 146), ANotification.With(id: "100")]);
        Passes(shell, Minute);

        Assert.True(Badge(opened, DestinationKind.Notifications).Full);
        Assert.Equal(2, shell.Host.Bells);
    }

    /// <summary>
    ///     Answering a request under <c>40+</c> is not something arriving: the count is the instance's, and a re-read that
    ///     finds the list full again — nothing new, only the next one past the forty moving up — rings nothing.
    /// </summary>
    [Fact]
    public async Task AnsweringARequestUnderAFullBadge_ThenAReReadFindingItFullAgain_RingsNothing()
    {
        var shell = new AShell
        {
            Accounts = FakeAccountRelationships.Holding(subject: null, [.. Asking(from: 0, count: 40)]),
        };
        var opened = await shell.Opened();
        opened.Step(ToRequests);
        shell.Host.Settle();

        shell.Accounts.NowListing([.. Asking(from: 1, count: 40)]);
        opened.Press(ShellKey.A);
        shell.Host.Drain();
        Passes(shell, Minute);

        Assert.Equal(0, shell.Host.Bells);
    }

    /// <summary>
    ///     <c>40+</c> is the only badge shape that says "at least": one answered under it leaves it saying <c>40+</c>
    ///     rather than <c>39+</c>, until the instance says otherwise.
    /// </summary>
    [Fact]
    public async Task AnsweringARequestUnderAFullBadge_LeavesIt40Plus()
    {
        var shell = new AShell
        {
            Accounts = FakeAccountRelationships.Holding(subject: null, [.. Asking(from: 0, count: 40)]),
        };
        var opened = await shell.Opened();
        opened.Step(ToRequests);
        shell.Host.Settle();

        opened.Press(ShellKey.A);
        shell.Host.Drain();

        var badge = Badge(opened, DestinationKind.Requests);
        Assert.Equal(40, badge.Unread);
        Assert.True(badge.Full);
    }

    /// <summary>
    ///     A badge moved down by something done here says what the instance would: a conversation marked read is one
    ///     fewer unread, so another arriving after it is a rise, and rings.
    /// </summary>
    [Fact]
    public async Task AConversationArrivingAfterOneWasMarkedRead_Rings()
    {
        var shell = new AShell { Messages = FakeDirectMessages.Holding(AConversation.With(id: "c1", unread: true)) };
        var opened = await shell.Opened();
        opened.Step(ToMessages);
        shell.Host.Settle();

        shell.Messages.NowHolding(AConversation.With(id: "c1", unread: false));
        await opened.MarkRead();
        shell.Host.Drain();
        Assert.Equal(0, Badge(opened, DestinationKind.Messages).Unread);

        shell.Messages.NowHolding(
            AConversation.With(id: "c2", unread: true),
            AConversation.With(id: "c1", unread: false));
        Passes(shell, Minute);

        Assert.Equal(1, shell.Host.Bells);
    }

    /// <summary>
    ///     On Notifications, a re-read that put what it found on screen moved the marker past it and cleared the badge —
    ///     so the next notification to arrive is a rise from nothing, and rings again.
    /// </summary>
    [Fact]
    public async Task OnNotifications_EachPollPuttingSomethingNewOnScreen_Rings()
    {
        var shell = OneUnreadNotification();
        var opened = await shell.Opened();
        opened.Step(ToMessages - 1);
        shell.Host.Settle();

        shell.Notifications.NowHolding(ANotification.With(id: "102"), ANotification.With(id: "101"));
        Passes(shell, Minute);
        Assert.Equal(0, Badge(opened, DestinationKind.Notifications).Unread);

        shell.Notifications.NowHolding(
            ANotification.With(id: "103"),
            ANotification.With(id: "102"),
            ANotification.With(id: "101"));
        Passes(shell, Minute);

        Assert.Equal(2, shell.Host.Bells);
    }

    [Fact]
    public async Task WithTheBellTurnedOff_NothingRings_AndTheBadgeStillRises()
    {
        var shell = OneUnreadNotification();
        shell.Bell = false;
        var opened = await shell.Opened();

        shell.Notifications.NowHolding(ANotification.With(id: "102"), ANotification.With(id: "101"));
        Passes(shell, Minute);

        Assert.Equal(0, shell.Host.Bells);
        Assert.Equal(2, Badge(opened, DestinationKind.Notifications).Unread);
    }

    /// <summary><paramref name="count" /> people asking to follow, newest first, from the <paramref name="from" />th.</summary>
    private static IEnumerable<Wooly.Core.Accounts.Account> Asking(int from, int count) =>
        Enumerable.Range(from, count).Select(n => AnAccount.With(id: $"a{n}", address: $"asker{n}@mas.to"));

    private static IEnumerable<Notification> Unread(int count, int newest) =>
        Enumerable.Range(0, count).Select(offset => ANotification.With(id: $"{newest - offset}"));

    /// <summary>One notification newer than the marker.</summary>
    private static AShell OneUnreadNotification()
    {
        var shell = new AShell { Notifications = FakeNotificationInbox.Holding(ANotification.With(id: "101")) };
        shell.Notifications.Marker = "100";

        return shell;
    }

    /// <summary>Acting as personal, which is the default, with work set up beside it on another instance.</summary>
    private static AShell PersonalAndWork() => new()
    {
        Profiles = FakeProfileRegistry.Holding(
            "personal",
            FakeProfileRegistry.Profile("personal", "mastodon.social", "jeff@mastodon.social"),
            FakeProfileRegistry.Profile("work", "hachyderm.io", "jeff@hachyderm.io")),
    };

    /// <summary>Switches to <paramref name="name" />, and lets everything the switch asked for land.</summary>
    private static void SwitchTo(AShell shell, Shell opened, string name)
    {
        opened.Press(ShellKey.CtrlP);
        Assert.IsType<ProfilesScreen>(opened.Screen).Pick(name);
        Assert.True(opened.Press(ShellKey.Enter));
        shell.Host.Drain();
    }

    /// <summary>Moves the shell's clock on by <paramref name="howLong" />, and lets whatever that asked for land.</summary>
    private static void Passes(AShell shell, TimeSpan howLong)
    {
        shell.Clock.Advance(howLong);
        shell.Host.Drain();
    }

    private static Destination Badge(Shell shell, DestinationKind kind) =>
        shell.Rail.Destinations.Single(destination => destination.Kind == kind);
}
