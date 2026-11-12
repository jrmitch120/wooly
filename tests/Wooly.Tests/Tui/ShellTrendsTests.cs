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
///     The instance's trends under the empty search prompt (#605): what arriving at Search asks for, how the trends are
///     drawn under the field, and what the keys and the mouse do with the field and the trends as one walk — through
///     <see cref="FakeTrends" />, the port's fake, and never HTTP.
/// </summary>
public class ShellTrendsTests
{
    /// <summary>
    ///     Arriving at Search asks for ten hashtags, ten links and three posts: every kind, without the posts pushing
    ///     the rest off the page (#602).
    /// </summary>
    [Fact]
    public async Task Search_AsksForTenHashtagsTenLinksAndThreePosts()
    {
        var shell = new AShell { Trends = FakeTrends.Trending() };
        var opened = await shell.Opened();

        opened.Search();
        shell.Host.Drain();

        Assert.Equal(
            [(TrendKind.Hashtags, 10, 0), (TrendKind.Links, 10, 0), (TrendKind.Posts, 3, 0)],
            shell.Trends.Reads.Select(read => (read.Kind, read.Page.Limit, read.Page.Offset)));
    }

    /// <summary>
    ///     The trends stand under the field in three headed sections: hashtags, then links, then posts.
    /// </summary>
    [Fact]
    public async Task Search_DrawsTheTrendsUnderTheFieldInThreeHeadedSections()
    {
        var shell = new AShell
        {
            Trends = FakeTrends.Trending(
                hashtags: [AHashtag.With("caturday")],
                posts: [APost.With(content: "Biscuit knocked the pen off the desk")],
                links: [ATrendingLink.With(title: "Cats learn to type")]),
        };
        var opened = await shell.Opened();

        opened.Search();
        shell.Host.Drain();

        var drawn = AShell.Drawn(opened.Screen);
        int Row(string text) => drawn.ToList().FindIndex(row => row.Contains(text, StringComparison.Ordinal));

        Assert.True(Row("╰") < Row("── Trending hashtags ──"), string.Join('\n', drawn));
        Assert.True(Row("── Trending hashtags ──") < Row("#caturday"), string.Join('\n', drawn));
        Assert.True(Row("#caturday") < Row("── Trending links ──"), string.Join('\n', drawn));
        Assert.True(Row("── Trending links ──") < Row("Cats learn to type"), string.Join('\n', drawn));
        Assert.True(Row("Cats learn to type") < Row("── Trending posts ──"), string.Join('\n', drawn));
        Assert.True(Row("── Trending posts ──") < Row("Biscuit knocked the pen"), string.Join('\n', drawn));
    }

    /// <summary>
    ///     <c>esc</c> back to the prompt from what a search found shows the trends that were already there, and asks the
    ///     instance for nothing: going back costs nothing.
    /// </summary>
    [Fact]
    public async Task Escape_FromTheResults_ShowsTheTrendsAlreadyThereAndAsksNothing()
    {
        var shell = new AShell { Trends = FakeTrends.Trending(hashtags: [AHashtag.With("caturday")]) };
        var opened = await shell.Opened();

        opened.Search();
        shell.Host.Drain();
        opened.Find("cats");
        shell.Host.Drain();

        var read = shell.Trends.Reads.Count;

        opened.Press(ShellKey.Escape);
        shell.Host.Drain();

        Assert.IsType<SearchScreen>(opened.Screen);
        Assert.Equal(read, shell.Trends.Reads.Count);
        Assert.Contains(AShell.Drawn(opened.Screen), row => row.Contains("#caturday", StringComparison.Ordinal));
    }

    /// <summary>
    ///     The trends are read afresh on every arrival at an empty prompt — by <c>/</c> from the results as much as by
    ///     the rail — so that what is shown is current.
    /// </summary>
    [Fact]
    public async Task Search_FromTheResults_ReadsTheTrendsAfresh()
    {
        var shell = new AShell { Trends = FakeTrends.Trending() };
        var opened = await shell.Opened();

        opened.Search();
        shell.Host.Drain();
        opened.Find("cats");
        shell.Host.Drain();
        opened.Search();
        shell.Host.Drain();

        Assert.IsType<SearchScreen>(opened.Screen);
        Assert.Equal(6, shell.Trends.Reads.Count);
    }

    /// <summary>
    ///     The field is a rounded box titled with the instance acted as — whose answer a search gets — with a blank row
    ///     above it so it stands clear of the panel's edge.
    /// </summary>
    [Fact]
    public async Task Field_IsABoxTitledWithTheInstanceUnderABlankRow()
    {
        var shell = new AShell { Trends = FakeTrends.Trending() };
        var opened = await shell.Opened();

        opened.Search();
        shell.Host.Drain();

        var drawn = AShell.Drawn(opened.Screen);

        Assert.Equal("", drawn[0]);
        Assert.StartsWith("╭─ mastodon.social ──", drawn[1], StringComparison.Ordinal);
        Assert.EndsWith("╮", drawn[1], StringComparison.Ordinal);
        Assert.StartsWith("│ ⌕ ", drawn[2], StringComparison.Ordinal);
        Assert.EndsWith("│", drawn[2], StringComparison.Ordinal);
        Assert.StartsWith("╰──", drawn[3], StringComparison.Ordinal);
        Assert.Equal(drawn[1].Length, drawn[3].Length);
    }

    /// <summary>
    ///     The field marks its own focus — no selection bar in the gutter, and no band behind it — whether it is picked
    ///     or not; its border is lit while it takes letters and dimmed while a trend is picked.
    /// </summary>
    [Fact]
    public async Task Field_HasNoSelectionBar_AndIsLitOnlyWhilePicked()
    {
        var shell = new AShell { Trends = FakeTrends.Trending() };
        var opened = await shell.Opened();

        opened.Search();
        shell.Host.Drain();

        var box = opened.Screen.Lines(new Drawing(61, AShell.Now)).Skip(1).Take(3).ToList();

        Assert.All(box, row => Assert.StartsWith(" ", row.Text, StringComparison.Ordinal));
        Assert.All(box, row => Assert.False(row.Picked));
        Assert.All(box, row => Assert.True(row.Has(Role.PanelBorderActive)));

        opened.Walk(1, reclaiming: null);

        var dimmed = opened.Screen.Lines(new Drawing(61, AShell.Now)).Skip(1).Take(3).ToList();

        Assert.All(dimmed, row => Assert.False(row.Has(Role.PanelBorderActive)));
        Assert.All(dimmed, row => Assert.True(row.Has(Role.PanelBorder)));
    }

    /// <summary>An empty field says what it takes, dimly.</summary>
    [Fact]
    public async Task Field_ShowsThePlaceholderWhileEmpty()
    {
        using var drawn = await Prompt(new AShell { Trends = FakeTrends.Trending() });

        Assert.Contains(
            drawn.Rows(),
            row => row.Contains("A word, a #hashtag, an @account, or a link", StringComparison.Ordinal));

        drawn.Type("cats");

        Assert.DoesNotContain(drawn.Rows(), row => row.Contains("A word, a #hashtag", StringComparison.Ordinal));
    }

    /// <summary>
    ///     The field is laid inside the box's middle row, and takes letters with a caret while it is picked — and is
    ///     dimmed with no caret, taking none, while a trend is.
    /// </summary>
    [Fact]
    public async Task Field_TakesLettersWithACaretOnlyWhilePicked()
    {
        using var drawn = await Prompt(new AShell { Trends = FakeTrends.Trending() });

        drawn.Type("cat");

        var row = ContentClicks.RowOf(drawn, "│ ⌕ cat");
        Assert.True(drawn.SearchField.HasFocus);

        drawn.Press(Key.CursorDown);

        Assert.False(drawn.SearchField.HasFocus);
        Assert.Equal(row, ContentClicks.RowOf(drawn, "│ ⌕ cat"));

        drawn.Type("x");

        Assert.Equal("cat", drawn.SearchField.Text);
    }

    /// <summary>
    ///     <c>↓</c> from the field walks into the trends, picking the first; <c>↑</c> from the first trend comes back
    ///     to the field, taking letters again.
    /// </summary>
    [Fact]
    public async Task Arrows_WalkOffTheFieldAndBackOntoIt()
    {
        using var drawn = await Prompt(Trending());
        var prompt = Assert.IsType<SearchScreen>(drawn.Shell.Screen);

        drawn.Press(Key.CursorDown);

        Assert.Equal("caturday", prompt.PickedHashtag?.Name);

        drawn.Press(Key.CursorUp);

        Assert.True(prompt.OnTheField);
        Assert.True(drawn.SearchField.HasFocus);
    }

    /// <summary>
    ///     With a trend picked, letters are keys: <c>k</c> walks on to the next trend and <c>j</c> back, onto the field
    ///     from the first — none of them typed.
    /// </summary>
    [Fact]
    public async Task Letters_OnATrend_WalkRatherThanType()
    {
        using var drawn = await Prompt(Trending());
        var prompt = Assert.IsType<SearchScreen>(drawn.Shell.Screen);

        drawn.Press(Key.CursorDown);
        drawn.Press(Key.K);

        Assert.Equal("SilentSunday", prompt.PickedHashtag?.Name);

        drawn.Press(Key.J);
        drawn.Press(Key.J);

        Assert.True(prompt.OnTheField);
        Assert.Empty(drawn.SearchField.Text);

        // On the field again, letters are typed.
        drawn.Type("jk");

        Assert.Equal("jk", drawn.SearchField.Text);
        Assert.True(prompt.OnTheField);
    }

    /// <summary>
    ///     <c>[</c> and <c>]</c> move between the sections from the field as from any trend, and are never typed into the
    ///     query.
    /// </summary>
    [Fact]
    public async Task Brackets_MoveBetweenSectionsFromAnywhere_AndAreNeverTyped()
    {
        using var drawn = await Prompt(Trending());
        var prompt = Assert.IsType<SearchScreen>(drawn.Shell.Screen);

        drawn.Type("ca");
        drawn.Press(new Key(']'));

        Assert.Equal("caturday", prompt.PickedHashtag?.Name);

        drawn.Press(new Key(']'));

        Assert.Equal("Cats learn to type", prompt.PickedLink?.Title);

        drawn.Press(new Key(']'));

        Assert.Equal("Biscuit knocked the pen off the desk", prompt.Picked?.Content);

        drawn.Press(new Key('['));

        Assert.Equal("Cats learn to type", prompt.PickedLink?.Title);
        Assert.Equal("ca", drawn.SearchField.Text);
    }

    /// <summary>A post key acts on a trending post picked under the field, without opening it first.</summary>
    [Fact]
    public async Task PostKeys_ActOnATrendingPost()
    {
        var built = Trending();
        using var drawn = await Prompt(built);

        drawn.Press(new Key(']'));
        drawn.Press(new Key(']'));
        drawn.Press(new Key(']'));
        drawn.Press(Key.F);
        drawn.Settle();

        var marked = Assert.Single(built.Engagement.Marks);
        Assert.Equal("biscuit-1", marked.PostId);
        Assert.Equal(PostMark.Favorite, marked.Mark);
    }

    /// <summary>
    ///     <c>/</c> with a trend picked goes back to the field with what it held kept, rather than arriving at a fresh
    ///     prompt — and asks nothing.
    /// </summary>
    [Fact]
    public async Task Slash_WithATrendPicked_ReturnsToTheFieldWithTheQueryKept()
    {
        var built = Trending();
        using var drawn = await Prompt(built);
        var prompt = Assert.IsType<SearchScreen>(drawn.Shell.Screen);
        var read = built.Trends.Reads.Count;

        drawn.Type("cat");
        drawn.Press(Key.CursorDown);
        drawn.Press(Key.K);
        drawn.Press(new Key('/'));
        drawn.Settle();

        Assert.Same(prompt, drawn.Shell.Screen);
        Assert.True(prompt.OnTheField);
        Assert.True(drawn.SearchField.HasFocus);
        Assert.Equal("cat", drawn.SearchField.Text);
        Assert.Equal(read, built.Trends.Reads.Count);

        // The caret where it was left, and nothing selected to be typed over.
        Assert.Equal((3, 0), (drawn.SearchField.InsertionPoint, drawn.SearchField.SelectedLength));

        // On the field, / is typed.
        drawn.Type("/");

        Assert.Equal("cat/", drawn.SearchField.Text);
    }

    /// <summary>
    ///     <c>⏎</c> on a trending hashtag opens its tag, as a hashtag a search found does — pushed over the prompt, for
    ///     <c>esc</c> to come back to.
    /// </summary>
    [Fact]
    public async Task Enter_OnATrendingHashtag_OpensItsTag()
    {
        using var drawn = await Prompt(Trending());

        drawn.Press(Key.CursorDown);
        drawn.Press(Key.Enter);
        drawn.Settle();

        Assert.IsType<FeedScreen>(drawn.Shell.Screen);
        Assert.Equal("Search › #caturday", drawn.Shell.Breadcrumb);
    }

    /// <summary><c>⏎</c> on a trending post opens its thread, as a post a search found does.</summary>
    [Fact]
    public async Task Enter_OnATrendingPost_OpensItsThread()
    {
        using var drawn = await Prompt(Trending());

        drawn.Press(new Key(']'));
        drawn.Press(new Key(']'));
        drawn.Press(new Key(']'));
        drawn.Press(Key.Enter);
        drawn.Settle();

        var thread = Assert.IsType<PostScreen>(drawn.Shell.Screen);
        Assert.Equal("biscuit-1", thread.Picked?.Id);
    }

    /// <summary>A click on a trend picks it, the field giving up the letters; a double click opens it.</summary>
    [Fact]
    public async Task Click_PicksATrend_AndADoubleClickOpensIt()
    {
        using var drawn = await Prompt(Trending());
        var prompt = Assert.IsType<SearchScreen>(drawn.Shell.Screen);

        drawn.Click(ContentClicks.OverContent, ContentClicks.RowOf(drawn, "#SilentSunday"));

        Assert.Equal("SilentSunday", prompt.PickedHashtag?.Name);
        Assert.False(drawn.SearchField.HasFocus);

        drawn.DoubleClick(ContentClicks.OverContent, ContentClicks.RowOf(drawn, "#caturday"));
        drawn.Settle();

        Assert.Equal("Search › #caturday", drawn.Shell.Breadcrumb);
    }

    /// <summary>
    ///     A click on the field with a trend picked picks the field, which takes letters again from where the click put
    ///     its caret.
    /// </summary>
    [Fact]
    public async Task Click_OnTheField_PicksItAndItTakesLetters()
    {
        using var drawn = await Prompt(Trending());
        var prompt = Assert.IsType<SearchScreen>(drawn.Shell.Screen);

        drawn.Type("cat");
        drawn.Press(Key.CursorDown);

        var row = ContentClicks.RowOf(drawn, "│ ⌕ cat");
        var column = drawn.Rows()[row].IndexOf("cat", StringComparison.Ordinal) + 3;

        drawn.Click(column, row);
        drawn.Type("s");

        Assert.True(prompt.OnTheField);
        Assert.True(drawn.SearchField.HasFocus);
        Assert.Equal("cats", drawn.SearchField.Text);
    }

    /// <summary>
    ///     The field scrolls with the prompt's rows: walked down until its row is off the page, it is nowhere on it, and
    ///     walked back up it is laid where its row is again.
    /// </summary>
    [Fact]
    public async Task Field_ScrollsWithThePage()
    {
        var built = new AShell
        {
            Trends = FakeTrends.Trending(
                hashtags: [.. Enumerable.Range(1, 10).Select(at => AHashtag.With($"tag{at}"))],
                links: [.. Enumerable.Range(1, 10).Select(at => ATrendingLink.With(url: $"https://a.example/{at}"))],
                posts: [.. Enumerable.Range(1, 3).Select(at => APost.With(id: $"{at}"))]),
        };
        using var drawn = await DrawnShell.Of(80, 24, Themes.Dark, built);

        drawn.Press(new Key('/'));
        drawn.Settle();
        drawn.Type("sheep");

        var row = ContentClicks.RowOf(drawn, "│ ⌕ sheep");

        drawn.Press(Key.CursorDown);
        drawn.WalkToTheLast();

        Assert.DoesNotContain(drawn.Rows(), line => line.Contains("⌕", StringComparison.Ordinal));
        Assert.DoesNotContain(drawn.Rows(), line => line.Contains("sheep", StringComparison.Ordinal));

        drawn.Press(new Key('/'));
        drawn.Settle();

        Assert.Equal(row, ContentClicks.RowOf(drawn, "│ ⌕ sheep"));
    }

    /// <summary>
    ///     A trending hashtag's row says how many people used it lately, the count against the row's right edge — so the
    ///     counts stand in one column however long the tags are, and nothing moves as the trends change.
    /// </summary>
    [Fact]
    public async Task Rows_SayHowManyPeopleUsedAHashtag_AgainstTheRightEdge()
    {
        var shell = Trending();
        var opened = await shell.Opened();

        opened.Search();
        shell.Host.Drain();

        var drawn = AShell.Drawn(opened.Screen);
        var caturday = drawn.Single(row => row.StartsWith("#caturday", StringComparison.Ordinal));
        var sunday = drawn.Single(row => row.StartsWith("#SilentSunday", StringComparison.Ordinal));

        Assert.EndsWith(" 1,517 people in recent days", caturday, StringComparison.Ordinal);
        Assert.EndsWith(" 9 people in recent days", sunday, StringComparison.Ordinal);
        Assert.Equal(60, caturday.Length);
        Assert.Equal(60, sunday.Length);
    }

    /// <summary>
    ///     A trending link is three rows — its title, its description, then its site and how many people are sharing
    ///     it — with a blank between one link and the next, and no picture.
    /// </summary>
    [Fact]
    public async Task Rows_DrawALinkAsThreeRowsWithABlankBetweenLinks()
    {
        var shell = new AShell
        {
            Trends = FakeTrends.Trending(
                hashtags: [],
                posts: [],
                links:
                [
                    ATrendingLink.With("https://a.example/1", "First", "One", "A", peopleSharing: 1),
                    ATrendingLink.With("https://b.example/2", "Second", "Two", "B", peopleSharing: 2_048),
                ]),
        };
        var opened = await shell.Opened();

        opened.Search();
        shell.Host.Drain();

        var drawn = AShell.Drawn(opened.Screen).ToList();
        var first = drawn.IndexOf("First");

        Assert.Equal(
            ["First", "One", "A · 1 person sharing", "", "Second", "Two", "B · 2,048 people sharing"],
            drawn.Skip(first).Take(7).Select(row => row.TrimEnd()));
    }

    /// <summary>Trending posts are the ordinary post block, a rule between one and the next.</summary>
    [Fact]
    public async Task Rows_RuleBetweenTrendingPosts()
    {
        var shell = new AShell
        {
            Trends = FakeTrends.Trending(
                hashtags: [],
                links: [],
                posts: [APost.With(id: "1", content: "First post"), APost.With(id: "2", content: "Second post")]),
        };
        var opened = await shell.Opened();

        opened.Search();
        shell.Host.Drain();

        var lines = opened.Screen.Lines(new Drawing(61, AShell.Now));
        var first = lines.ToList().FindIndex(line => line.Text.Contains("First post", StringComparison.Ordinal));
        var second = lines.ToList().FindIndex(line => line.Text.Contains("Second post", StringComparison.Ordinal));

        Assert.Contains(lines.Skip(first).Take(second - first), line => line.Ruled);
    }

    /// <summary>
    ///     An instance with nothing trending says so under the field, so that an empty Search does not look broken.
    /// </summary>
    [Fact]
    public async Task Search_SaysWhereNothingIsTrending()
    {
        var shell = new AShell { Trends = FakeTrends.TrendingNothing() };
        var opened = await shell.Opened();

        opened.Search();
        shell.Host.Drain();

        Assert.Contains(AShell.Drawn(opened.Screen), row => row == "Nothing trending on this instance");
    }

    /// <summary>
    ///     A trends read the instance turns down leaves the prompt usable — the field taking letters and searching —
    ///     and says why on the status row.
    /// </summary>
    [Fact]
    public async Task Search_WhereTheTrendsAreRefused_LeavesThePromptUsableAndSaysWhy()
    {
        var built = new AShell { Trends = FakeTrends.Refusing("Trends are off on this instance") };
        using var drawn = await Prompt(built);

        Assert.IsType<SearchScreen>(drawn.Shell.Screen);
        Assert.Contains("Trends are off on this instance", drawn.Shell.Notice, StringComparison.Ordinal);
        Assert.True(drawn.SearchField.HasFocus);

        drawn.Type("cats");
        drawn.Press(Key.Enter);
        drawn.Settle();

        Assert.IsType<SearchResultsScreen>(drawn.Shell.Screen);
    }

    /// <summary>
    ///     On the field, the status row offers only what is not typed: searching, walking into the trends, moving
    ///     between their sections, and leaving.
    /// </summary>
    [Fact]
    public async Task Keys_OnTheField_OfferOnlyWhatIsNotTyped()
    {
        var shell = Trending();
        var opened = await shell.Opened();

        opened.Search();
        shell.Host.Drain();

        Assert.Equal(
            ["⏎ search", "↓ trends", "[/] section", "tab destination"],
            opened.Keys.Select(key => $"{key.Key} {key.Does}"));
    }

    /// <summary>
    ///     On a trend, the status row offers the keys that do something on it: <c>⏎</c> on a hashtag, which opens, and
    ///     on a link with <c>o</c> for its article (#607); a post's keys on a post; and <c>/</c> back to the field on
    ///     each.
    /// </summary>
    [Fact]
    public async Task Keys_OnATrend_OfferWhatActsOnIt()
    {
        var shell = Trending();
        var opened = await shell.Opened();

        opened.Search();
        shell.Host.Drain();
        opened.Walk(1, reclaiming: null);

        var onAHashtag = opened.Keys.Select(key => $"{key.Key} {key.Does}").ToList();

        Assert.Equal(["j/k trend", "[/] section", "⏎ open", "/ search"], onAHashtag.Take(4));

        opened.Walk(2, reclaiming: null);

        var onALink = opened.Keys.Select(key => $"{key.Key} {key.Does}").ToList();

        Assert.Equal(["j/k trend", "[/] section", "⏎ open", "o article", "/ search"], onALink.Take(5));

        opened.Walk(1, reclaiming: null);

        var onAPost = opened.Keys.Select(key => key.Key).ToList();

        Assert.Contains("f", onAPost);
        Assert.Contains("/", onAPost);
        Assert.Contains("⏎", onAPost);
    }

    /// <summary>The trends read for the shell in these tests: two hashtags, a link and a post.</summary>
    private static AShell Trending() => new()
    {
        Trends = FakeTrends.Trending(
            hashtags:
            [
                AHashtag.With("caturday", recentAccounts: 1517),
                AHashtag.With("SilentSunday", recentAccounts: 9),
            ],
            posts:
            [
                APost.With(
                    id: "biscuit-1",
                    account: "biscuit@mastodon.social",
                    content: "Biscuit knocked the pen off the desk"),
            ],
            links: [ATrendingLink.With(title: "Cats learn to type")]),
    };

    /// <summary>The whole shell drawn headless, gone to search with <c>/</c> and the trends read.</summary>
    private static async Task<DrawnShell> Prompt(AShell built)
    {
        var drawn = await DrawnShell.Of(80, ContentClicks.Tall, Themes.Dark, built);

        drawn.Press(new Key('/'));
        drawn.Settle();

        return drawn;
    }
}
