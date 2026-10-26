using Wooly.Core.Accounts;
using Wooly.Core.Posts;
using Wooly.Tests.Fakes;
using Wooly.Tui.Rendering;
using Wooly.Tui.Screens;
using Wooly.Tui.Shell;

namespace Wooly.Tests.Tui;

/// <summary>
///     <c>w</c> on a picked post opens who favorited or boosted it, one side at a time, on the actors screen
///     Notifications uses — and <c>s</c> swaps sides in place, the way it does on a follow list (#489).
/// </summary>
public class ShellPostActorsTests
{
    /// <summary>The height of the content region the reading-on tests say the page is, in rows.</summary>
    private const int Screenful = 20;

    /// <summary>
    ///     On a feed post with favorites, <c>w</c> opens "Favorited by", read from the post's own id a page of 80 at a
    ///     time, its rows the compact Account blocks the notification actors screen draws.
    /// </summary>
    [Fact]
    public async Task PressW_OnAFeedPostOpensWhoFavoritedIt()
    {
        var shell = Holding(APost.With(id: "110"));
        shell.Engagement.ActorsOf[("110", ActorSide.Favorited)] = Actors("Alice", "Bob");
        var opened = await shell.Opened();

        opened.Press(ShellKey.W);
        shell.Host.Drain();

        var screen = Assert.IsType<ActorsScreen>(opened.Screen);
        Assert.Equal("Favorited by", screen.Crumb);
        Assert.Equal(["Alice", "Bob"], screen.People.Select(person => person.Author));
        var read = Assert.Single(shell.Engagement.ActorReads);
        Assert.Equal(("110", ActorSide.Favorited, 80), (read.PostId, read.Side, read.Limit));
        Assert.Contains(Drawn(screen), line => line.Contains("bob@hachyderm.io", StringComparison.Ordinal));
    }

    /// <summary>On a boost it acts on the post boosted, as every other post verb does.</summary>
    [Fact]
    public async Task PressW_OnABoostReadsTheBoostedPost()
    {
        var shell = Holding(APost.With(id: "200", boosted: APost.With(id: "110")));
        shell.Engagement.ActorsOf[("110", ActorSide.Favorited)] = Actors("Alice");
        var opened = await shell.Opened();

        opened.Press(ShellKey.W);
        shell.Host.Drain();

        Assert.Equal("110", Assert.Single(shell.Engagement.ActorReads).PostId);
    }

    /// <summary>A post with boosts and no favorites opens on "Boosted by" rather than on an empty list.</summary>
    [Fact]
    public async Task PressW_OnAPostWithBoostsAndNoFavoritesOpensWhoBoostedIt()
    {
        var shell = Holding(APost.With(id: "110") with { Favorites = 0, Boosts = 2 });
        shell.Engagement.ActorsOf[("110", ActorSide.Boosted)] = Actors("Carol");
        var opened = await shell.Opened();

        opened.Press(ShellKey.W);
        shell.Host.Drain();

        Assert.Equal("Boosted by", opened.Screen.Crumb);
        Assert.Equal(ActorSide.Boosted, Assert.Single(shell.Engagement.ActorReads).Side);
    }

    /// <summary>
    ///     A post nobody has favorited or boosted says so and stays put: nothing is read and nothing is pushed — the
    ///     decision is the post's own counts, made before anything is fetched.
    /// </summary>
    [Fact]
    public async Task PressW_OnAPostNobodyFavoritedOrBoostedSaysSoAndStaysPut()
    {
        var shell = Holding(APost.With(id: "110") with { Favorites = 0, Boosts = 0 });
        var opened = await shell.Opened();
        var depth = opened.Depth;

        opened.Press(ShellKey.W);
        shell.Host.Drain();

        Assert.Equal("Nobody has favorited or boosted this.", opened.Notice);
        Assert.False(opened.NoticeIsError);
        Assert.Empty(shell.Engagement.ActorReads);
        Assert.Equal(depth, opened.Depth);
        Assert.IsType<FeedScreen>(opened.Screen);
    }

    /// <summary>
    ///     <c>w</c> on the post screen asks about the post being read, and on a reply in its thread about that reply.
    /// </summary>
    [Fact]
    public async Task PressW_InAThreadAsksAboutThePickedPostOrReply()
    {
        var shell = new AShell
        {
            Timelines = FakeTimelineReader.Holding(APost.With(id: "110")),
            Engagement = FakePostEngagement.Answered(
                APost.With(id: "110"),
                APost.With(id: "120", inReplyTo: new PostReplyTarget { PostId = "110" })),
        };
        var opened = await shell.Opened();

        await opened.Enter();
        shell.Host.Drain();
        Assert.IsType<PostScreen>(opened.Screen);

        opened.Press(ShellKey.W);
        shell.Host.Drain();
        opened.Back();
        opened.Move(1);
        opened.Press(ShellKey.W);
        shell.Host.Drain();

        Assert.Equal(["110", "120"], shell.Engagement.ActorReads.Select(read => read.PostId));
    }

    /// <summary>
    ///     <c>s</c> swaps to the other side in place: a new crumb over the same stack, so flipping back and forth never
    ///     grows it, and <c>esc</c> returns to the post.
    /// </summary>
    [Fact]
    public async Task PressS_SwapsSidesWithoutGrowingTheStack()
    {
        var shell = Holding(APost.With(id: "110"));
        shell.Engagement.ActorsOf[("110", ActorSide.Favorited)] = Actors("Alice");
        shell.Engagement.ActorsOf[("110", ActorSide.Boosted)] = Actors("Carol");
        var opened = await shell.Opened();

        opened.Press(ShellKey.W);
        shell.Host.Drain();
        var depth = opened.Depth;

        opened.Press(ShellKey.S);
        shell.Host.Drain();

        var screen = Assert.IsType<ActorsScreen>(opened.Screen);
        Assert.Equal("Boosted by", screen.Crumb);
        Assert.Equal(["Carol"], screen.People.Select(person => person.Author));
        Assert.Equal(depth, opened.Depth);

        opened.Press(ShellKey.S);
        shell.Host.Drain();

        Assert.Equal("Favorited by", opened.Screen.Crumb);
        Assert.Equal(depth, opened.Depth);
        Assert.Equal(
            [ActorSide.Favorited, ActorSide.Boosted, ActorSide.Favorited],
            shell.Engagement.ActorReads.Select(read => read.Side));

        opened.Back();

        Assert.IsType<FeedScreen>(opened.Screen);
    }

    /// <summary><c>g</c> reads the side the reader is on again, from the top.</summary>
    [Fact]
    public async Task Refresh_ReadsTheCurrentSideAgain()
    {
        var shell = Holding(APost.With(id: "110"));
        shell.Engagement.ActorsOf[("110", ActorSide.Boosted)] = Actors("Carol");
        var opened = await shell.Opened();

        opened.Press(ShellKey.W);
        shell.Host.Drain();
        opened.Press(ShellKey.S);
        shell.Host.Drain();
        shell.Engagement.ActorsOf[("110", ActorSide.Boosted)] = Actors("Carol", "Dan");

        opened.Press(ShellKey.G);
        shell.Host.Drain();

        Assert.Equal(3, shell.Engagement.ActorReads.Count);
        Assert.Equal(ActorSide.Boosted, shell.Engagement.ActorReads[^1].Side);
        Assert.Equal(["Carol", "Dan"], Assert.IsType<ActorsScreen>(opened.Screen).People.Select(p => p.Author));
    }

    /// <summary>
    ///     A long side reads on as the reader walks down it: the rest from where the first read stopped, of the same
    ///     post and side, with where the reader stands asked only of whoever is new.
    /// </summary>
    [Fact]
    public async Task Viewed_ReadsTheSideOnFromWhereTheFirstReadStopped()
    {
        var everyone = Enumerable.Range(1, 100).Select(at => $"Fan{at}").ToArray();
        var shell = Holding(APost.With(id: "110"));
        shell.Engagement.ActorsOf[("110", ActorSide.Favorited)] = Actors(everyone);
        var opened = await shell.Opened();

        opened.Press(ShellKey.W);
        shell.Host.Drain();

        var screen = Assert.IsType<ActorsScreen>(opened.Screen);
        Assert.Equal(80, screen.Read);

        shell.Accounts.ForgetStandings();
        opened.Viewed(under: 0, screenful: Screenful);
        shell.Host.Drain();

        var on = Assert.Single(shell.Engagement.ActorReadsOn);
        Assert.Equal(("110", ActorSide.Favorited, 80), (on.PostId, on.Side, on.Limit));
        Assert.Equal(everyone, screen.People.Select(person => person.Author));
        Assert.Equal(20, Assert.Single(shell.Accounts.Standings).AccountIds.Count);
        Assert.Equal(["", "that's everything"], Drawn(screen)[^2..]);
    }

    /// <summary><c>⏎</c> opens whoever is picked out, and <c>esc</c> walks back to the list.</summary>
    [Fact]
    public async Task Enter_OpensThePersonPickedOut()
    {
        var shell = Holding(APost.With(id: "110"));
        shell.Engagement.ActorsOf[("110", ActorSide.Favorited)] = Actors("Alice", "Bob");
        var opened = await shell.Opened();

        opened.Press(ShellKey.W);
        shell.Host.Drain();
        opened.Move(1);
        opened.Press(ShellKey.Enter);
        shell.Host.Drain();

        Assert.IsType<AccountScreen>(opened.Screen);
        Assert.Equal("bob@hachyderm.io", shell.Accounts.Reads[^1].Account.Text);

        opened.Back();

        Assert.Equal("Bob", Assert.IsType<ActorsScreen>(opened.Screen).PickedPerson?.Author);
    }

    /// <summary>A side that reads back empty — the count was stale — says so rather than drawing nothing.</summary>
    [Theory]
    [InlineData(5, 0, "Nobody has favorited this.")]
    [InlineData(0, 3, "Nobody has boosted this.")]
    public async Task Arrived_AnEmptySideSaysNobodyIsOnIt(long favorites, long boosts, string nobody)
    {
        var shell = Holding(APost.With(id: "110") with { Favorites = favorites, Boosts = boosts });
        var opened = await shell.Opened();

        opened.Press(ShellKey.W);
        shell.Host.Drain();

        Assert.Contains(nobody, Drawn(Assert.IsType<ActorsScreen>(opened.Screen)));
    }

    /// <summary>The status row offers <c>w:who</c> while a post is picked, and <c>s</c> on the screen it opens.</summary>
    [Fact]
    public async Task Keys_OfferWhoOnAPostAndTheSwapOnItsActors()
    {
        var shell = Holding(APost.With(id: "110"));
        shell.Engagement.ActorsOf[("110", ActorSide.Favorited)] = Actors("Alice");
        var opened = await shell.Opened();

        Assert.Contains(opened.Screen.Keys, key => key is { Key: "w", Does: "who" });

        opened.Press(ShellKey.W);
        shell.Host.Drain();

        Assert.Contains(opened.Screen.Keys, key => key is { Key: "s", Does: "boosted by" });
    }

    /// <summary>
    ///     On an account's screen <c>w</c> goes by what is picked (#490): with one of their posts picked, that post's
    ///     who — the same as on any feed, a boost's inner post included.
    /// </summary>
    [Fact]
    public async Task PressW_OnAPostOnAnAccountScreenOpensItsWho()
    {
        var shell = OnBensTimeline(APost.With(id: "200", account: "ben@hachyderm.io", boosted: APost.With(id: "110")));
        shell.Engagement.ActorsOf[("110", ActorSide.Favorited)] = Actors("Alice");
        var opened = await OnBensAccount(shell);
        var listed = shell.Accounts.Lists.Count;

        opened.Move(1);
        opened.Press(ShellKey.W);
        shell.Host.Drain();

        Assert.Equal("Favorited by", Assert.IsType<ActorsScreen>(opened.Screen).Crumb);
        Assert.Equal("110", Assert.Single(shell.Engagement.ActorReads).PostId);
        Assert.Equal(listed, shell.Accounts.Lists.Count);
    }

    /// <summary>With the header block picked it opens their follows, as it always has, and asks nothing of a post.</summary>
    [Fact]
    public async Task PressW_OnTheHeaderOfAnAccountScreenOpensTheirFollows()
    {
        var shell = OnBensTimeline(APost.With(id: "110", account: "ben@hachyderm.io"));
        var opened = await OnBensAccount(shell);

        opened.Press(ShellKey.W);
        shell.Host.Drain();

        Assert.IsType<FollowsScreen>(opened.Screen);
        Assert.Empty(shell.Engagement.ActorReads);
    }

    /// <summary>A post of theirs nobody has favorited or boosted says so there too, and stays put.</summary>
    [Fact]
    public async Task PressW_OnAnAccountsPostNobodyFavoritedOrBoostedSaysSo()
    {
        var shell = OnBensTimeline(APost.With(id: "110", account: "ben@hachyderm.io") with { Favorites = 0, Boosts = 0 });
        var opened = await OnBensAccount(shell);
        var listed = shell.Accounts.Lists.Count;

        opened.Move(1);
        opened.Press(ShellKey.W);
        shell.Host.Drain();

        Assert.Equal("Nobody has favorited or boosted this.", opened.Notice);
        Assert.IsType<AccountScreen>(opened.Screen);
        Assert.Empty(shell.Engagement.ActorReads);
        Assert.Equal(listed, shell.Accounts.Lists.Count);
    }

    /// <summary>The status row says what <c>w</c> will do, and changes as the pick moves: follows, then who.</summary>
    [Fact]
    public async Task Keys_OnAnAccountScreenSayWhatWDoesForThePick()
    {
        var opened = await OnBensAccount(OnBensTimeline(APost.With(id: "110", account: "ben@hachyderm.io")));

        Assert.Equal(["follows"], opened.Screen.Keys.Where(key => key.Key == "w").Select(key => key.Does));

        opened.Move(1);

        Assert.Equal(["who"], opened.Screen.Keys.Where(key => key.Key == "w").Select(key => key.Does));

        opened.Move(-1);

        Assert.Equal(["follows"], opened.Screen.Keys.Where(key => key.Key == "w").Select(key => key.Does));
    }

    /// <summary>Fakes whose timeline holds <paramref name="post" /> of Ben's, and who answer about Ben.</summary>
    private static AShell OnBensTimeline(Post post) => new()
    {
        Timelines = FakeTimelineReader.Holding(post),
        Accounts = FakeAccountRelationships.Holding(AnAccount.With(address: "ben@hachyderm.io")),
    };

    /// <summary>A shell drilled into Ben's account from the post on the timeline, the header block picked.</summary>
    private static async Task<Shell> OnBensAccount(AShell shell)
    {
        var opened = await shell.Opened();

        await opened.OpenAuthor();
        shell.Host.Drain();
        Assert.IsType<AccountScreen>(opened.Screen);

        return opened;
    }

    private static AShell Holding(Post post) => new() { Timelines = FakeTimelineReader.Holding(post) };

    private static IReadOnlyList<Account> Actors(params string[] names) =>
    [
        .. names.Select((name, at) => AnAccount.With(
            address: $"{name.ToLowerInvariant()}@hachyderm.io",
            author: name,
            id: $"{100 + at}")),
    ];

    /// <summary>The screen's rows as text.</summary>
    private static List<string> Drawn(Screen screen) =>
        [.. screen.Lines(new Drawing(61, AShell.Now)).Select(line => line.Text)];
}
