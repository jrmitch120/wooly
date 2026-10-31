using Wooly.Core.Posts;
using Wooly.Core.Timelines;
using Wooly.Tests.Fakes;
using Wooly.Tui.Rendering;
using Wooly.Tui.Screens;
using Wooly.Tui.Shell;

namespace Wooly.Tests.Tui;

/// <summary>
///     Reading on, on every timeline as Home does it (#464): Local, Federated, a tag set on the rail (#511), and a
///     hashtag walked to. The trigger, the one-at-a-time rule and the cache are Home's and said in <see
///     cref="ShellReadingOnTests" />; what these say is that each of the others reads on its own timeline and says
///     whole at the foot.
/// </summary>
public class ShellReadingOnTimelinesTests
{
    /// <summary>The height of the content region these tests say the page is, in rows.</summary>
    private const int Screenful = 20;

    /// <summary>The page within a screenful of the end of one of the rail's timelines reads on that timeline.</summary>
    [Theory]
    [InlineData("Local")]
    [InlineData("Federated")]
    [InlineData("dotnet")]
    [InlineData("cats")]
    public async Task Viewed_NearTheEndOfARailTimeline_ReadsOnThatTimeline(string label)
    {
        var built = Timelines(posts: 100);
        var opened = await built.Opened();

        opened.Rail.GoTo(Place(opened.Rail, label));
        built.Host.Drain();
        opened.Viewed(under: 0, screenful: Screenful);
        built.Host.Drain();

        var feed = Assert.IsType<FeedScreen>(opened.Screen);
        Assert.Equal(Place(opened.Rail, label).Timeline, Assert.Single(built.Timelines.ReadsOn).Timeline);
        Assert.Equal(Ids(1, 80), feed.Posts.Select(post => post.Id));
    }

    /// <summary>And read on to its end, the foot says it is whole and nothing more is asked.</summary>
    [Theory]
    [InlineData("Local")]
    [InlineData("Federated")]
    [InlineData("dotnet")]
    [InlineData("cats")]
    public async Task Viewed_AtTheEndOfARailTimeline_SaysThatsEverything(string label)
    {
        var built = Timelines(posts: 50);
        var opened = await built.Opened();

        opened.Rail.GoTo(Place(opened.Rail, label));
        built.Host.Drain();
        opened.Viewed(under: 0, screenful: Screenful);
        built.Host.Drain();
        opened.Viewed(under: 0, screenful: Screenful);
        built.Host.Drain();

        Assert.Single(built.Timelines.ReadsOn);
        Assert.Equal("that's everything", Foot(opened.Screen));
    }

    /// <summary>
    ///     A hashtag walked to, rather than a set of the rail's, reads on all the same: the page near the end of
    ///     <c>#cats</c> reads on <c>#cats</c>, under the posts its first read brought.
    /// </summary>
    [Fact]
    public async Task Viewed_NearTheEndOfAWalkedToHashtag_ReadsOnThatTag()
    {
        var built = Timelines(posts: 100);
        var opened = await built.Opened();

        WalkedTo(built, opened, "#cats");
        opened.Viewed(under: 0, screenful: Screenful);
        built.Host.Drain();

        var feed = Assert.IsType<FeedScreen>(opened.Screen);
        Assert.Equal("Search › #cats", opened.Breadcrumb);
        Assert.Equal(Timeline.Tag("cats"), Assert.Single(built.Timelines.ReadsOn).Timeline);
        Assert.Equal(Ids(1, 80), feed.Posts.Select(post => post.Id));
    }

    /// <summary>
    ///     And a quiet tag whose first read already held everything says so straight away, and is never asked for
    ///     more.
    /// </summary>
    [Fact]
    public async Task AWalkedToHashtagReadWholeAtOnce_SaysThatsEverything_AndAsksNothing()
    {
        var built = Timelines(posts: 3);
        var opened = await built.Opened();

        WalkedTo(built, opened, "#cats");
        opened.Viewed(under: 0, screenful: Screenful);
        built.Host.Drain();

        Assert.Empty(built.Timelines.ReadsOn);
        Assert.Equal("that's everything", Foot(opened.Screen));
    }

    /// <summary>Walks to <paramref name="tag" /> the way a reader does: typed into search, and <c>⏎</c>.</summary>
    private static void WalkedTo(AShell built, Shell shell, string tag)
    {
        shell.Search();
        built.Host.Drain();

        foreach (var letter in tag)
        {
            shell.Type(letter);
        }

        shell.Press(ShellKey.Enter);
        built.Host.Drain();
    }

    /// <summary>Every row <paramref name="screen" /> draws, as text, the gutter and all.</summary>
    private static IReadOnlyList<string> Rows(Screen screen) =>
        [.. screen.Lines(new Drawing(61, AShell.Now)).Select(line => line.Text)];

    /// <summary>The last row <paramref name="screen" /> draws, which is the foot of its list where it has one.</summary>
    private static string Foot(Screen screen) => Rows(screen)[^1];

    /// <summary>The rail's destination called <paramref name="label" />.</summary>
    private static Destination Place(Rail rail, string label) =>
        rail.Destinations.Single(destination => destination.Label == label);

    /// <summary>
    ///     Every timeline holding <paramref name="posts" /> posts, newest first, ids counting up from 1, which reads on
    ///     — the rail keeping places for two tag sets, <c>dotnet</c> and <c>cats</c>.
    /// </summary>
    private static AShell Timelines(int posts) => new()
    {
        TagSets = [TagSet.Of("dotnet", ["dotnet", "csharp"]), TagSet.Of("cats", ["cats"])],
        Timelines = FakeTimelineReader.ReadingOn([.. Ids(1, posts).Select(id => APost.With(id: id))]),
    };

    /// <summary>The ids from <paramref name="first" /> to <paramref name="last" />, in order.</summary>
    private static IEnumerable<string> Ids(int first, int last) =>
        Enumerable.Range(first, last - first + 1).Select(at => $"{at}");
}
