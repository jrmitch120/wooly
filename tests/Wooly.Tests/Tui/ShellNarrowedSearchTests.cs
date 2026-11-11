using Wooly.Core.Accounts;
using Wooly.Core.Errors;
using Wooly.Core.Posts;
using Wooly.Core.Search;
using Wooly.Tests.Fakes;
using Wooly.Tui.Rendering;
using Wooly.Tui.Screens;
using Wooly.Tui.Shell;

namespace Wooly.Tests.Tui;

/// <summary>
///     A search narrowed to one kind (#604): each section heading on the results says how many are under it — <c>N+</c>
///     where the instance served a full page — and <c>⏎</c> on one pushes that kind alone, crumbed
///     <c>Search › cats › Posts</c>, asked forty at a time and read on by offset as the reader scrolls.
/// </summary>
public class ShellNarrowedSearchTests
{
    /// <summary>The height of the content region these tests say the page is, in rows.</summary>
    private const int Screenful = 20;

    /// <summary>
    ///     The mixed results ask twenty of each kind, and a heading says how many came back — or <c>20+</c> where the
    ///     page came back full, which is the instance saying there may be more than it served.
    /// </summary>
    [Fact]
    public async Task Find_HeadsEachKindWithItsCount_OrNPlusWhereThePageCameBackFull()
    {
        var built = Finding(accounts: 3, hashtags: 0, posts: 25);
        var opened = await built.Opened();

        Found(built, opened, "cats");

        Assert.Equal(20, Assert.Single(built.Search.Searches).Query.Page.Limit);
        Assert.Equal(["── 3 accounts ──", "── 20+ posts ──"], Headings(opened.Screen));
    }

    /// <summary>
    ///     A heading is walked to like any result, and <c>⏎</c> on it pushes that kind alone, crumbed under the
    ///     search, asked of the instance with its kind and forty at a time.
    /// </summary>
    [Fact]
    public async Task Enter_OnAHeading_PushesThatKindAlone()
    {
        var built = Finding(accounts: 1, hashtags: 1, posts: 100);
        var opened = await built.Opened();

        Found(built, opened, "cats");
        built.Search.Searches.Clear();

        OnTheHeadingOf(opened, "posts");
        opened.Press(ShellKey.Enter);
        built.Host.Drain();

        var asked = Assert.Single(built.Search.Searches).Query;
        Assert.Equal("Search › cats › Posts", opened.Breadcrumb);
        Assert.Equal(("cats", SearchKind.Posts, 40), (asked.Text, asked.Kind, asked.Page.Limit));
        Assert.Equal(Ids("p", 1, 40), Assert.IsType<SectionScreen>(opened.Screen).Posts.Select(post => post.Id));
    }

    /// <summary>
    ///     The page near the end reads on by offset, forty at a time, under what is held — and a page that comes back
    ///     short is the end: the foot says so, and nothing more is asked however far the reader goes.
    /// </summary>
    [Fact]
    public async Task Viewed_NearTheEnd_ReadsOnByOffsetToTheEnd()
    {
        var built = Finding(accounts: 0, hashtags: 0, posts: 100);
        var opened = await built.Opened();

        Narrowed(built, opened, "cats", "posts");

        for (var near = 0; near < 4; near++)
        {
            opened.Viewed(under: 0, screenful: Screenful);
            built.Host.Drain();
        }

        // The mixed results' own search first, and then the narrowed one's pages.
        var narrowed = built.Search.Searches.Skip(1).ToList();
        var section = Assert.IsType<SectionScreen>(opened.Screen);
        Assert.Equal(
            [(40, null), (40, 40), (40, 80)],
            narrowed.Select(asked => (asked.Query.Page.Limit, asked.Query.Page.Offset)));
        Assert.All(narrowed, asked => Assert.Equal(SearchKind.Posts, asked.Query.Kind));
        Assert.Equal(Ids("p", 1, 100), section.Posts.Select(post => post.Id));
        Assert.Equal("that's everything", Foot(section));
    }

    /// <summary>
    ///     Accounts read on carry where the reader stands with them on every page, asked once for the page and only of
    ///     the people that page brought.
    /// </summary>
    [Fact]
    public async Task ReadOn_Accounts_CarryTheirStandingOnEveryPage()
    {
        var built = Finding(accounts: 50, hashtags: 0, posts: 0);
        built.Accounts.Stands = AnAccount.Standing(followedBy: true);
        var opened = await built.Opened();

        Narrowed(built, opened, "cats", "accounts");
        built.Accounts.ForgetStandings();

        opened.Viewed(under: 0, screenful: Screenful);
        built.Host.Drain();

        var section = Assert.IsType<SectionScreen>(opened.Screen);
        Assert.Equal("Search › cats › Accounts", opened.Breadcrumb);
        Assert.Equal(Ids("a", 41, 50), Assert.Single(built.Accounts.Standings).AccountIds);
        Assert.Equal(50, section.Accounts.Count);
        Assert.All(section.Accounts, account => Assert.True(account.Standing?.FollowedBy));
    }

    /// <summary>
    ///     A rate limit while reading on keeps what was already read and says so, counting down as reading on a feed
    ///     does — and once waited out, the same page is asked again and put under the rest.
    /// </summary>
    [Fact]
    public async Task ReadOn_StoppedByARateLimit_KeepsWhatWasReadAndSaysSo()
    {
        var built = Finding(accounts: 0, hashtags: 0, posts: 60);
        var opened = await built.Opened();

        Narrowed(built, opened, "cats", "posts");
        built.Search.RateLimitsNext(new RateLimitedException("mastodon.social", AShell.Now + TimeSpan.FromSeconds(3)));

        opened.Viewed(under: 0, screenful: Screenful);
        built.Host.Drain();

        var section = Assert.IsType<SectionScreen>(opened.Screen);
        Assert.Contains("Rate limited by mastodon.social", opened.Notice);
        Assert.Equal(Ids("p", 1, 40), section.Posts.Select(post => post.Id));
        Assert.Equal(ReadingOn.InFlightSays, Foot(section));

        built.Clock.Advance(TimeSpan.FromSeconds(3));
        built.Host.SettleAll();

        // The mixed results' search and the narrowed first page, then the refused page and the same page again.
        Assert.Equal([40, 40], built.Search.Searches.Skip(2).Select(asked => asked.Query.Page.Offset ?? 0));
        Assert.Equal(Ids("p", 1, 60), section.Posts.Select(post => post.Id));
        Assert.Null(opened.Notice);
    }

    /// <summary>A post on the narrowed screen is a post on a feed: the post keys act on it.</summary>
    [Fact]
    public async Task PostKeys_ActOnAPostOnTheNarrowedScreen()
    {
        var built = Finding(accounts: 0, hashtags: 0, posts: 3);
        var opened = await built.Opened();

        Narrowed(built, opened, "cats", "posts");
        opened.Press(ShellKey.F);
        built.Host.Drain();

        var marked = Assert.Single(built.Engagement.Marks);
        Assert.Equal(("p1", PostMark.Favorite), (marked.PostId, marked.Mark));
        Assert.Contains(opened.Keys, key => key.Does == "favorite");
    }

    /// <summary>
    ///     A hashtag narrowed to opens as it does on the mixed results — its timeline pushed over the narrowed screen.
    /// </summary>
    [Fact]
    public async Task Enter_OnANarrowedHashtag_OpensItsTimeline()
    {
        var built = Finding(accounts: 0, hashtags: 3, posts: 0);
        var opened = await built.Opened();

        Narrowed(built, opened, "cats", "hashtags");
        opened.Walk(1, reclaiming: null);
        opened.Press(ShellKey.Enter);
        built.Host.Drain();

        Assert.Equal("Search › cats › Hashtags › #h2", opened.Breadcrumb);
    }

    /// <summary>
    ///     <c>esc</c> walks back to the mixed results as they were left — the heading still picked, the page where it
    ///     was — and asks the instance nothing.
    /// </summary>
    [Fact]
    public async Task Escape_FromTheNarrowedScreen_ComesBackToTheResultsAsTheyWereLeft()
    {
        var built = Finding(accounts: 2, hashtags: 2, posts: 30);
        var opened = await built.Opened();

        Found(built, opened, "cats");
        var results = Assert.IsType<SearchResultsScreen>(opened.Screen);
        OnTheHeadingOf(opened, "posts");
        var picked = results.At;
        opened.Press(ShellKey.Enter);
        built.Host.Drain();
        var asked = built.Requests;

        opened.Back();
        built.Host.Drain();

        Assert.Same(results, opened.Screen);
        Assert.Equal(picked, results.At);
        Assert.Equal(SearchKind.Posts, results.PickedSection);
        Assert.Equal("Search › cats", opened.Breadcrumb);
        Assert.Equal(asked, built.Requests);
    }

    /// <summary>
    ///     A heading is walked to between the runs like any result: up from the first post is the heading over the
    ///     posts, and the status row offers to open it.
    /// </summary>
    [Fact]
    public async Task Walk_UpFromTheFirstOfARun_PicksItsHeading()
    {
        var built = Finding(accounts: 1, hashtags: 0, posts: 2);
        var opened = await built.Opened();

        Found(built, opened, "cats");
        var results = Assert.IsType<SearchResultsScreen>(opened.Screen);
        Assert.NotNull(results.PickedAccount);

        opened.Walk(2, reclaiming: null);
        Assert.Equal("p1", results.Picked?.Id);

        opened.Walk(-1, reclaiming: null);
        Assert.Equal(SearchKind.Posts, results.PickedSection);
        Assert.Equal("── 2 posts ──", PickedRow(results)?.Trim().TrimStart('▌').Trim());
        Assert.Contains(opened.Keys, key => key is { Key: "⏎", Does: "open" });
    }

    /// <summary>
    ///     A click on a heading opens it, as <c>⏎</c> on it would: the kind alone pushed over the results.
    /// </summary>
    [Fact]
    public async Task Click_OnAHeading_PushesThatKindAlone()
    {
        using var drawn = await ContentClicks.Searched();

        drawn.Click(ContentClicks.OverContent, ContentClicks.RowOf(drawn, "── 2 hashtags ──"));
        drawn.Settle();

        Assert.Equal("Search › sheep › Hashtags", drawn.Shell.Breadcrumb);
        Assert.Equal(
            ["sheep", "wool"],
            Assert.IsType<SectionScreen>(drawn.Shell.Screen).Hashtags.Select(hashtag => hashtag.Name));
    }

    /// <summary>
    ///     A double click on a heading pushes it once: its second click asks again from the results, and that answer is
    ///     dropped when the first lands, the results no longer being in front.
    /// </summary>
    [Fact]
    public async Task DoubleClick_OnAHeading_PushesThatKindOnce()
    {
        using var drawn = await ContentClicks.Searched();

        drawn.DoubleClick(ContentClicks.OverContent, ContentClicks.RowOf(drawn, "── 2 hashtags ──"));
        drawn.Settle();

        Assert.Equal("Search › sheep › Hashtags", drawn.Shell.Breadcrumb);
    }

    /// <summary>Narrows a search for <paramref name="text" /> to <paramref name="kind" />, by the keys.</summary>
    private static void Narrowed(AShell built, Shell shell, string text, string kind)
    {
        Found(built, shell, text);
        OnTheHeadingOf(shell, kind);
        shell.Press(ShellKey.Enter);
        built.Host.Drain();
    }

    /// <summary>The last row <paramref name="screen" /> draws, which is the foot of its list where it has one.</summary>
    private static string Foot(Screen screen) => screen.Lines(new Drawing(80, AShell.Now))[^1].Text;

    /// <summary>
    ///     Searches <paramref name="text" /> the way a reader does: <c>/</c>, typed, and <c>⏎</c>.
    /// </summary>
    private static void Found(AShell built, Shell shell, string text)
    {
        shell.Search();
        built.Host.Drain();

        foreach (var letter in text)
        {
            shell.Type(letter);
        }

        shell.Press(ShellKey.Enter);
        built.Host.Drain();
    }

    /// <summary>
    ///     Walks the pick on the results up or down to the heading over <paramref name="kind" />, by the keys alone.
    /// </summary>
    private static void OnTheHeadingOf(Shell shell, string kind)
    {
        var results = Assert.IsType<SearchResultsScreen>(shell.Screen);
        var heading = Headings(results).ToList().FindIndex(text => text.EndsWith($" {kind} ──", StringComparison.Ordinal));
        Assert.True(heading >= 0, $"No heading over {kind}.");

        // Walk to the top, then down until the picked row is the heading wanted.
        shell.Walk(-results.At, reclaiming: null);

        while (PickedRow(results) is { } row && !row.Contains($" {kind} ──", StringComparison.Ordinal))
        {
            var at = results.At;
            shell.Walk(1, reclaiming: null);
            Assert.NotEqual(at, results.At);
        }
    }

    /// <summary>The first row of whatever is picked on <paramref name="screen" />, as text.</summary>
    private static string? PickedRow(Screen screen) =>
        screen.Lines(new Drawing(80, AShell.Now)).FirstOrDefault(line => line.Picked)?.Text;

    /// <summary>The section headings <paramref name="screen" /> draws, as text, without the gutter.</summary>
    private static IReadOnlyList<string> Headings(Screen screen) =>
        [.. screen.Lines(new Drawing(80, AShell.Now)).Where(line => line.Heads).Select(line => line.Text.Trim())];

    /// <summary>
    ///     A search finding <paramref name="accounts" /> accounts, <paramref name="hashtags" /> hashtags and
    ///     <paramref name="posts" /> posts, ids counting up from 1 behind a letter for each kind.
    /// </summary>
    private static AShell Finding(int accounts, int hashtags, int posts) => new()
    {
        Search = FakeInstanceSearch.Finding(
            accounts: [.. Ids("a", 1, accounts).Select(id => AnAccount.With(id: id, address: $"{id}@hachyderm.io"))],
            hashtags: [.. Ids("h", 1, hashtags).Select(name => AHashtag.With(name: name))],
            posts: [.. Ids("p", 1, posts).Select(id => APost.With(id: id))]),
    };

    /// <summary>The ids from <paramref name="first" /> to <paramref name="last" /> behind <paramref name="kind" />.</summary>
    private static IEnumerable<string> Ids(string kind, int first, int last) =>
        Enumerable.Range(first, last - first + 1).Select(at => $"{kind}{at}");
}
