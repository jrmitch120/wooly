using Wooly.Core.Errors;
using Wooly.Core.Notifications;
using Wooly.Core.Paging;
using Wooly.Core.Posts;
using Wooly.Tests.Fakes;
using Wooly.Tui.Screens;
using Wooly.Tui.Shell;

namespace Wooly.Tests.Tui;

/// <summary>
///     The shell reads all three Inbox badges again every 60 seconds for the profile it is acting as, so that something
///     arriving shows on the rail without the reader going to look (#398) — off the shell's own clock, which a test moves
///     by hand.
/// </summary>
public class InboxPollTests
{
    private static readonly TimeSpan Minute = TimeSpan.FromSeconds(60);

    [Fact]
    public async Task AMinuteOn_EveryCountIsReadAgain()
    {
        var shell = new AShell();
        await shell.Opened();
        var (notifications, messages, requests) = Reads(shell);

        Passes(shell, Minute);

        Assert.Equal(notifications + 1, shell.Notifications.UnreadReads.Count);
        Assert.Equal(messages + 1, shell.Messages.Listings.Count);
        Assert.Equal(requests + 1, shell.Accounts.Lists.Count);
    }

    /// <summary>
    ///     The Inbox is kept live from the moment the session starts, not from when the first destination has been
    ///     read: a first read that never answers holds back neither the stream nor the poll.
    /// </summary>
    [Fact]
    public async Task AFirstDestinationThatNeverAnswers_HoldsNothingBack()
    {
        var shell = new AShell
        {
            Timelines = FakeTimelineReader.Awaiting(_ => new TaskCompletionSource<Fetch<Post>>().Task),
            Notifications = FakeNotificationInbox.Holding(ANotification.With(id: "101")),
        };
        shell.Notifications.Marker = "100";
        var opened = shell.Build();
        _ = opened.Open();
        shell.Host.Drain();

        Assert.Single(shell.Stream.Listens);

        Passes(shell, Minute);

        Assert.Equal(1, opened.Rail.Destinations.Single(place => place.Kind == DestinationKind.Notifications).Unread);
    }

    [Fact]
    public async Task ShortOfAMinute_NothingIsReadAgain()
    {
        var shell = new AShell();
        await shell.Opened();
        var before = shell.Requests;

        Passes(shell, Minute - TimeSpan.FromSeconds(1));

        Assert.Equal(before, shell.Requests);
    }

    [Fact]
    public async Task ANotificationArrivingBetweenPolls_ShowsOnTheBadgeAfterTheNext()
    {
        var shell = new AShell { Notifications = FakeNotificationInbox.Holding(ANotification.With(id: "101")) };
        shell.Notifications.Marker = "100";
        var opened = await shell.Opened();

        Assert.Equal(1, Badge(opened, DestinationKind.Notifications).Unread);

        shell.Notifications.NowHolding(ANotification.With(id: "102"), ANotification.With(id: "101"));
        Passes(shell, Minute);

        Assert.Equal(2, Badge(opened, DestinationKind.Notifications).Unread);
    }

    /// <summary>Set outright from each answer, so a badge falls as readily as it rises.</summary>
    [Fact]
    public async Task EachPoll_SetsEveryBadgeFromWhatItRead()
    {
        var shell = new AShell
        {
            Notifications = FakeNotificationInbox.Holding(ANotification.With(id: "101")),
            Messages = FakeDirectMessages.Holding(AConversation.With(id: "c1", unread: true)),
            Accounts = FakeAccountRelationships.Holding(subject: null, AnAccount.With(address: "asker@mas.to")),
        };
        var opened = await shell.Opened();

        shell.Notifications.Marker = "101";
        Passes(shell, Minute);

        Assert.Equal(0, Badge(opened, DestinationKind.Notifications).Unread);
        Assert.Equal(1, Badge(opened, DestinationKind.Messages).Unread);
        Assert.Equal(1, Badge(opened, DestinationKind.Requests).Unread);
    }

    [Fact]
    public async Task EveryMinute_TheCountsAreReadAgain()
    {
        var shell = new AShell();
        await shell.Opened();
        var (notifications, _, _) = Reads(shell);

        Passes(shell, Minute);
        Passes(shell, Minute);
        Passes(shell, Minute);

        Assert.Equal(notifications + 3, shell.Notifications.UnreadReads.Count);
    }

    /// <summary>A poll that fails leaves the badge as it was drawn, and the next one is read as if nothing happened.</summary>
    [Fact]
    public async Task APollThatFails_LeavesTheBadge_AndPollingGoesOn()
    {
        var shell = new AShell { Notifications = FakeNotificationInbox.Holding(ANotification.With(id: "101")) };
        shell.Notifications.Marker = "100";
        var opened = await shell.Opened();

        shell.Notifications.ByProfile["personal"] =
            () => Task.FromException<Fetch<Notification>>(new TransientNetworkException(null, 3, new HttpRequestException("Down.")));
        Passes(shell, Minute);

        Assert.Equal(1, Badge(opened, DestinationKind.Notifications).Unread);

        shell.Notifications.ByProfile.Remove("personal");
        shell.Notifications.NowHolding(ANotification.With(id: "102"), ANotification.With(id: "101"));
        Passes(shell, Minute);

        Assert.Equal(2, Badge(opened, DestinationKind.Notifications).Unread);
    }

    /// <summary>
    ///     Only the profile acted as is polled: after a switch, nothing is read as the old one, and the new one is
    ///     polled in its place.
    /// </summary>
    [Fact]
    public async Task AfterASwitch_OnlyTheNewProfileIsPolled()
    {
        var shell = PersonalAndWork();
        var opened = await shell.Opened();

        SwitchTo(shell, opened, "work");
        var before = shell.Notifications.UnreadReads.Count;

        Passes(shell, Minute);

        var poll = Assert.Single(shell.Notifications.UnreadReads.Skip(before));
        Assert.Equal("work", poll.Profile);
    }

    /// <summary>A poll as the old profile still in flight at the switch is not drawn on the new one's rail.</summary>
    [Fact]
    public async Task APollInFlightAtTheSwitch_NeverLands()
    {
        var shell = PersonalAndWork();
        shell.Notifications.ByProfile["work"] = () => Task.FromResult(Fetch<Notification>.Complete([]));
        var opened = await shell.Opened();

        var late = new TaskCompletionSource<Fetch<Notification>>();
        shell.Notifications.ByProfile["personal"] = () => late.Task;
        Passes(shell, Minute);

        SwitchTo(shell, opened, "work");

        late.SetResult(Fetch<Notification>.Complete([ANotification.With(id: "1"), ANotification.With(id: "2")]));
        shell.Host.Drain();

        Assert.Equal(0, Badge(opened, DestinationKind.Notifications).Unread);
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

    /// <summary>How many reads each count has had so far.</summary>
    private static (int Notifications, int Messages, int Requests) Reads(AShell shell) =>
        (shell.Notifications.UnreadReads.Count, shell.Messages.Listings.Count, shell.Accounts.Lists.Count);

    /// <summary>Moves the shell's clock on by <paramref name="howLong" />, and lets whatever that asked for land.</summary>
    private static void Passes(AShell shell, TimeSpan howLong)
    {
        shell.Clock.Advance(howLong);
        shell.Host.Drain();
    }

    private static Destination Badge(Shell shell, DestinationKind kind) =>
        shell.Rail.Destinations.Single(destination => destination.Kind == kind);
}
