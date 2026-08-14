using Wooly.Core.Errors;
using Wooly.Core.Paging;
using Wooly.Core.Posts;
using Wooly.Core.Timelines;
using Wooly.Tests.Fakes;
using Wooly.Tui.Screens;
using Wooly.Tui.Shell;

namespace Wooly.Tests.Tui;

/// <summary>
///     Story 53: a rate limit is waited out with a countdown the reader can see rather than failing — the opposite of
///     the CLI, which reports it and exits so that no script ever hangs (ADR-0006). The difference is not a
///     disagreement about rate limits; it is that a person can watch a countdown and a script cannot.
/// </summary>
public class RateLimitWaitTests
{
    /// <summary>The call is made again once the window the instance named has rolled over, and it lands.</summary>
    [Fact]
    public async Task Open_WaitsOutARateLimitAndThenReadsTheTimeline()
    {
        var attempts = 0;
        var shell = new AShell();

        shell.Timelines = FakeTimelineReader.Awaiting(_ =>
        {
            attempts++;

            return attempts == 1
                ? Task.FromException<Fetch<Post>>(
                    new RateLimitedException("mastodon.social", AShell.Now + TimeSpan.FromSeconds(3)))
                : Task.FromResult(Fetch<Post>.Complete([APost.With(id: "110")]));
        });

        var opened = shell.Build();
        var opening = opened.Open();

        shell.Host.Drain();

        // The limit is being waited out, and the reader is told what for and for how long.
        Assert.Contains("Rate limited by mastodon.social", opened.Notice);
        Assert.Contains("3s", opened.Notice);
        Assert.False(opened.NoticeIsError);

        shell.Clock.Advance(TimeSpan.FromSeconds(3));
        shell.Host.SettleAll();

        await opening;
        shell.Host.Drain();

        Assert.Equal(2, attempts);
        Assert.Null(opened.Notice);

        var feed = Assert.IsType<FeedScreen>(opened.Screen);
        Assert.Equal(["110"], feed.Posts.Select(post => post.Id));
    }

    /// <summary>The countdown counts down, which is what makes it a countdown rather than a stuck message.</summary>
    [Fact]
    public async Task Open_CountsTheWaitDownWhereTheReaderCanSeeIt()
    {
        var attempts = 0;
        var shell = new AShell();

        shell.Timelines = FakeTimelineReader.Awaiting(_ =>
        {
            attempts++;

            return attempts == 1
                ? Task.FromException<Fetch<Post>>(
                    new RateLimitedException("mastodon.social", AShell.Now + TimeSpan.FromSeconds(5)))
                : Task.FromResult(Fetch<Post>.Complete([]));
        });

        var opened = shell.Build();
        var opening = opened.Open();

        shell.Host.Drain();

        Assert.Contains("5s", opened.Notice);

        shell.Clock.Advance(TimeSpan.FromSeconds(2));
        shell.Host.Settle();

        Assert.Contains("3s", opened.Notice);

        shell.Clock.Advance(TimeSpan.FromSeconds(3));
        shell.Host.SettleAll();

        await opening;
        shell.Host.Drain();

        Assert.Null(opened.Notice);
    }

    /// <summary>
    ///     Anything a wait cannot mend is said out loud instead, in the role that says it is a failure — and the shell
    ///     stays open, because a shell that closed on one bad answer would be worse than the CLI it is not.
    /// </summary>
    [Fact]
    public async Task Open_SaysAFailureOutLoudRatherThanWaitingOnIt()
    {
        var shell = new AShell
        {
            Timelines = FakeTimelineReader.Awaiting(_ =>
                Task.FromException<Fetch<Post>>(new AuthenticationException("That token has been revoked."))),
        };

        var opened = await shell.Opened();

        Assert.Equal("That token has been revoked.", opened.Notice);
        Assert.True(opened.NoticeIsError);
    }

    /// <summary>
    ///     A timeline cut short by a limit part way through is drawn with what did arrive, and says it was cut short —
    ///     the fetch already carries both, and reporting an empty timeline to somebody who has one is the failure worth
    ///     avoiding.
    /// </summary>
    [Fact]
    public async Task Open_DrawsWhatArrivedWhenARateLimitStoppedTheReadPartWayThrough()
    {
        var shell = new AShell { Timelines = FakeTimelineReader.RateLimitedAfter(APost.With(id: "110")) };
        var opened = await shell.Opened();

        var feed = Assert.IsType<FeedScreen>(opened.Screen);

        Assert.Equal(["110"], feed.Posts.Select(post => post.Id));
        Assert.Contains("Rate limited part way through", feed.Notice);
    }

    /// <summary>
    ///     A count the rail could not read is drawn as no count. It is the least of what is on screen, and refusing to
    ///     open over a badge would be trading the whole shell for a number.
    /// </summary>
    [Fact]
    public async Task Open_OpensEvenWhereTheRailsCountsCouldNotBeRead()
    {
        var shell = new AShell
        {
            Notifications = FakeNotificationInbox.RateLimitedAfter(),
            Accounts = FakeAccountRelationships.Refusing(new AuthenticationException("No.")),
        };

        var opened = await shell.Opened();

        Assert.IsType<FeedScreen>(opened.Screen);
        Assert.All(opened.Rail.Destinations, destination => Assert.Equal(0, destination.Unread));
    }

    /// <summary>The counts the rail carries are read from the same ports the CLI's own commands read them from.</summary>
    [Fact]
    public async Task Open_ReadsTheUnreadCountsTheRailCarries()
    {
        var shell = new AShell
        {
            Notifications = FakeNotificationInbox.Holding(ANotification.With(id: "1"), ANotification.With(id: "2")),
            Messages = FakeDirectMessages.Holding(
                AConversation.With(id: "c1", unread: true),
                AConversation.With(id: "c2", unread: false)),
            Accounts = FakeAccountRelationships.Holding(
                subject: null,
                AnAccount.With(),
                AnAccount.With(address: "bob@mas.to")),
        };

        var opened = await shell.Opened();

        Assert.Equal(2, Unread(opened, DestinationKind.Notifications));
        Assert.Equal(1, Unread(opened, DestinationKind.Messages));
        Assert.Equal(2, Unread(opened, DestinationKind.Requests));
    }

    private static int Unread(Shell shell, DestinationKind kind) =>
        shell.Rail.Destinations.First(destination => destination.Kind == kind).Unread;
}
