using Wooly.Core.Accounts;
using Wooly.Core.Posts;
using Wooly.Core.Relationships;
using Wooly.Tests.Fakes;
using Wooly.Tui.Screens;
using Wooly.Tui.Shell;

namespace Wooly.Tests.Tui;

/// <summary>
///     What a change made on the instance makes stale, wherever the reader happened to be standing when it was made:
///     one table in <see cref="Arrival" />, rather than each verb guessing at the destination showing (#234).
/// </summary>
public class ShellChangeTests
{
    private const int ToNotifications = 4;

    private static readonly Post Mine = APost.With(id: "110", account: "jeff@mastodon.social");

    private static readonly Post Somebody = APost.With(id: "220", account: "ben@hachyderm.io");

    /// <summary>
    ///     A post marked on Home is marked on every list holding it, so a Local read a moment ago is not handed back
    ///     with the star still off.
    /// </summary>
    [Fact]
    public async Task Mark_ForgetsEveryCachedListHoldingThePost()
    {
        var favorited = Somebody with { Marks = APost.Marked(favorited: true) };

        var shell = new AShell
        {
            Timelines = FakeTimelineReader.Holding(Somebody),
            Engagement = FakePostEngagement.Answering(favorited),
        };

        var opened = await OnHomeHavingReadLocal(shell);

        await opened.Mark(PostMark.Favorite);
        shell.Host.Drain();

        shell.Timelines.NowHolding(favorited);

        opened.Step(1);
        shell.Host.Settle();

        Assert.True(opened.Screen.Picked?.Marks.Favorited);
    }

    /// <summary>A post deleted on Home is not handed back on a Local list read before it went.</summary>
    [Fact]
    public async Task Delete_ForgetsEveryCachedListThePostWasIn()
    {
        var shell = new AShell { Timelines = FakeTimelineReader.Holding(Mine, Somebody) };
        var opened = await OnHomeHavingReadLocal(shell);

        opened.AskToDelete();
        await opened.Answer(agreed: true);
        shell.Host.Drain();

        shell.Timelines.NowHolding(Somebody);

        opened.Step(1);
        shell.Host.Settle();

        var local = Assert.IsType<FeedScreen>(opened.Screen);

        Assert.Equal(["220"], local.Posts.Select(post => post.Id));
    }

    /// <summary>
    ///     A post sent is on Home whatever was showing when it was sent, so Home is read again rather than handed back
    ///     without it.
    /// </summary>
    [Fact]
    public async Task Send_ForgetsHomeWhereverItWasSentFrom()
    {
        var shell = new AShell();
        var opened = await shell.Opened();

        opened.Step(ToNotifications);
        shell.Host.Settle();

        opened.Compose();
        ((ComposeScreen)opened.Screen).Text += "Hello";

        await opened.Send();
        shell.Host.Drain();

        var reads = shell.Timelines.Reads.Count;

        opened.Step(-ToNotifications);
        shell.Host.Settle();

        Assert.Equal(reads + 1, shell.Timelines.Reads.Count);
    }

    /// <summary>
    ///     Somebody followed from a follow list stands differently on it, so the list is read again rather than handed
    ///     back saying the opposite.
    /// </summary>
    [Fact]
    public async Task Tie_ForgetsACachedFollowListTheAccountIsOn()
    {
        var ben = AnAccount.With(id: "42", address: "ben@hachyderm.io");
        var carol = AnAccount.With(id: "7", address: "carol@hachyderm.io");

        var shell = new AShell
        {
            Timelines = FakeTimelineReader.Holding(Somebody),
            Accounts = FakeAccountRelationships.Holding(ben, carol),
        };

        var opened = await shell.Opened();

        await opened.OpenAuthor();
        shell.Host.Drain();

        opened.Press(ShellKey.W);
        shell.Host.Drain();

        shell.Accounts.NowShowing(carol);

        opened.Press(ShellKey.Enter);
        shell.Host.Drain();

        shell.Accounts.Becoming = carol with { Standing = AnAccount.Standing(following: true) };

        opened.Press(Pressing.Tying(AccountTie.Follow));
        shell.Host.Drain();

        opened.Back();

        // The list under the account screen heard it too, and the account screen under that is still Ben's.
        var listed = Assert.IsType<FollowsScreen>(opened.Screen);

        Assert.True(Assert.Single(listed.People).Standing?.Following);

        opened.Back();

        Assert.Equal("42", Assert.IsType<AccountScreen>(opened.Screen).Account.Id);

        var asked = shell.Accounts.Lists.Count(list => list.Side is not null);

        opened.Press(ShellKey.W);
        shell.Host.Drain();

        Assert.Equal("42", Assert.IsType<FollowsScreen>(opened.Screen).Whose.Id);
        Assert.Equal(asked + 1, shell.Accounts.Lists.Count(list => list.Side is not null));
    }

    /// <summary>
    ///     A dismissal is a notification fewer on the instance, however far the reader has gone by the time it lands:
    ///     the badge moves by one rather than being counted off a list nobody is holding any more.
    /// </summary>
    [Fact]
    public async Task Dismiss_MovesTheBadgeByOneWhereTheReaderHasArrivedSomewhereElse()
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
        opened.Rail.GoTo(DestinationKind.Home);
        shell.Host.Drain();

        Assert.IsType<FeedScreen>(opened.Screen);
        Assert.Equal(1, opened.Rail.Destinations.First(place => place.Kind == DestinationKind.Notifications).Unread);
    }

    /// <summary>A shell standing on Home again, having read Local on the way — so Local is held.</summary>
    private static async Task<Shell> OnHomeHavingReadLocal(AShell shell)
    {
        var opened = await shell.Opened();

        opened.Step(1);
        shell.Host.Settle();
        opened.Step(-1);
        shell.Host.Settle();

        return opened;
    }
}
