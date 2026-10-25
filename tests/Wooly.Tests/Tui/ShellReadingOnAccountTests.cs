using Wooly.Core.Posts;
using Wooly.Core.Timelines;
using Wooly.Tests.Fakes;
using Wooly.Tui.Rendering;
using Wooly.Tui.Screens;
using Wooly.Tui.Shell;

namespace Wooly.Tests.Tui;

/// <summary>
///     Reading on, on an account's screen (#464): their posts read on below the pinned run, following whichever run is
///     showing, and the pinned run, which is one page in the account's own order, never read on at all.
/// </summary>
public class ShellReadingOnAccountTests
{
    /// <summary>The height of the content region these tests say the page is, in rows.</summary>
    private const int Screenful = 20;

    /// <summary>Whoever the account screen is about in these tests.</summary>
    private const string Whose = "ben@hachyderm.io";

    /// <summary>
    ///     The page near the end of their posts reads their posts on, and puts the older ones under those already
    ///     held: below the pinned run, which stays where it was at the top.
    /// </summary>
    [Fact]
    public async Task Viewed_NearTheEndOfTheirPosts_PutsTheirOlderPostsUnderThoseHeld()
    {
        var (built, shell) = await OnTheAccountScreen(pinned: ["p1", "p2"]);

        shell.Viewed(under: 0, screenful: Screenful);
        built.Host.Drain();

        var screen = Assert.IsType<AccountScreen>(shell.Screen);
        var read = Assert.Single(built.Timelines.ReadsOn).Timeline;
        Assert.Equal(TimelineScope.Account, read.Scope);
        Assert.Equal(Whose, read.Account?.Address.Text);
        Assert.Equal(["p1", "p2", .. Ids("", 1, 80)], screen.Posts.Select(post => post.Id));
        Assert.Equal(["── 2 pinned ──", "── their posts ──"], Headings(screen));
    }

    /// <summary>
    ///     Read on to the end of their posts, the foot says it is whole — and the pinned run was never read on, however
    ///     far down the page went.
    /// </summary>
    [Fact]
    public async Task ReadOnToTheEnd_SaysThatsEverything_AndNeverReadsThePinnedRunOn()
    {
        var (built, shell) = await OnTheAccountScreen(pinned: ["p1"], posts: 50);

        shell.Viewed(under: 0, screenful: Screenful);
        built.Host.Drain();
        shell.Viewed(under: 0, screenful: Screenful);
        built.Host.Drain();

        Assert.All(built.Timelines.ReadsOn, read => Assert.Equal(TimelineScope.Account, read.Timeline.Scope));
        Assert.Single(built.Timelines.ReadsOn);
        Assert.Equal("that's everything", Foot(shell.Screen));
    }

    /// <summary>With <b>Posts and replies</b> showing, reading on reads that run, not their posts alone.</summary>
    [Fact]
    public async Task Viewed_WithPostsAndRepliesShowing_ReadsThatRunOn()
    {
        var (built, shell) = await OnTheAccountScreen(pinned: []);

        shell.Press(ShellKey.S);
        built.Host.Drain();
        shell.Viewed(under: 0, screenful: Screenful);
        built.Host.Drain();

        var screen = Assert.IsType<AccountScreen>(shell.Screen);
        Assert.Equal(TimelineScope.WithReplies, Assert.Single(built.Timelines.ReadsOn).Timeline.Scope);
        Assert.Equal(Ids("r", 1, 80), screen.Posts.Select(post => post.Id));
    }

    /// <summary>
    ///     A post they pinned long ago comes back in an older page of their posts too, and is still drawn once — in the
    ///     pinned run, not again in the run below it (#182).
    /// </summary>
    [Fact]
    public async Task APinnedPostReadOnAgain_IsDrawnOnceAndInThePinnedRun()
    {
        var (built, shell) = await OnTheAccountScreen(pinned: ["60"]);

        shell.Viewed(under: 0, screenful: Screenful);
        built.Host.Drain();

        var screen = Assert.IsType<AccountScreen>(shell.Screen);
        Assert.Equal(["60", .. Ids("", 1, 59), .. Ids("", 61, 80)], screen.Posts.Select(post => post.Id));
    }

    /// <summary>What the rows head their runs with, in the order they are drawn.</summary>
    private static IEnumerable<string> Headings(Screen screen) =>
        screen.Lines(new Drawing(61, AShell.Now)).Where(line => line.Heads).Select(line => line.Text);

    /// <summary>The last row <paramref name="screen" /> draws, which is the foot of its list where it has one.</summary>
    private static string Foot(Screen screen) => screen.Lines(new Drawing(61, AShell.Now))[^1].Text;

    /// <summary>
    ///     A shell drilled into <see cref="Whose" />'s account screen from Home. Their posts are
    ///     <paramref name="posts" /> posts with ids counting up from 1, newest first, and with replies in the same
    ///     many counting up from <c>r1</c>; the posts they pinned are those <paramref name="pinned" /> names, among
    ///     their posts or not.
    /// </summary>
    private static async Task<(AShell Built, Shell Shell)> OnTheAccountScreen(
        IReadOnlyList<string> pinned,
        int posts = 100)
    {
        var built = new AShell
        {
            Timelines = FakeTimelineReader.ReadingOn(timeline => timeline.Scope switch
            {
                TimelineScope.Pinned => [.. pinned.Select(By)],
                TimelineScope.Account => [.. Ids("", 1, posts).Select(By)],
                TimelineScope.WithReplies => [.. Ids("r", 1, posts).Select(By)],
                _ => [By("h1")],
            }),
            Accounts = FakeAccountRelationships.Holding(AnAccount.With(address: Whose)),
        };

        var shell = await built.Opened();

        await shell.OpenAuthor();
        built.Host.Drain();

        return (built, shell);
    }

    /// <summary>A post of <see cref="Whose" />'s, with the id <paramref name="id" />.</summary>
    private static Post By(string id) => APost.With(id: id, account: Whose);

    /// <summary>The ids from <paramref name="first" /> to <paramref name="last" />, in order, each after <paramref name="prefix" />.</summary>
    private static IEnumerable<string> Ids(string prefix, int first, int last) =>
        Enumerable.Range(first, last - first + 1).Select(at => $"{prefix}{at}");
}
