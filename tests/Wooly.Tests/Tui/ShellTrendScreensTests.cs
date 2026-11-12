using Terminal.Gui.Input;
using Wooly.Core.Posts;
using Wooly.Core.Trends;
using Wooly.Tests.Fakes;
using Wooly.Tui.Rendering;
using Wooly.Tui.Screens;
using Wooly.Tui.Shell;
using Wooly.Tui.Theme;

namespace Wooly.Tests.Tui;

/// <summary>
///     A trends heading under the search prompt opens onto more of its kind (#606), as a heading on a search's results
///     does (#604): <c>⏎</c> or a click on <c>── Trending posts ──</c> pushes <c>Search › Trending posts</c>, read on
///     by offset at the most the instance serves of the kind as the reader scrolls — through <see cref="FakeTrends" />,
///     and never HTTP.
/// </summary>
public class ShellTrendScreensTests
{
    /// <summary>The height of the content region these tests say the page is, in rows.</summary>
    private const int Screenful = 20;

    /// <summary>
    ///     <c>⏎</c> on the heading over the trending posts pushes the trending posts alone, crumbed under the prompt,
    ///     asked forty at a time — the most an instance serves of them in one page.
    /// </summary>
    [Fact]
    public async Task Enter_OnTheTrendingPostsHeading_PushesTheTrendingPostsAlone()
    {
        var built = Trending(hashtags: 2, links: 2, posts: 100);
        var opened = await built.Opened();

        Prompted(built, opened);
        built.Trends.Reads.Clear();

        OnTheHeading(opened, "Trending posts");
        opened.Press(ShellKey.Enter);
        built.Host.Drain();

        Assert.Equal("Search › Trending posts", opened.Breadcrumb);
        Assert.Equal(
            [(TrendKind.Posts, 40, 0)],
            built.Trends.Reads.Select(read => (read.Kind, read.Page.Limit, read.Page.Offset)));
        Assert.Equal(Ids("p", 1, 40), Assert.IsType<SectionScreen>(opened.Screen).Posts.Select(post => post.Id));
    }

    /// <summary>
    ///     The hashtags and the links are pushed alone the same way, each crumbed with what its heading calls it and asked
    ///     twenty at a time — the most an instance serves of either.
    /// </summary>
    [Theory]
    [InlineData("Trending hashtags", TrendKind.Hashtags)]
    [InlineData("Trending links", TrendKind.Links)]
    public async Task Enter_OnAHeading_PushesThatKindOfTrendAlone(string called, TrendKind kind)
    {
        var built = Trending(hashtags: 30, links: 30, posts: 3);
        var opened = await built.Opened();

        Prompted(built, opened);
        built.Trends.Reads.Clear();

        OnTheHeading(opened, called);
        opened.Press(ShellKey.Enter);
        built.Host.Drain();

        var section = Assert.IsType<SectionScreen>(opened.Screen);
        Assert.Equal($"Search › {called}", opened.Breadcrumb);
        Assert.Equal([(kind, 20, 0)], built.Trends.Reads.Select(read => (read.Kind, read.Page.Limit, read.Page.Offset)));
        Assert.Equal(20, section.Items.Count);
    }

    /// <summary>
    ///     The page near the end reads on by offset, at the most the instance serves at a time, under what is held —
    ///     and a page that comes back short is the end: the foot says so, and nothing more is asked however far the
    ///     reader goes.
    /// </summary>
    [Fact]
    public async Task Viewed_NearTheEnd_ReadsOnByOffsetToTheEnd()
    {
        var built = Trending(hashtags: 0, links: 0, posts: 100);
        var opened = await built.Opened();

        Opened(built, opened, "Trending posts");

        for (var near = 0; near < 4; near++)
        {
            opened.Viewed(under: 0, screenful: Screenful);
            built.Host.Drain();
        }

        var section = Assert.IsType<SectionScreen>(opened.Screen);
        Assert.Equal(
            [(40, 0), (40, 40), (40, 80)],
            built.Trends.Reads.Select(read => (read.Page.Limit ?? 0, read.Page.Offset)));
        Assert.All(built.Trends.Reads, read => Assert.Equal(TrendKind.Posts, read.Kind));
        Assert.Equal(Ids("p", 1, 100), section.Posts.Select(post => post.Id));
        Assert.Equal("that's everything", Foot(section));
    }

    /// <summary>Trending hashtags read on twenty at a time, to the end of the ranking.</summary>
    [Fact]
    public async Task ReadOn_Hashtags_TwentyAtATime()
    {
        var built = Trending(hashtags: 45, links: 0, posts: 0);
        var opened = await built.Opened();

        Opened(built, opened, "Trending hashtags");

        for (var near = 0; near < 3; near++)
        {
            opened.Viewed(under: 0, screenful: Screenful);
            built.Host.Drain();
        }

        Assert.Equal(
            [(20, 0), (20, 20), (20, 40)],
            built.Trends.Reads.Select(read => (read.Page.Limit ?? 0, read.Page.Offset)));
        Assert.Equal(Ids("h", 1, 45), Assert.IsType<SectionScreen>(opened.Screen).Hashtags.Select(tag => tag.Name));
    }

    /// <summary>
    ///     On the trending posts' own screen there is no field to take letters, so the post keys act on a trending post as
    ///     on any feed.
    /// </summary>
    [Fact]
    public async Task PostKeys_ActOnATrendingPostOnItsScreen()
    {
        var built = Trending(hashtags: 1, links: 1, posts: 3);
        var opened = await built.Opened();

        Opened(built, opened, "Trending posts");
        opened.Press(ShellKey.F);
        built.Host.Drain();

        var marked = Assert.Single(built.Engagement.Marks);
        Assert.Equal(("p1", PostMark.Favorite), (marked.PostId, marked.Mark));
        Assert.Contains(opened.Keys, key => key.Does == "favorite");
    }

    /// <summary>A trending hashtag on its kind's screen opens its tag, as it does under the prompt.</summary>
    [Fact]
    public async Task Enter_OnATrendingHashtagOnItsScreen_OpensItsTag()
    {
        var built = Trending(hashtags: 3, links: 0, posts: 0);
        var opened = await built.Opened();

        Opened(built, opened, "Trending hashtags");
        opened.Walk(1, reclaiming: null);
        opened.Press(ShellKey.Enter);
        built.Host.Drain();

        Assert.IsType<FeedScreen>(opened.Screen);
        Assert.Equal("Search › Trending hashtags › #h2", opened.Breadcrumb);
    }

    /// <summary>A trending post on its kind's screen opens its thread, as it does under the prompt.</summary>
    [Fact]
    public async Task Enter_OnATrendingPostOnItsScreen_OpensItsThread()
    {
        var built = Trending(hashtags: 0, links: 0, posts: 3);
        var opened = await built.Opened();

        Opened(built, opened, "Trending posts");
        opened.Walk(1, reclaiming: null);
        opened.Press(ShellKey.Enter);
        built.Host.Drain();

        Assert.Equal("p2", Assert.IsType<PostScreen>(opened.Screen).Picked?.Id);
    }

    /// <summary>
    ///     The trending links' screen draws each link as under the prompt — its title, its description, then its site and
    ///     how many people are sharing it, a blank between one and the next — and offers what a link offers there: to
    ///     open it, and its article.
    /// </summary>
    [Fact]
    public async Task Links_AreDrawnAsUnderThePrompt()
    {
        var built = Trending(hashtags: 0, links: 2, posts: 0);
        var opened = await built.Opened();

        Opened(built, opened, "Trending links");

        Assert.Equal(
            ["l1", "A study of keyboards and the cats who sit on them.", "Example News · 65 people sharing", "", "l2"],
            AShell.Drawn(opened.Screen).Take(5).Select(row => row.TrimEnd()));
        Assert.Contains(opened.Keys, key => key is { Key: "⏎", Does: "open" });
        Assert.Contains(opened.Keys, key => key is { Key: "o", Does: "article" });
    }

    /// <summary>
    ///     A trending link on its kind's screen opens as it does under the prompt (#607): the posts sharing it, crumbed
    ///     with its title.
    /// </summary>
    [Fact]
    public async Task Enter_OnATrendingLinkOnItsScreen_OpensThePostsSharingIt()
    {
        var built = Trending(hashtags: 0, links: 3, posts: 0);
        built.Timelines = FakeTimelineReader.ReadingOn([APost.With(id: "s1")]);
        var opened = await built.Opened();

        Opened(built, opened, "Trending links");
        opened.Walk(1, reclaiming: null);
        opened.Press(ShellKey.Enter);
        built.Host.Drain();

        Assert.Equal("Search › Trending links › l2", opened.Breadcrumb);
        Assert.Equal(["s1"], Assert.IsType<FeedScreen>(opened.Screen).Posts.Select(post => post.Id));
    }

    /// <summary><c>o</c> on a trending link on its kind's screen opens the article itself, as under the prompt.</summary>
    [Fact]
    public async Task PressO_OnATrendingLinkOnItsScreen_OpensTheArticle()
    {
        var built = Trending(hashtags: 0, links: 2, posts: 0);
        var opened = await built.Opened();

        Opened(built, opened, "Trending links");
        var asked = built.Requests;
        opened.Press(ShellKey.O);
        built.Host.Drain();

        Assert.Equal("https://news.example/l1", Assert.Single(built.Browser.Opened).ToString());
        Assert.IsType<SectionScreen>(opened.Screen);
        Assert.Equal(asked, built.Requests);
    }

    /// <summary>
    ///     A heading is walked to like a trend, and the status row offers to open it — and the arrows pass over the first
    ///     one: <c>↑</c> from it, as from the trend under it, goes back to the field.
    /// </summary>
    [Fact]
    public async Task Walk_OntoAHeading_OffersToOpenIt()
    {
        var built = Trending(hashtags: 2, links: 0, posts: 0);
        var opened = await built.Opened();

        Prompted(built, opened);
        opened.Walk(1, reclaiming: null);

        var prompt = Assert.IsType<SearchScreen>(opened.Screen);
        Assert.Equal(TrendKind.Hashtags, prompt.PickedSection);
        Assert.Contains(opened.Keys, key => key is { Key: "⏎", Does: "open" });

        opened.Press(ShellKey.Up);

        Assert.True(prompt.OnTheField);
    }

    /// <summary>
    ///     <c>esc</c> walks back to the prompt as it was left — the heading still picked, the trends it held — and asks the
    ///     instance nothing.
    /// </summary>
    [Fact]
    public async Task Escape_FromATrendScreen_ComesBackToThePromptAsItWasLeft()
    {
        var built = Trending(hashtags: 2, links: 2, posts: 30);
        var opened = await built.Opened();

        Prompted(built, opened);
        var prompt = Assert.IsType<SearchScreen>(opened.Screen);
        OnTheHeading(opened, "Trending posts");
        var picked = prompt.At;
        opened.Press(ShellKey.Enter);
        built.Host.Drain();
        var asked = built.Requests;

        opened.Back();
        built.Host.Drain();

        Assert.Same(prompt, opened.Screen);
        Assert.Equal(picked, prompt.At);
        Assert.Equal(TrendKind.Posts, prompt.PickedSection);
        Assert.Equal(Ids("h", 1, 2), prompt.Hashtags.Select(tag => tag.Name));
        Assert.Equal("Search", opened.Breadcrumb);
        Assert.Equal(asked, built.Requests);
    }

    /// <summary>
    ///     <c>esc</c> back from a trend screen finds the field holding what was typed into it before the heading was
    ///     opened.
    /// </summary>
    [Fact]
    public async Task Escape_FromATrendScreen_KeepsTheQueryTyped()
    {
        using var drawn = await Prompt(Trending(hashtags: 2, links: 1, posts: 3));

        drawn.Type("cat");
        drawn.Click(ContentClicks.OverContent, ContentClicks.RowOf(drawn, "── Trending posts ──"));
        drawn.Settle();

        Assert.Equal("Search › Trending posts", drawn.Shell.Breadcrumb);

        drawn.Press(Key.Esc);
        drawn.Settle();

        Assert.IsType<SearchScreen>(drawn.Shell.Screen);
        Assert.Equal("cat", drawn.SearchField.Text);
    }

    /// <summary>
    ///     A click on a heading opens it, as <c>⏎</c> on it would: that kind of trend alone, pushed over the prompt.
    /// </summary>
    [Fact]
    public async Task Click_OnAHeading_PushesThatKindAlone()
    {
        using var drawn = await Prompt(Trending(hashtags: 2, links: 1, posts: 3));

        drawn.Click(ContentClicks.OverContent, ContentClicks.RowOf(drawn, "── Trending hashtags ──"));
        drawn.Settle();

        Assert.Equal("Search › Trending hashtags", drawn.Shell.Breadcrumb);
        Assert.Equal(
            ["h1", "h2"],
            Assert.IsType<SectionScreen>(drawn.Shell.Screen).Hashtags.Select(hashtag => hashtag.Name));
    }

    /// <summary>
    ///     A double click on a heading pushes it once: its second click asks again from the prompt, and that answer is
    ///     dropped, the prompt no longer being in front.
    /// </summary>
    [Fact]
    public async Task DoubleClick_OnAHeading_PushesThatKindOnce()
    {
        using var drawn = await Prompt(Trending(hashtags: 2, links: 1, posts: 3));

        drawn.DoubleClick(ContentClicks.OverContent, ContentClicks.RowOf(drawn, "── Trending links ──"));
        drawn.Settle();

        Assert.Equal("Search › Trending links", drawn.Shell.Breadcrumb);
    }

    /// <summary>Opens the heading saying <paramref name="called" /> under a fresh prompt, by the keys.</summary>
    private static void Opened(AShell built, Shell shell, string called)
    {
        Prompted(built, shell);
        OnTheHeading(shell, called);
        built.Trends.Reads.Clear();
        shell.Press(ShellKey.Enter);
        built.Host.Drain();
    }

    /// <summary>The whole shell drawn headless, gone to search with <c>/</c> and the trends read.</summary>
    private static async Task<DrawnShell> Prompt(AShell built)
    {
        var drawn = await DrawnShell.Of(80, ContentClicks.Tall, Themes.Dark, built);

        drawn.Press(new Key('/'));
        drawn.Settle();

        return drawn;
    }

    /// <summary>Goes to search the way a reader does, by <c>/</c>, and lets the trends be read.</summary>
    private static void Prompted(AShell built, Shell shell)
    {
        shell.Search();
        built.Host.Drain();
    }

    /// <summary>
    ///     Walks the pick on the prompt down from the field to the heading saying <paramref name="called" />, by the keys
    ///     alone.
    /// </summary>
    private static void OnTheHeading(Shell shell, string called)
    {
        var prompt = Assert.IsType<SearchScreen>(shell.Screen);

        shell.Walk(-prompt.At, reclaiming: null);

        while (PickedRow(prompt) is not { } row || !row.Contains($"── {called} ──", StringComparison.Ordinal))
        {
            var at = prompt.At;
            shell.Walk(1, reclaiming: null);
            Assert.NotEqual(at, prompt.At);
        }
    }

    /// <summary>The first row of whatever is picked on <paramref name="screen" />, as text.</summary>
    private static string? PickedRow(Screen screen) =>
        screen.Lines(new Drawing(80, AShell.Now)).FirstOrDefault(line => line.Picked)?.Text;

    /// <summary>The last row <paramref name="screen" /> draws, which is the foot of its list where it has one.</summary>
    private static string Foot(Screen screen) => screen.Lines(new Drawing(80, AShell.Now))[^1].Text;

    /// <summary>
    ///     An instance trending <paramref name="hashtags" /> hashtags, <paramref name="links" /> links and
    ///     <paramref name="posts" /> posts, named counting up from 1 behind a letter for each kind.
    /// </summary>
    private static AShell Trending(int hashtags, int links, int posts) => new()
    {
        Trends = FakeTrends.Trending(
            hashtags: [.. Ids("h", 1, hashtags).Select(name => AHashtag.With(name))],
            posts: [.. Ids("p", 1, posts).Select(id => APost.With(id: id))],
            links: [.. Ids("l", 1, links).Select(id => ATrendingLink.With(url: $"https://news.example/{id}", title: id))]),
    };

    /// <summary>The names from <paramref name="first" /> to <paramref name="last" /> behind <paramref name="kind" />.</summary>
    private static IEnumerable<string> Ids(string kind, int first, int last) =>
        Enumerable.Range(first, Math.Max(0, last - first + 1)).Select(at => $"{kind}{at}");
}
