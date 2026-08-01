using Wooly.Core.Notifications;
using Wooly.Tests.Fakes;
using Wooly.Tui.Screens;
using Wooly.Tui.Shell;

namespace Wooly.Tests.Tui;

/// <summary>
///     The notifications screen: what is waiting, what <c>d</c> takes off it one at a time, and what <c>D</c> takes off
///     it all at once. Every one of these is a decision the shell makes without a terminal — which id is dismissed,
///     whether emptying the inbox has been agreed to, what the rail's badge now says.
/// </summary>
public class ShellNotificationTests
{
    /// <summary>Where the rail's notifications destination is, counting from Home.</summary>
    private const int ToNotifications = 4;

    [Fact]
    public async Task Step_ListsWhatIsWaitingAndCountsItOnTheRail()
    {
        var shell = new AShell
        {
            Notifications = FakeNotificationInbox.Holding(
                ANotification.With(id: "34", author: "Alice"),
                ANotification.Follow(id: "35")),
        };

        var opened = await shell.Opened();

        opened.Step(ToNotifications);
        shell.Host.Settle();

        var screen = Assert.IsType<NotificationsScreen>(opened.Screen);
        Assert.Equal(["34", "35"], screen.Notifications.Select(notification => notification.Id));
        Assert.Equal("notifications", opened.Breadcrumb);

        var destination = opened.Rail.Destinations.First(place => place.Kind == DestinationKind.Notifications);
        Assert.Equal(2, destination.Unread);
    }

    /// <summary>
    ///     A notification is not the post it is about (CONTEXT.md): dismissing takes the notification's own id, and
    ///     dismissing by the post's would clear nothing.
    /// </summary>
    [Fact]
    public async Task Dismiss_ClearsThePickedNotificationByItsOwnId()
    {
        var shell = new AShell
        {
            Notifications = FakeNotificationInbox.Holding(
                ANotification.With(id: "34", post: APost.With(id: "110")),
                ANotification.With(id: "36", post: APost.With(id: "111"))),
        };

        var opened = await shell.Opened();

        opened.Step(ToNotifications);
        shell.Host.Settle();

        await opened.Press(ShellKey.Discard);

        Assert.Equal("34", Assert.Single(shell.Notifications.Dismissals).NotificationId);

        var screen = Assert.IsType<NotificationsScreen>(opened.Screen);
        Assert.Equal(["36"], screen.Notifications.Select(notification => notification.Id));

        // The badge and the list are the same fact, so one cannot say two over a list of one.
        Assert.Equal(1, opened.Rail.Destinations.First(place => place.Kind == DestinationKind.Notifications).Unread);
    }

    /// <summary>
    ///     Emptying the inbox takes away a list nobody has necessarily read and nothing brings it back, so it is asked
    ///     on the same terms <c>notification clear</c> asks it.
    /// </summary>
    [Fact]
    public async Task AskToClear_EmptiesNothingUntilItIsAgreedTo()
    {
        var shell = new AShell { Notifications = FakeNotificationInbox.Holding(ANotification.With()) };
        var opened = await shell.Opened();

        opened.Step(ToNotifications);
        shell.Host.Settle();

        opened.AskToClear();

        Assert.NotNull(opened.Asking);
        Assert.Contains("cannot be undone", opened.Asking.Question);
        Assert.Equal("clear", opened.Asking.Going);
        Assert.Empty(shell.Notifications.Clearances);

        await opened.Answer(agreed: true);

        Assert.Equal("personal", Assert.Single(shell.Notifications.Clearances));

        var screen = Assert.IsType<NotificationsScreen>(opened.Screen);
        Assert.Empty(screen.Notifications);
        Assert.Equal(0, opened.Rail.Destinations.First(place => place.Kind == DestinationKind.Notifications).Unread);
    }

    [Fact]
    public async Task Answer_LeavesTheInboxAloneWhereTheClearIsNotAgreedTo()
    {
        var shell = new AShell { Notifications = FakeNotificationInbox.Holding(ANotification.With()) };
        var opened = await shell.Opened();

        opened.Step(ToNotifications);
        shell.Host.Settle();

        opened.AskToClear();
        await opened.Answer(agreed: false);

        Assert.Empty(shell.Notifications.Clearances);
        Assert.Single(Assert.IsType<NotificationsScreen>(opened.Screen).Notifications);
    }

    /// <summary>Nothing to ask about is nothing to ask, rather than a confirmation over an empty list.</summary>
    [Fact]
    public async Task AskToClear_AsksNothingWhereNothingIsWaiting()
    {
        var shell = new AShell();
        var opened = await shell.Opened();

        opened.Step(ToNotifications);
        shell.Host.Settle();

        opened.AskToClear();

        Assert.Null(opened.Asking);
    }

    /// <summary>An empty inbox says so, rather than being a destination that swallowed a keypress.</summary>
    [Fact]
    public async Task Step_SaysSoWhereNothingIsWaiting()
    {
        var shell = new AShell();
        var opened = await shell.Opened();

        opened.Step(ToNotifications);
        shell.Host.Settle();

        var drawn = opened.Screen.Lines(61, AShell.Now).Select(line => line.Text);

        Assert.Contains(drawn, line => line.Contains("Nothing is waiting", StringComparison.Ordinal));
    }

    /// <summary>
    ///     A read a rate limit stopped part way through is said out loud. A reader told "nothing is waiting" would
    ///     believe it, which is the whole reason a fetch reports what stopped it (ADR-0007).
    /// </summary>
    [Fact]
    public async Task Step_SaysWhereARateLimitStoppedTheReadPartWayThrough()
    {
        var shell = new AShell { Notifications = FakeNotificationInbox.RateLimitedAfter(ANotification.With()) };
        var opened = await shell.Opened();

        opened.Step(ToNotifications);
        shell.Host.Settle();

        var drawn = opened.Screen.Lines(61, AShell.Now).Select(line => line.Text);

        Assert.Contains(drawn, line => line.Contains("Rate limited part way through", StringComparison.Ordinal));
    }

    /// <summary>
    ///     The post a notification is about is what the keys that act on a post act on, so answering a mention does not
    ///     mean leaving the inbox first. A follow is somebody arriving rather than something they wrote, so it has none.
    /// </summary>
    [Fact]
    public async Task Move_PicksOutThePostANotificationIsAbout()
    {
        var shell = new AShell
        {
            Notifications = FakeNotificationInbox.Holding(
                ANotification.With(id: "34", post: APost.With(id: "110")),
                ANotification.Follow(id: "35")),
        };

        var opened = await shell.Opened();

        opened.Step(ToNotifications);
        shell.Host.Settle();

        Assert.Equal("110", opened.Screen.Picked?.Id);

        opened.Move(1);

        Assert.Null(opened.Screen.Picked);
    }

    /// <summary>
    ///     <c>d</c> is the contract's own example of a key that means two things, so the status row has to say which
    ///     one is on offer here (<c>docs/tui-shell.md</c>).
    /// </summary>
    [Fact]
    public async Task Keys_SayThatDDismissesRatherThanDeletesHere()
    {
        var shell = new AShell { Notifications = FakeNotificationInbox.Holding(ANotification.With()) };
        var opened = await shell.Opened();

        opened.Step(ToNotifications);
        shell.Host.Settle();

        Assert.Contains(opened.Keys, key => key is { Key: "d", Does: "dismiss" });
        Assert.Contains(opened.Keys, key => key is { Key: "D", Does: "clear all" });
        Assert.DoesNotContain(opened.Keys, key => key.Does == "delete");
    }

    /// <summary>
    ///     The inbox this client has just emptied is not worth remembering, whatever its age says — the same rule a
    ///     published or deleted post puts on the timeline it was on.
    /// </summary>
    [Fact]
    public async Task Dismiss_ForgetsWhatTheDestinationHeldSoItIsReadAgain()
    {
        var shell = new AShell
        {
            Notifications = FakeNotificationInbox.Holding(ANotification.With(id: "34"), ANotification.With(id: "36")),
        };

        var opened = await shell.Opened();

        opened.Step(ToNotifications);
        shell.Host.Settle();

        var reads = shell.Notifications.Reads.Count;

        await opened.Dismiss();

        opened.Step(1);
        shell.Host.Settle();
        opened.Step(-1);
        shell.Host.Settle();

        Assert.Equal(reads + 1, shell.Notifications.Reads.Count);
    }
}
