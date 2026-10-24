using Wooly.Core.Notifications;
using Wooly.Tests.Fakes;
using Wooly.Tui.Rendering;
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
    private const int ToNotifications = 6;

    /// <summary>Arriving lists what is waiting and reads it, so the rail's badge clears (#397).</summary>
    [Fact]
    public async Task Step_ListsWhatIsWaitingAndReadsIt()
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
        Assert.Equal(["34", "35"], screen.Groups.Select(group => group.NewestId));
        Assert.Equal("Notifications", opened.Breadcrumb);

        var destination = opened.Rail.Destinations.First(place => place.Kind == DestinationKind.Notifications);
        Assert.Equal(0, destination.Unread);
        Assert.Equal("34", Assert.Single(shell.Notifications.MarkerMoves).NotificationId);
    }

    /// <summary>
    ///     A notification is not the post it is about (GLOSSARY.md): dismissing names the picked group by its own key,
    ///     and dismissing by the post's id would clear nothing.
    /// </summary>
    [Fact]
    public async Task Dismiss_ClearsThePickedGroupByItsOwnKey()
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

        opened.Press(ShellKey.D);
        shell.Host.Drain();

        Assert.Equal("ungrouped-34", Assert.Single(shell.Notifications.GroupDismissals).Key);

        var screen = Assert.IsType<NotificationsScreen>(opened.Screen);
        Assert.Equal(["36"], screen.Groups.Select(group => group.NewestId));

        // The badge and the list are the same fact, so one cannot say two over a list of one.
        Assert.Equal(1, opened.Rail.Destinations.First(place => place.Kind == DestinationKind.Notifications).Unread);
    }

    /// <summary>
    ///     A dismissal happened on the instance whether or not the reader stayed to see it, so walking into another
    ///     screen before it lands still takes it off the badge and off the list (#233).
    /// </summary>
    [Fact]
    public async Task Dismiss_CountsItOnTheBadgeEvenWhereTheReaderHasWalkedIntoAnotherScreen()
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

        var notifications = Assert.IsType<NotificationsScreen>(opened.Screen);

        // Dismissed, and walked away from before the answer is back on the drawing thread.
        opened.Press(ShellKey.D);
        opened.Help();
        shell.Host.Drain();

        Assert.IsType<HelpScreen>(opened.Screen);
        Assert.Equal(1, opened.Rail.Destinations.First(place => place.Kind == DestinationKind.Notifications).Unread);
        Assert.Equal(["36"], notifications.Groups.Select(group => group.NewestId));
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

        opened.Press(ShellKey.CapitalD);

        Assert.NotNull(opened.Asking);
        Assert.Equal(Confirmation.CannotBeUndone, opened.Asking.Warning);
        Assert.Equal("clear", opened.Asking.Going);
        Assert.Empty(shell.Notifications.Clearances);

        await opened.Answer(agreed: true);
        shell.Host.Drain();

        Assert.Equal("personal", Assert.Single(shell.Notifications.Clearances));

        var screen = Assert.IsType<NotificationsScreen>(opened.Screen);
        Assert.Empty(screen.Groups);
        Assert.Equal(0, opened.Rail.Destinations.First(place => place.Kind == DestinationKind.Notifications).Unread);
    }

    [Fact]
    public async Task Answer_LeavesTheInboxAloneWhereTheClearIsNotAgreedTo()
    {
        var shell = new AShell { Notifications = FakeNotificationInbox.Holding(ANotification.With()) };
        var opened = await shell.Opened();

        opened.Step(ToNotifications);
        shell.Host.Settle();

        opened.Press(ShellKey.CapitalD);
        await opened.Answer(agreed: false);

        Assert.Empty(shell.Notifications.Clearances);
        Assert.Single(Assert.IsType<NotificationsScreen>(opened.Screen).Groups);
    }

    /// <summary>Nothing to ask about is nothing to ask, rather than a confirmation over an empty list.</summary>
    [Fact]
    public async Task AskToClear_AsksNothingWhereNothingIsWaiting()
    {
        var shell = new AShell();
        var opened = await shell.Opened();

        opened.Step(ToNotifications);
        shell.Host.Settle();

        opened.Press(ShellKey.CapitalD);

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

        var drawn = opened.Screen.Lines(new Drawing(61, AShell.Now)).Select(line => line.Text);

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

        var drawn = opened.Screen.Lines(new Drawing(61, AShell.Now)).Select(line => line.Text);

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
            Notifications = FakeNotificationInbox.Holding(ANotification.With(id: "36"), ANotification.With(id: "34")),
        };

        var opened = await shell.Opened();

        opened.Step(ToNotifications);
        shell.Host.Settle();

        var reads = shell.Notifications.GroupReads.Count;

        opened.Press(ShellKey.D);

        opened.Step(1);
        shell.Host.Settle();
        opened.Step(-1);
        shell.Host.Settle();

        Assert.Equal(reads + 1, shell.Notifications.GroupReads.Count);
    }

    /// <summary>
    ///     <c>a</c> opens the notification's actor, not the author of the post it is about (#451): on a favorite or a
    ///     boost that author is the reader, and going to yourself is not where "Alice favorited your post" points. A kind
    ///     this client has no words for has an actor all the same.
    /// </summary>
    [Theory]
    [InlineData("favorite")]
    [InlineData("boost")]
    [InlineData("poll")]
    public async Task Press_AOpensTheActorRatherThanThePostsAuthor(string kind)
    {
        var shell = new AShell
        {
            Notifications = FakeNotificationInbox.Holding(
                ANotification.With(
                    kind: kind switch
                    {
                        "favorite" => NotificationKind.Favorite,
                        "boost" => NotificationKind.Boost,
                        _ => NotificationKind.Reported(kind),
                    },
                    account: "alice@hachyderm.io",
                    author: "Alice",
                    post: APost.With(id: "110", account: "jeff", author: "Jeff"))),
            Accounts = FakeAccountRelationships.Holding(AnAccount.With(address: "alice@hachyderm.io")),
        };

        var opened = await shell.Opened();

        opened.Step(ToNotifications);
        shell.Host.Settle();

        opened.Press(ShellKey.A);
        shell.Host.Drain();

        var account = Assert.IsType<AccountScreen>(opened.Screen);
        Assert.Equal("alice@hachyderm.io", account.Account.Address);
        Assert.Equal("alice@hachyderm.io", Assert.Single(shell.Accounts.Reads).Account.Text);
    }

    /// <summary>
    ///     A follow carries no post, which used to leave <c>a</c> with nothing to act on; its actor is the follower, and
    ///     looking at them is what a reader does before following back (#451).
    /// </summary>
    [Fact]
    public async Task Press_AOpensTheAccountThatFollowed()
    {
        var shell = new AShell
        {
            Notifications = FakeNotificationInbox.Holding(ANotification.Follow(account: "bob@mastodon.social")),
            Accounts = FakeAccountRelationships.Holding(AnAccount.With(address: "bob@mastodon.social", author: "Bob")),
        };

        var opened = await shell.Opened();

        opened.Step(ToNotifications);
        shell.Host.Settle();

        Assert.Contains(opened.Keys, key => key is { Key: "a", Does: "actor" });

        opened.Press(ShellKey.A);
        shell.Host.Drain();

        var account = Assert.IsType<AccountScreen>(opened.Screen);
        Assert.Equal("bob@mastodon.social", account.Account.Address);
        Assert.Equal("bob@mastodon.social", Assert.Single(shell.Accounts.Reads).Account.Text);
    }

    /// <summary>On a mention the actor and the post's author are the same account, and <c>a</c> opens it.</summary>
    [Fact]
    public async Task Press_AOpensTheAccountThatMentioned()
    {
        var shell = new AShell
        {
            Notifications = FakeNotificationInbox.Holding(
                ANotification.With(kind: NotificationKind.Mention, account: "alice@hachyderm.io")),
            Accounts = FakeAccountRelationships.Holding(AnAccount.With(address: "alice@hachyderm.io")),
        };

        var opened = await shell.Opened();

        opened.Step(ToNotifications);
        shell.Host.Settle();

        // Said as what it opens here, and said once.
        Assert.DoesNotContain(opened.Keys, key => key is { Key: "a", Does: "author" });

        opened.Press(ShellKey.A);
        shell.Host.Drain();

        var account = Assert.IsType<AccountScreen>(opened.Screen);
        Assert.Equal("alice@hachyderm.io", account.Account.Address);
        Assert.Equal("alice@hachyderm.io", Assert.Single(shell.Accounts.Reads).Account.Text);
    }
}
