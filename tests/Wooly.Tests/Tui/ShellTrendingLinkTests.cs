using Terminal.Gui.Input;
using Wooly.Core.Posts;
using Wooly.Core.Timelines;
using Wooly.Core.Trends;
using Wooly.Tests.Fakes;
using Wooly.Tui.Rendering;
using Wooly.Tui.Screens;
using Wooly.Tui.Shell;
using Wooly.Tui.Theme;

namespace Wooly.Tests.Tui;

/// <summary>
///     Opening a <b>Trending link</b> (#607): the posts sharing it pushed over the search prompt, read on as a feed is,
///     the article one key away in the browser, and a link that has stopped trending told apart — on opening, and while
///     reading on — through <see cref="FakeTimelineReader" /> and <see cref="FakeTrends" />, never HTTP.
/// </summary>
public class ShellTrendingLinkTests
{
    private const string Article = "https://news.example/cats-learn-to-type";

    private const int Screenful = 20;

    /// <summary>
    ///     <c>⏎</c> on a trending link pushes the posts sharing it, crumbed with the article's title — what people are
    ///     saying about it being why it trends.
    /// </summary>
    [Fact]
    public async Task Enter_OnATrendingLink_PushesThePostsSharingItCrumbedWithItsTitle()
    {
        var built = Sharing(posts: 3);
        var opened = await built.Opened();

        PickedTheLink(built, opened);
        opened.Press(ShellKey.Enter);
        built.Host.Drain();

        var feed = Assert.IsType<FeedScreen>(opened.Screen);
        Assert.Equal("Search › Cats learn to type", opened.Breadcrumb);
        Assert.Equal(Timeline.Sharing(new Uri(Article)), built.Timelines.Reads[^1].Timeline);
        Assert.Equal(Ids(1, 3), feed.Posts.Select(post => post.Id));
    }

    /// <summary>
    ///     The posts sharing a link read on by <c>max_id</c> as a feed does: the page near the end of them asks for what
    ///     stands behind the last one, and puts it under what was read.
    /// </summary>
    [Fact]
    public async Task Viewed_NearTheEndOfALinksPosts_ReadsOnThatLink()
    {
        var built = Sharing(posts: 100);
        var opened = await built.Opened();

        Opened(built, opened);
        opened.Viewed(under: 0, screenful: Screenful);
        built.Host.Drain();

        var feed = Assert.IsType<FeedScreen>(opened.Screen);
        var readOn = Assert.Single(built.Timelines.ReadsOn);
        Assert.Equal(Timeline.Sharing(new Uri(Article)), readOn.Timeline);
        Assert.Equal("40", readOn.From.MaxId);
        Assert.Equal(Ids(1, 80), feed.Posts.Select(post => post.Id));
    }

    /// <summary>A post's keys act on the posts sharing a link, as on any feed.</summary>
    [Fact]
    public async Task PostKeys_ActOnThePostsSharingALink()
    {
        var built = Sharing(posts: 3);
        var opened = await built.Opened();

        Opened(built, opened);
        opened.Move(1);
        opened.Press(ShellKey.F);
        built.Host.Drain();

        var marked = Assert.Single(built.Engagement.Marks);
        Assert.Equal("2", marked.PostId);
        Assert.Equal(PostMark.Favorite, marked.Mark);
    }

    /// <summary>
    ///     <c>o</c> on the posts sharing a link opens the article itself in the browser, and the status row offers it
    ///     there.
    /// </summary>
    [Fact]
    public async Task PressO_OnThePostsSharingALink_OpensTheArticleInTheBrowser()
    {
        var built = Sharing(posts: 3);
        var opened = await built.Opened();

        Opened(built, opened);

        Assert.Contains(opened.Screen.Keys, key => key is { Key: "o", Does: "article" });

        opened.Press(ShellKey.O);

        Assert.Equal(Article, Assert.Single(built.Browser.Opened).ToString());
        Assert.IsType<FeedScreen>(opened.Screen);
    }

    /// <summary>
    ///     And <c>o</c> on a trending link picked under the prompt opens the article without opening the link — offered
    ///     on the status row with <c>⏎</c>, which opens the link.
    /// </summary>
    [Fact]
    public async Task PressO_OnAPickedTrendingLink_OpensTheArticleAndAsksNothing()
    {
        var built = Sharing(posts: 3);
        var opened = await built.Opened();

        PickedTheLink(built, opened);

        Assert.Contains(opened.Screen.Keys, key => key is { Key: "o", Does: "article" });
        Assert.Contains(opened.Screen.Keys, key => key is { Key: "⏎", Does: "open" });

        var read = built.Timelines.Reads.Count;
        opened.Press(ShellKey.O);
        built.Host.Drain();

        Assert.Equal(Article, Assert.Single(built.Browser.Opened).ToString());
        Assert.IsType<SearchScreen>(opened.Screen);
        Assert.Equal(read, built.Timelines.Reads.Count);
    }

    /// <summary>
    ///     A link that has stopped trending by the time it is opened pushes nothing: the prompt stays with the link still
    ///     picked, and the status row says so and offers the article — which <c>o</c> then opens.
    /// </summary>
    [Fact]
    public async Task Enter_OnALinkNoLongerTrending_PushesNothingAndOffersTheArticle()
    {
        var built = Sharing(posts: 3);
        var opened = await built.Opened();

        PickedTheLink(built, opened);
        built.Timelines.NotTrending(new Uri(Article));
        opened.Press(ShellKey.Enter);
        built.Host.Drain();

        var prompt = Assert.IsType<SearchScreen>(opened.Screen);
        Assert.Equal("Search", opened.Breadcrumb);
        Assert.Equal("That link is no longer trending — o to open the article.", opened.Notice);
        Assert.Equal(Article, prompt.PickedLink?.Url);

        opened.Press(ShellKey.O);

        Assert.Equal(Article, Assert.Single(built.Browser.Opened).ToString());
    }

    /// <summary>
    ///     A link that stops trending while its posts are being read keeps the posts already shown, and reading on simply
    ///     ends: the foot says that's everything, with nothing said as a failure.
    /// </summary>
    [Fact]
    public async Task Viewed_OnALinkThatStoppedTrending_KeepsWhatWasReadAndEndsReadingOn()
    {
        var built = Sharing(posts: 100);
        var opened = await built.Opened();

        Opened(built, opened);
        built.Timelines.NotTrending(new Uri(Article));
        opened.Viewed(under: 0, screenful: Screenful);
        built.Host.Drain();

        var feed = Assert.IsType<FeedScreen>(opened.Screen);
        Assert.Equal(Ids(1, 40), feed.Posts.Select(post => post.Id));
        Assert.Equal("that's everything", Foot(feed));
        Assert.Null(opened.Notice);

        opened.Viewed(under: 0, screenful: Screenful);
        built.Host.Drain();

        Assert.Single(built.Timelines.ReadsOn);
    }

    /// <summary>A double click on a trending link opens it as <c>⏎</c> does: its posts, crumbed with its title.</summary>
    [Fact]
    public async Task DoubleClick_OnATrendingLink_PushesThePostsSharingIt()
    {
        using var drawn = await DrawnShell.Of(80, ContentClicks.Tall, Themes.Dark, Sharing(posts: 3));

        drawn.Press(new Key('/'));
        drawn.Settle();
        drawn.DoubleClick(ContentClicks.OverContent, ContentClicks.RowOf(drawn, "Cats learn to type"));
        drawn.Settle();

        Assert.IsType<FeedScreen>(drawn.Shell.Screen);
        Assert.Equal("Search › Cats learn to type", drawn.Shell.Breadcrumb);
    }

    /// <summary>
    ///     A long title is elided in the breadcrumb as anything long is there, rather than pushing the crumbs before it
    ///     off the row.
    /// </summary>
    [Fact]
    public async Task Breadcrumb_ElidesALongTitle()
    {
        var title = string.Join(' ', Enumerable.Repeat("Cats learn to type and then they write novels", 4));
        var built = new AShell
        {
            Trends = FakeTrends.Trending(hashtags: [], posts: [], links: [ATrendingLink.With(Article, title)]),
            Timelines = FakeTimelineReader.Holding(APost.With(id: "1")),
        };
        using var drawn = await DrawnShell.Of(80, ContentClicks.Tall, Themes.Dark, built);

        drawn.Press(new Key('/'));
        drawn.Settle();
        drawn.DoubleClick(ContentClicks.OverContent, ContentClicks.RowOf(drawn, "Cats learn to type"));
        drawn.Settle();

        var crumbs = drawn.Rows()[0];
        Assert.Contains("Cats learn to type and then", crumbs, StringComparison.Ordinal);
        Assert.Contains("…", crumbs, StringComparison.Ordinal);
        Assert.DoesNotContain(title, crumbs, StringComparison.Ordinal);
    }

    /// <summary>The trending link under the prompt, the one these tests open.</summary>
    private static TrendingLink Link() => ATrendingLink.With(url: Article, title: "Cats learn to type");

    /// <summary>
    ///     A shell whose one trend is <see cref="Link" />, shared by <paramref name="posts" /> posts, newest first, ids
    ///     counting up from 1 — a timeline that reads on.
    /// </summary>
    private static AShell Sharing(int posts) => new()
    {
        Trends = FakeTrends.Trending(hashtags: [], posts: [], links: [Link()]),
        Timelines = FakeTimelineReader.ReadingOn([.. Ids(1, posts).Select(id => APost.With(id: id))]),
    };

    /// <summary>
    ///     Goes to search, the trends read, and walks down from the field, past the heading over the links (#606), onto
    ///     the trending link.
    /// </summary>
    private static void PickedTheLink(AShell built, Shell shell)
    {
        shell.Search();
        built.Host.Drain();
        shell.Move(2);

        Assert.Equal(Article, Assert.IsType<SearchScreen>(shell.Screen).PickedLink?.Url);
    }

    /// <summary>Picks the trending link under the prompt and opens it with <c>⏎</c>.</summary>
    private static void Opened(AShell built, Shell shell)
    {
        PickedTheLink(built, shell);
        shell.Press(ShellKey.Enter);
        built.Host.Drain();

        Assert.IsType<FeedScreen>(shell.Screen);
    }

    /// <summary>Every row <paramref name="screen" /> draws, as text, the gutter and all.</summary>
    private static IReadOnlyList<string> Rows(Screen screen) =>
        [.. screen.Lines(new Drawing(61, AShell.Now)).Select(line => line.Text)];

    /// <summary>The last row <paramref name="screen" /> draws, which is the foot of its list where it has one.</summary>
    private static string Foot(Screen screen) => Rows(screen)[^1];

    /// <summary>The ids from <paramref name="first" /> to <paramref name="last" />, in order.</summary>
    private static IEnumerable<string> Ids(int first, int last) =>
        Enumerable.Range(first, last - first + 1).Select(at => $"{at}");
}
