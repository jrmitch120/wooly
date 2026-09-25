using Wooly.Core.Paging;
using Wooly.Core.Posts;
using Wooly.Core.Timelines;
using Wooly.Tests.Fakes;
using Wooly.Tui.Rendering;
using Wooly.Tui.Screens;
using Wooly.Tui.Shell;

namespace Wooly.Tests.Tui;

/// <summary>
///     <c>s</c> on the account screen: an account's <b>Posts and replies</b>, and back to its posts alone, in place
///     (#229). The swap a follow list already makes between its sides (#180), made between the two runs of one
///     account's timeline.
/// </summary>
public class AccountRepliesTests
{
    /// <summary>Whoever the account screen is about in these tests.</summary>
    private static readonly string Whose = "ben@hachyderm.io";

    /// <summary>A reply of theirs to somebody else, which their posts alone leave out.</summary>
    private static readonly Post Reply = APost.With(
        id: "440",
        account: Whose,
        inReplyTo: new PostReplyTarget { PostId = "1", Handle = "sam@hachyderm.io" });

    /// <summary>
    ///     The account screen still opens on their posts alone: the screen-reader reasoning in ADR-0019 is the
    ///     default, and only the reader's own press widens it.
    /// </summary>
    [Fact]
    public async Task TheAccountScreenOpensOnTheirPostsAlone()
    {
        var (fakes, shell) = await OnTheAccountScreen();

        var screen = Assert.IsType<AccountScreen>(shell.Screen);

        Assert.False(screen.WithReplies);
        Assert.Equal($"@{Whose}", screen.Crumb);
        Assert.DoesNotContain(fakes.Timelines.Reads, read => read.Timeline.Scope == TimelineScope.WithReplies);
    }

    /// <summary>
    ///     <c>s</c> re-asks their timeline with the replies in, and puts it where the screen stood: a new crumb, the
    ///     stack no deeper, and the run it asked for drawn under a heading that says so.
    /// </summary>
    [Fact]
    public async Task SwapReplies_ShowsTheirPostsAndRepliesWithoutGrowingTheStack()
    {
        var (fakes, shell) = await OnTheAccountScreen();
        var depth = shell.Depth;

        await shell.SwapReplies();
        fakes.Host.Drain();

        var screen = Assert.IsType<AccountScreen>(shell.Screen);

        Assert.True(screen.WithReplies);
        Assert.Equal($"@{Whose} posts and replies", screen.Crumb);
        Assert.Equal(depth, shell.Depth);
        Assert.Equal(TimelineScope.WithReplies, LastRun(fakes).Scope);
        Assert.Equal(Whose, LastRun(fakes).Account?.Address.Text);
        Assert.Equal(["440", "220"], screen.Posts.Select(post => post.Id));
        Assert.Contains("── their posts and replies ──", Headings(screen));
    }

    /// <summary>And a second <c>s</c> goes back to their posts alone, the stack still no deeper.</summary>
    [Fact]
    public async Task SwapReplies_TwiceReturnsToTheirPostsAlone()
    {
        var (fakes, shell) = await OnTheAccountScreen();
        var depth = shell.Depth;

        await shell.SwapReplies();
        fakes.Host.Drain();
        await shell.SwapReplies();
        fakes.Host.Drain();

        var screen = Assert.IsType<AccountScreen>(shell.Screen);

        Assert.False(screen.WithReplies);
        Assert.Equal($"@{Whose}", screen.Crumb);
        Assert.Equal(depth, shell.Depth);
        Assert.Equal(TimelineScope.Account, LastRun(fakes).Scope);
        Assert.Equal(["220"], screen.Posts.Select(post => post.Id));
        Assert.Contains("── their posts ──", Headings(screen));
    }

    /// <summary>
    ///     The swap is a fresh screen, so the pick goes back to the header block the way a follow list's swap puts
    ///     it back at the top.
    /// </summary>
    [Fact]
    public async Task SwapReplies_PutsThePickBackOnTheHeader()
    {
        var (fakes, shell) = await OnTheAccountScreen();

        shell.Screen.Move(1);
        Assert.NotNull(shell.Screen.Picked);

        await shell.SwapReplies();
        fakes.Host.Drain();

        Assert.Null(shell.Screen.Picked);
    }

    /// <summary>
    ///     With replies in, a pinned reply can come back in both runs, and it is still drawn once — in the pinned run,
    ///     not in the run below it.
    /// </summary>
    [Fact]
    public async Task APinnedReplyIsDrawnOnceAndInThePinnedRun()
    {
        var (fakes, shell) = await OnTheAccountScreen(pinned: [Reply]);

        await shell.SwapReplies();
        fakes.Host.Drain();

        var screen = Assert.IsType<AccountScreen>(shell.Screen);

        Assert.Equal(["440", "220"], screen.Posts.Select(post => post.Id));
        Assert.Equal(["── 1 pinned ──", "── their posts and replies ──"], Headings(screen));
    }

    /// <summary><c>g</c> on the widened screen re-asks the widened run, not the default one.</summary>
    [Fact]
    public async Task RefreshingTheWidenedScreenReadsTheirPostsAndRepliesAgain()
    {
        var (fakes, shell) = await OnTheAccountScreen();

        await shell.SwapReplies();
        fakes.Host.Drain();

        var reads = fakes.Timelines.Reads.Count;

        await shell.Refresh();
        fakes.Host.Drain();

        var screen = Assert.IsType<AccountScreen>(shell.Screen);

        Assert.True(screen.WithReplies);
        Assert.Equal($"@{Whose} posts and replies", screen.Crumb);
        Assert.Contains(
            fakes.Timelines.Reads.Skip(reads),
            read => read.Timeline.Scope == TimelineScope.WithReplies);
        Assert.DoesNotContain(fakes.Timelines.Reads.Skip(reads), read => read.Timeline.Scope == TimelineScope.Account);
    }

    /// <summary>And <c>s</c> does nothing off the account screen, there being no account's timeline to widen.</summary>
    [Fact]
    public async Task SwapReplies_DoesNothingOffTheAccountScreen()
    {
        var fakes = new AShell();
        var shell = await fakes.Opened();
        var reads = fakes.Timelines.Reads.Count;

        await shell.SwapReplies();
        fakes.Host.Drain();

        Assert.IsNotType<AccountScreen>(shell.Screen);
        Assert.Equal(reads, fakes.Timelines.Reads.Count);
    }

    /// <summary>
    ///     The status row says what <c>s</c> swaps to, the way a follow list's says which side — beside <c>w</c>, the
    ///     other key this screen took for itself.
    /// </summary>
    [Fact]
    public void TheSwapKeySaysWhichRunItSwapsTo()
    {
        var posts = new AccountScreen(AnAccount.With(address: Whose), [], pinned: []);
        var widened = new AccountScreen(AnAccount.With(address: Whose), [], pinned: [], withReplies: true);

        Assert.Equal("posts and replies", Assert.Single(posts.Keys, key => key.Key == "s").Does);
        Assert.Equal("posts", Assert.Single(widened.Keys, key => key.Key == "s").Does);
        Assert.Equal("w", posts.Keys[posts.Keys.ToList().FindIndex(key => key.Key == "s") - 1].Key);
    }

    /// <summary>The run last asked of their timeline, pinned run aside.</summary>
    private static Timeline LastRun(AShell fakes) =>
        fakes.Timelines.Reads.Last(read => read.Timeline.Scope is TimelineScope.Account or TimelineScope.WithReplies)
            .Timeline;

    /// <summary>What the rows head their runs with, in the order they are drawn.</summary>
    private static IEnumerable<string> Headings(Screen screen) =>
        screen.Lines(new Drawing(61, AShell.Now)).Where(line => line.Heads).Select(line => line.Text);

    /// <summary>
    ///     A shell drilled into <see cref="Whose" />'s account screen. Their posts alone are one post; with replies in
    ///     they are that post and <see cref="Reply" />, newest first.
    /// </summary>
    private static async Task<(AShell Fakes, Shell Shell)> OnTheAccountScreen(IReadOnlyList<Post>? pinned = null)
    {
        var fakes = new AShell
        {
            Timelines = FakeTimelineReader.Answering(timeline => timeline.Scope switch
            {
                TimelineScope.Pinned => Fetch<Post>.Complete(pinned ?? []),
                TimelineScope.Account => Fetch<Post>.Complete([APost.With(id: "220", account: Whose)]),
                TimelineScope.WithReplies => Fetch<Post>.Complete([Reply, APost.With(id: "220", account: Whose)]),
                _ => Fetch<Post>.Complete([APost.With(id: "110", account: Whose)]),
            }),
            Accounts = FakeAccountRelationships.Holding(AnAccount.With(address: Whose)),
        };

        var shell = await fakes.Opened();

        await shell.OpenAuthor();

        fakes.Host.Drain();

        return (fakes, shell);
    }
}
