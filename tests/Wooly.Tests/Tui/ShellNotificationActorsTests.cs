using Wooly.Core.Accounts;
using Wooly.Core.Notifications;
using Wooly.Tests.Fakes;
using Wooly.Tui.Rendering;
using Wooly.Tui.Screens;
using Wooly.Tui.Shell;

namespace Wooly.Tests.Tui;

/// <summary>
///     <c>w</c> on a notification group of several opens a screen listing every <b>Actor</b> in it, so every one of
///     them can be reached and not only the two the row names (#455). It is read from the group's own accounts, so it
///     lists exactly the group the row counted, and it is an ordinary screen on the stack drawn as Account blocks, the
///     way Followers is.
/// </summary>
public class ShellNotificationActorsTests
{
    /// <summary>Where the rail's notifications destination is, counting from Home.</summary>
    private const int ToNotifications = 6;

    /// <summary>
    ///     On a group of several, <c>w</c> opens the actors screen, filled from the group's accounts by its key —
    ///     everyone in it, not only the two the sample named.
    /// </summary>
    [Fact]
    public async Task PressA_OnAGroupOfSeveralOpensEveryActorInIt()
    {
        var (shell, opened) = await Arrived(
            ANotification.Group(key: "favourite-110-493000", count: 3, actors: ["Alice", "Bob"]),
            Actors("Alice", "Bob", "Carol"));

        opened.Press(ShellKey.W);
        shell.Host.Drain();

        var screen = Assert.IsType<ActorsScreen>(opened.Screen);
        Assert.Equal(["Alice", "Bob", "Carol"], screen.People.Select(person => person.Author));
        Assert.Equal("favourite-110-493000", Assert.Single(shell.Notifications.ActorReads).GroupKey);
        Assert.Contains(Drawn(screen), line => line.Contains("Carol", StringComparison.Ordinal));
        Assert.Empty(shell.Accounts.Reads);
    }

    /// <summary>On a group of one, <c>w</c> opens that one actor's account, as it did before groups had several (#451).</summary>
    [Fact]
    public async Task PressA_OnAGroupOfOneOpensItsActor()
    {
        var (shell, opened) = await Arrived(ANotification.Group(count: 1, actors: ["Alice"]));

        opened.Press(ShellKey.W);
        shell.Host.Drain();

        Assert.IsType<AccountScreen>(opened.Screen);
        Assert.Equal("alice@hachyderm.io", Assert.Single(shell.Accounts.Reads).Account.Text);
        Assert.Empty(shell.Notifications.ActorReads);
    }

    /// <summary>
    ///     The crumb says what the people on the screen did, from the group's kind — and for a kind this client has no
    ///     words for, the instance's own word.
    /// </summary>
    [Theory]
    [InlineData("favorite", "Favorited by")]
    [InlineData("boost", "Boosted by")]
    [InlineData("follow", "Followed you")]
    [InlineData("admin.sign_up", "admin.sign_up")]
    public async Task PressA_NamesTheKindInTheCrumb(string kind, string crumb)
    {
        var (shell, opened) = await Arrived(ANotification.Group(
            kind: kind switch
            {
                "favorite" => NotificationKind.Favorite,
                "boost" => NotificationKind.Boost,
                "follow" => NotificationKind.Follow,
                _ => NotificationKind.Reported(kind),
            },
            count: 2,
            actors: ["Alice", "Bob"],
            noPost: kind != "favorite" && kind != "boost"));

        opened.Press(ShellKey.W);
        shell.Host.Drain();

        Assert.Equal(crumb, opened.Screen.Crumb);
    }

    /// <summary>
    ///     A large group is read a page at a time: the first 80, then the next as the reader walks onto the end of
    ///     what has arrived, until the instance runs out.
    /// </summary>
    [Fact]
    public async Task Walking_ReadsTheNextPageOfActorsAtTheEnd()
    {
        var everyone = Enumerable.Range(1, 100).Select(at => $"Fan{at}").ToArray();
        var (shell, opened) = await Arrived(
            ANotification.Group(count: 100, actors: ["Fan1", "Fan2"]),
            Actors(everyone));

        opened.Press(ShellKey.W);
        shell.Host.Drain();

        var screen = Assert.IsType<ActorsScreen>(opened.Screen);
        Assert.Equal(80, screen.Read);
        Assert.Equal(80, shell.Notifications.ActorReads[^1].Limit);

        opened.Move(78);
        shell.Host.Drain();

        Assert.Single(shell.Notifications.ActorReads);

        opened.Move(1);
        shell.Host.Drain();

        Assert.Equal(160, shell.Notifications.ActorReads[^1].Limit);
        Assert.Equal(everyone, screen.People.Select(person => person.Author));
        Assert.False(screen.More);
    }

    /// <summary>
    ///     ⏎ opens whoever is picked out as their account screen, and <c>esc</c> walks back to the actors screen and
    ///     then to Notifications, as it does from Followers.
    /// </summary>
    [Fact]
    public async Task Enter_OpensTheActorPickedOutAndEscWalksBackOut()
    {
        var (shell, opened) = await Arrived(
            ANotification.Group(count: 2, actors: ["Alice", "Bob"]),
            Actors("Alice", "Bob"));

        opened.Press(ShellKey.W);
        shell.Host.Drain();
        opened.Move(1);
        opened.Press(ShellKey.Enter);
        shell.Host.Drain();

        Assert.IsType<AccountScreen>(opened.Screen);
        Assert.Equal("bob@hachyderm.io", shell.Accounts.Reads[^1].Account.Text);

        opened.Back();

        Assert.Equal("Bob", Assert.IsType<ActorsScreen>(opened.Screen).PickedPerson?.Author);

        opened.Back();

        Assert.IsType<NotificationsScreen>(opened.Screen);
    }

    private static IReadOnlyList<Account> Actors(params string[] names) =>
    [
        .. names.Select((name, at) => AnAccount.With(
            address: $"{name.ToLowerInvariant()}@hachyderm.io",
            author: name,
            id: $"{100 + at}")),
    ];

    private static async Task<(AShell Shell, Shell Opened)> Arrived(
        NotificationGroup group,
        IReadOnlyList<Account>? actors = null)
    {
        var shell = new AShell { Notifications = FakeNotificationInbox.Holding() };
        shell.Notifications.Groups = [group];
        shell.Notifications.ActorsOf[group.Key] = actors ?? group.Actors;

        var opened = await shell.Opened();

        opened.Step(ToNotifications);
        shell.Host.Settle();

        return (shell, opened);
    }

    /// <summary>The screen's rows as text.</summary>
    private static List<string> Drawn(Screen screen) =>
        [.. screen.Lines(new Drawing(61, AShell.Now)).Select(line => line.Text)];
}
