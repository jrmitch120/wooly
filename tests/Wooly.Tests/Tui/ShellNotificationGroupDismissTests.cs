using Wooly.Core.Notifications;
using Wooly.Tests.Fakes;
using Wooly.Tui.Screens;
using Wooly.Tui.Shell;

namespace Wooly.Tests.Tui;

/// <summary>
///     <c>d</c> on a <b>Notification group</b> dismisses all of it in one request, by the group's key (#457): clearing
///     "8 people favorited your post" is one key and one request, and the group leaves the screen and the badge.
/// </summary>
public class ShellNotificationGroupDismissTests
{
    /// <summary>Where the rail's notifications destination is, counting from Home.</summary>
    private const int ToNotifications = 6;

    /// <summary>
    ///     One dismissal, naming the group rather than any notification in it, takes the group off the screen and every
    ///     notification in it off the badge.
    /// </summary>
    [Fact]
    public async Task Dismiss_SendsOneGroupDismissByItsKeyAndTakesTheGroupOff()
    {
        var shell = Holding();
        var opened = await shell.Opened();

        opened.Step(ToNotifications);
        shell.Host.Settle();

        opened.Press(ShellKey.D);
        shell.Host.Drain();

        Assert.Equal("favourite-110-493000", Assert.Single(shell.Notifications.GroupDismissals).Key);
        Assert.Empty(shell.Notifications.Dismissals);

        var screen = Assert.IsType<NotificationsScreen>(opened.Screen);
        Assert.Equal(["follow-493000"], screen.Groups.Select(group => group.Key));
        Assert.Equal(1, Badge(opened));
    }

    /// <summary>
    ///     A dismissal happened on the instance whether or not the reader stayed to see it, so walking into another
    ///     screen before it lands still takes the group off the badge and off the list (#233).
    /// </summary>
    [Fact]
    public async Task Dismiss_TakesTheGroupOffEvenWhereTheReaderHasWalkedIntoAnotherScreen()
    {
        var shell = Holding();
        var opened = await shell.Opened();

        opened.Step(ToNotifications);
        shell.Host.Settle();

        var notifications = Assert.IsType<NotificationsScreen>(opened.Screen);

        opened.Press(ShellKey.D);
        opened.Help();
        shell.Host.Drain();

        Assert.IsType<HelpScreen>(opened.Screen);
        Assert.Equal(1, Badge(opened));
        Assert.Equal(["follow-493000"], notifications.Groups.Select(group => group.Key));
    }

    /// <summary>
    ///     An inbox of a favorite group of three and a follow, all four unread: the marker stays where it is, so the badge
    ///     counts what is still on the instance.
    /// </summary>
    private static AShell Holding()
    {
        var shell = new AShell
        {
            Notifications = FakeNotificationInbox.Holding(
                ANotification.With(id: "32"),
                ANotification.With(id: "31"),
                ANotification.With(id: "30"),
                ANotification.Follow(id: "29")),
        };

        shell.Notifications.Groups =
        [
            ANotification.Group(key: "favourite-110-493000", count: 3, newestId: "32", actors: ["Alice", "Bob", "Carol"]),
            ANotification.Group(
                key: "follow-493000",
                kind: NotificationKind.Follow,
                newestId: "29",
                noPost: true),
        ];
        shell.Notifications.Members["favourite-110-493000"] = ["30", "31", "32"];
        shell.Notifications.TurnsMarkerMovesDown = true;

        return shell;
    }

    private static int Badge(Shell opened) =>
        opened.Rail.Destinations.First(place => place.Kind == DestinationKind.Notifications).Unread;
}
