using Wooly.Core.Inbox;
using Wooly.Core.Notifications;
using Wooly.Tests.Fakes;
using Wooly.Tui.Screens;
using Wooly.Tui.Shell;

namespace Wooly.Tests.Tui;

/// <summary>
///     The shell keeps the Inbox live over the user stream of the profile it is acting as (#401): each event re-reads the
///     counts it touches, the 60-second poll stops while the stream is up, and a stream that drops is reconnected with
///     backoff, polled meanwhile, and all three counts read again once it is back — saying nothing about any of it.
/// </summary>
public class InboxStreamTests
{
    private static readonly TimeSpan Minute = TimeSpan.FromSeconds(60);

    private static ShellTiming Timing => ShellTiming.Default;

    [Fact]
    public async Task ANotification_ReadsTheNotificationsCountAgain()
    {
        var shell = Connected();
        var opened = await shell.Opened();

        shell.Notifications.NowHolding(ANotification.With(id: "1"));
        shell.Stream.Notify(NotificationKind.Mention);
        shell.Host.Drain();

        Assert.Equal(1, Badge(opened, DestinationKind.Notifications).Unread);
    }

    [Fact]
    public async Task AMention_ReadsNoOtherCountAgain()
    {
        var shell = Connected();
        await shell.Opened();
        var (notifications, messages, requests) = Reads(shell);

        shell.Stream.Notify(NotificationKind.Mention);
        shell.Host.Drain();

        Assert.Equal((notifications + 1, messages, requests), Reads(shell));
    }

    [Fact]
    public async Task AFollowRequest_ReadsTheRequestsCountAgainToo()
    {
        var shell = Connected();
        shell.Accounts = FakeAccountRelationships.HoldingNobody();
        var opened = await shell.Opened();
        var (notifications, messages, requests) = Reads(shell);

        shell.Accounts.NowListing(AnAccount.With(address: "asker@mas.to"));
        shell.Stream.Notify(NotificationKind.Reported("follow_request"));
        shell.Host.Drain();

        Assert.Equal((notifications + 1, messages, requests + 1), Reads(shell));
        Assert.Equal(1, Badge(opened, DestinationKind.Requests).Unread);
    }

    [Fact]
    public async Task AConversation_ReadsTheMessagesCountAgain()
    {
        var shell = Connected();
        var opened = await shell.Opened();
        var (notifications, messages, requests) = Reads(shell);

        shell.Messages.NowHolding(AConversation.With(id: "c1", unread: true));
        shell.Stream.Converse();
        shell.Host.Drain();

        Assert.Equal((notifications, messages + 1, requests), Reads(shell));
        Assert.Equal(1, Badge(opened, DestinationKind.Messages).Unread);
    }

    /// <summary>The counts the shell opened with are already fresh: the stream coming up the first time reads none again.</summary>
    [Fact]
    public async Task ComingUpTheFirstTime_ReadsNothingAgain()
    {
        var shell = new AShell();
        await shell.Opened();
        var before = shell.Requests;

        shell.Stream.Connect();
        shell.Host.Drain();

        Assert.Equal(before, shell.Requests);
    }

    [Fact]
    public async Task WhileTheStreamIsUp_NothingIsPolled()
    {
        var shell = Connected();
        await shell.Opened();
        var before = shell.Requests;

        Passes(shell, Minute * 5);

        Assert.Equal(before, shell.Requests);
    }

    /// <summary>A stream that comes up late stops the poll from then on.</summary>
    [Fact]
    public async Task OnceTheStreamComesUp_ThePollStops()
    {
        var shell = new AShell();
        await shell.Opened();
        Passes(shell, Minute);

        shell.Stream.Connect();
        shell.Host.Drain();
        var before = shell.Requests;
        Passes(shell, Minute * 3);

        Assert.Equal(before, shell.Requests);
    }

    [Fact]
    public async Task WhileTheStreamIsDown_TheCountsArePolledEveryMinute()
    {
        var shell = Connected();
        await shell.Opened();

        shell.Stream.Refuses = true;
        shell.Stream.Drop();
        shell.Host.Drain();
        var (notifications, _, _) = Reads(shell);

        Passes(shell, Minute);
        Passes(shell, Minute);

        Assert.Equal(notifications + 2, shell.Notifications.UnreadReads.Count);
    }

    /// <summary>
    ///     An instance that will not stream is asked again and again, and the poll it fell back on is not put off by
    ///     each refusal: it still reads every minute.
    /// </summary>
    [Fact]
    public async Task AStreamThatWillNotConnect_LeavesTheCountsPolledEveryMinute()
    {
        var shell = new AShell { Stream = new FakeInboxStream { Refuses = true } };
        await shell.Opened();
        var (notifications, _, _) = Reads(shell);

        Passes(shell, Minute);
        Passes(shell, Minute);
        Passes(shell, Minute);

        Assert.Equal(notifications + 3, shell.Notifications.UnreadReads.Count);
    }

    [Fact]
    public async Task ComingBackUp_ReadsAllThreeCountsAgainOnce()
    {
        var shell = Connected();
        await shell.Opened();
        var (notifications, messages, requests) = Reads(shell);

        shell.Stream.Drop();
        Passes(shell, Timing.ReconnectAfter);

        Assert.Equal((notifications + 1, messages + 1, requests + 1), Reads(shell));
    }

    /// <summary>What arrived while the stream was down shows on the badge once it is back.</summary>
    [Fact]
    public async Task ANotificationArrivingWhileDown_ShowsOnceTheStreamIsBack()
    {
        var shell = Connected();
        var opened = await shell.Opened();

        shell.Stream.Drop();
        shell.Host.Drain();
        shell.Notifications.NowHolding(ANotification.With(id: "1"));
        Passes(shell, Timing.ReconnectAfter);

        Assert.Equal(1, Badge(opened, DestinationKind.Notifications).Unread);
    }

    /// <summary>Each try that fails waits twice as long as the last before the next.</summary>
    [Fact]
    public async Task TryingAgain_BacksOff()
    {
        var shell = new AShell { Stream = new FakeInboxStream { Refuses = true } };
        await shell.Opened();

        int[] tries =
        [
            Tries(shell), // The first, as it opened.
            After(shell, TimeSpan.FromSeconds(2)),
            After(shell, TimeSpan.FromSeconds(3)),
            After(shell, TimeSpan.FromSeconds(1)), // Four seconds on from the second.
            After(shell, TimeSpan.FromSeconds(7)),
            After(shell, TimeSpan.FromSeconds(1)), // Eight on from the third.
        ];

        Assert.Equal([1, 2, 2, 3, 3, 4], tries);
    }

    [Fact]
    public async Task TryingAgain_NeverWaitsLongerThanTheCeiling()
    {
        var shell = new AShell { Stream = new FakeInboxStream { Refuses = true } };
        await shell.Opened();
        Passes(shell, TimeSpan.FromHours(1));
        var tries = Tries(shell);

        Passes(shell, Timing.ReconnectAtMost);

        Assert.Equal(tries + 1, Tries(shell));
    }

    /// <summary>A connection that came up starts the waiting over: the next drop is tried again after the shortest wait.</summary>
    [Fact]
    public async Task ComingUp_StartsTheBackoffOver()
    {
        var shell = new AShell { Stream = new FakeInboxStream { Refuses = true } };
        await shell.Opened();
        Passes(shell, TimeSpan.FromMinutes(10));

        shell.Stream.Refuses = false;
        shell.Stream.ConnectsAtOnce = true;
        Passes(shell, Timing.ReconnectAtMost);
        var tries = Tries(shell);

        shell.Stream.Drop();
        Passes(shell, Timing.ReconnectAfter);

        Assert.Equal(tries + 1, Tries(shell));
    }

    /// <summary>A stream that drops or will not come up is no business of the reader's: nothing on screen says so.</summary>
    [Fact]
    public async Task ADroppedOrRefusedStream_SaysNothing()
    {
        var shell = Connected();
        var opened = await shell.Opened();
        var drawn = AShell.Drawn(opened.Screen);
        var rail = opened.Rail.Destinations.ToList();

        shell.Stream.Refuses = true;
        shell.Stream.Drop();
        Passes(shell, Minute * 3);

        Assert.Null(opened.Notice);
        Assert.Equal(drawn, AShell.Drawn(opened.Screen));
        Assert.Equal(rail, opened.Rail.Destinations);
    }

    /// <summary>A switch closes the old profile's stream and opens the new one's.</summary>
    [Fact]
    public async Task AfterASwitch_TheNewProfilesStreamIsOpened_AndTheOldOneClosed()
    {
        var shell = PersonalAndWork();
        var opened = await shell.Opened();
        var old = shell.Stream.Latest;

        SwitchTo(shell, opened, "work");

        Assert.True(old.Closed);
        Assert.Equal("work", shell.Stream.Latest.Profile.Name);
    }

    [Fact]
    public async Task AfterASwitch_EventsFromTheOldProfilesStreamAreNotApplied()
    {
        var shell = PersonalAndWork();
        var opened = await shell.Opened();
        var old = shell.Stream.Latest;

        SwitchTo(shell, opened, "work");
        var before = shell.Requests;

        old.Hear(new InboxEvent.Notified(NotificationKind.Mention));
        old.Hear(new InboxEvent.Conversed());
        old.Connect();
        old.Drop();
        Passes(shell, Minute);

        Assert.Equal(before, shell.Requests);
    }

    /// <summary>A shell whose stream comes up as soon as it is asked for.</summary>
    private static AShell Connected() => new() { Stream = new FakeInboxStream { ConnectsAtOnce = true } };

    /// <summary>Acting as personal, which is the default, with work set up beside it on another instance.</summary>
    private static AShell PersonalAndWork() => new()
    {
        Stream = new FakeInboxStream { ConnectsAtOnce = true },
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

    /// <summary>How many times the stream has been connected to, or tried.</summary>
    private static int Tries(AShell shell) => shell.Stream.Listens.Count;

    /// <summary>How many tries there have been once <paramref name="howLong" /> more has passed.</summary>
    private static int After(AShell shell, TimeSpan howLong)
    {
        Passes(shell, howLong);

        return Tries(shell);
    }

    /// <summary>How many reads each count has had so far.</summary>
    private static (int Notifications, int Messages, int Requests) Reads(AShell shell) =>
        (shell.Notifications.UnreadReads.Count, shell.Messages.Listings.Count, shell.Accounts.Lists.Count);

    /// <summary>
    ///     Moves the shell's clock on by <paramref name="howLong" /> a second at a time, letting whatever each second
    ///     asked for land before the next — so that a wait started by what a wait set going is itself waited out.
    /// </summary>
    private static void Passes(AShell shell, TimeSpan howLong)
    {
        shell.Host.Drain();

        for (var passed = TimeSpan.Zero; passed < howLong; passed += TimeSpan.FromSeconds(1))
        {
            shell.Clock.Advance(TimeSpan.FromSeconds(1));
            shell.Host.Drain();
        }
    }

    private static Destination Badge(Shell shell, DestinationKind kind) =>
        shell.Rail.Destinations.Single(destination => destination.Kind == kind);
}
