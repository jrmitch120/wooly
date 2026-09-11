using Wooly.Core.Search;
using Wooly.Tests.Fakes;
using Wooly.Tui.Rendering;
using Wooly.Tui.Screens;
using Wooly.Tui.Theme;

namespace Wooly.Tests.Tui;

/// <summary>
///     The headed runs a screen draws and what <c>[</c> and <c>]</c> do about them: the pick lands on the first thing
///     of the run before or after the one the reader is in. Asked of the rows rather than of the screen that drew
///     them, so the runs a reader can see are the runs the key moves between — the same reason a row says which thing
///     it is part of (#51, #166).
/// </summary>
public class SectionTests
{
    /// <summary>Three runs of a search — three accounts, two hashtags and four posts, numbered as one list.</summary>
    private static readonly int[] Found = [3, 2, 4];

    /// <summary><c>]</c> from an account is the first hashtag, which is the first thing of the next run and not the next thing.</summary>
    [Fact]
    public void Along_PicksOutTheFirstThingOfTheRunAfterThisOne() =>
        Assert.Equal(3, Sections.Along(Headed(picked: 1, Found), by: 1, reclaiming: null));

    /// <summary>
    ///     And <c>[</c> from a post is the first hashtag rather than the first post: the run before the one the reader
    ///     is in, whichever thing in it they were standing on.
    /// </summary>
    [Theory]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(8)]
    public void Along_PicksOutTheFirstThingOfTheRunBeforeThisOne(int picked) =>
        Assert.Equal(3, Sections.Along(Headed(picked, Found), by: -1, reclaiming: null));

    /// <summary>
    ///     The ends clamp and never wrap: a reader already among the last run presses <c>]</c> and nothing happens,
    ///     and <c>[</c> from the first run is the same. Nothing in this shell wraps.
    /// </summary>
    [Theory]
    [InlineData(6, 1)]
    [InlineData(8, 1)]
    [InlineData(0, -1)]
    [InlineData(2, -1)]
    public void Along_AnswersNothingAtEitherEnd(int picked, int by) =>
        Assert.Null(Sections.Along(Headed(picked, Found), by, reclaiming: null));

    /// <summary>
    ///     Reclaimed, the jump runs from the thing the reader is actually looking at rather than from the pick that
    ///     has been scrolled off the page — which is the exact failure reclaiming exists to prevent.
    /// </summary>
    [Fact]
    public void Along_RunsFromWhatIsReclaimedRatherThanFromThePick()
    {
        var lines = Headed(picked: 0, Found);

        // The pick is on the first account; the page has been walked down to the posts.
        Assert.Equal(5, Sections.Along(lines, by: 1, reclaiming: 3));
        Assert.Null(Sections.Along(lines, by: 1, reclaiming: 6));
        Assert.Equal(3, Sections.Along(lines, by: -1, reclaiming: 6));
    }

    /// <summary>
    ///     A screen whose rows carry no heading at all has no run to move to, so the key does nothing rather than
    ///     meaning something else — which is what makes <c>[</c> and <c>]</c> safe to bind everywhere.
    /// </summary>
    [Fact]
    public void Along_AnswersNothingWhereNoRowHeadsAnything() =>
        Assert.Null(Sections.Along(
            [.. Enumerable.Range(0, 5).Select(item => Line.Of("text", Role.Selection).PartOf(item))],
            by: 1,
            reclaiming: null));

    /// <summary>And a screen with nothing picked out on it is nothing to move from.</summary>
    [Fact]
    public void Along_AnswersNothingWhereNothingIsPickedOut() =>
        Assert.Null(Sections.Along(Headed(picked: -1, Found), by: 1, reclaiming: null));

    /// <summary>
    ///     A thing standing above the first heading — the account screen's header block, which is a run of one with
    ///     nothing over it — is where <c>]</c> reaches the first headed run from, and where <c>[</c> clamps.
    /// </summary>
    [Fact]
    public void Along_ReachesTheFirstRunFromAThingAboveEveryHeading()
    {
        IReadOnlyList<Line> lines = [Line.Of("header", Role.Selection).PartOf(0), .. Headed(picked: -1, [2], from: 1)];

        Assert.Equal(1, Sections.Along(lines, by: 1, reclaiming: null));
        Assert.Null(Sections.Along(lines, by: -1, reclaiming: null));
    }

    /// <summary>
    ///     The screen this was built for, and the pick it leaves behind: <c>]</c> from the account a search found
    ///     picks out the first hashtag, and <c>[</c> from there picks the account back. No terminal in the room —
    ///     the rows are the screen's own.
    /// </summary>
    [Fact]
    public void ASearchScreenMovesThePickBetweenItsThreeKinds()
    {
        var search = Searched();

        Assert.Equal(0, search.At);

        search.Pick(Along(search, by: 1));

        Assert.Equal(2, search.At);
        Assert.NotNull(search.PickedHashtag);

        search.Pick(Along(search, by: 1));

        Assert.Equal(3, search.At);
        Assert.NotNull(search.Picked);

        search.Pick(Along(search, by: -1));

        Assert.Equal(2, search.At);
    }

    /// <summary>And it clamps there, on the very rows the screen draws.</summary>
    [Fact]
    public void ASearchScreenClampsAtItsFirstRunAndItsLast()
    {
        var search = Searched();

        Assert.Null(Sections.Along(Drawn(search), by: -1, reclaiming: null));

        search.Pick(3);

        Assert.Null(Sections.Along(Drawn(search), by: 1, reclaiming: null));
    }

    /// <summary>
    ///     Every kind is headed with how many of it were found — the <c>── {n} replies ──</c> vocabulary the post
    ///     screen already speaks, which tells a reader whether the run below is worth walking or worth jumping.
    /// </summary>
    [Fact]
    public void ASearchScreenHeadsEveryKindWithHowManyItFound() =>
        Assert.Equal(
            ["── 2 accounts ──", "── 1 hashtags ──", "── 3 posts ──"],
            Headings(Searched(posts: 3)));

    /// <summary>
    ///     And a kind nothing was found of draws no heading at all rather than a heading over nothing: three
    ///     "nothing" headings on a two-result search is noise, and per-kind absence is the ordinary case. The post
    ///     screen's <c>── replies ──</c> zero-fallback does not reach here.
    /// </summary>
    [Fact]
    public void ASearchScreenDrawsNoHeadingForAKindItFoundNothingOf() =>
        Assert.Equal(["── 1 hashtags ──"], Headings(Searched(accounts: 0, posts: 0)));

    /// <summary>
    ///     Accounts are separated the way every other screen listing people separates them: a blank between two of
    ///     them and no rule anywhere, four-row Account blocks laid end to end being what runs together (#180, #198).
    /// </summary>
    [Fact]
    public void ASearchScreenPutsABlankBetweenAccountsAndNoRule()
    {
        // The prompt, its blank, the heading, its blank, four rows of Alice, a blank, four rows of Bob.
        var rows = Drawn(Searched(hashtags: 0, posts: 0));

        Assert.Equal(13, rows.Count);
        Assert.Equal(string.Empty, rows[8].Text.Trim());
        Assert.DoesNotContain(rows.Where(line => !line.Heads), line => line.Text.Contains('─', StringComparison.Ordinal));
    }

    /// <summary>
    ///     And nothing at all between hashtags, which are one row apiece: a separator per row would spend as many rows
    ///     on the gaps as on the tags, where the heading has already said how many there are and the gutter says which
    ///     one is picked out.
    /// </summary>
    [Fact]
    public void ASearchScreenPutsNothingBetweenHashtags()
    {
        var rows = Drawn(Searched(accounts: 0, hashtags: 3, posts: 0));

        Assert.Equal(7, rows.Count);
        Assert.All(rows.Skip(4), line => Assert.Contains("#tag", line.Text, StringComparison.Ordinal));
    }

    /// <summary>
    ///     A run of hashtags reads down as well as across: the counts stand in one column whatever the names are, and
    ///     the numbers are right-aligned inside it so <c>81</c> falls under <c>4</c>.
    /// </summary>
    [Fact]
    public void ASearchScreenStandsAHashtagsCountsInTheRunsOwnColumns()
    {
        var search = new SearchScreen();

        search.Found(
            "sheep",
            new SearchResults
            {
                Hashtags =
                [
                    AHashtag.With("ewe", recentPosts: 81, recentAccounts: 7),
                    AHashtag.With("shearing", recentPosts: 4, recentAccounts: 1_204),
                ],
            });

        // Past the gutter, which says which of them is picked out rather than anything about the columns.
        Assert.Equal(
            [
                "#ewe       81 posts ·     7 accounts",
                "#shearing   4 posts · 1,204 accounts",
            ],
            Drawn(search).Skip(4).Select(line => line.Text[1..]));
    }

    /// <summary>
    ///     A terminal too narrow to hold the columns loses them altogether, the whole run falling back to a two-space
    ///     gap and unpadded counts: padding is spent from the left and the row is clipped from the right, so columns
    ///     held past the width would cost the accounts count that fits without them.
    /// </summary>
    [Fact]
    public void ASearchScreenGivesUpTheHashtagColumnsWhereTheyWouldNotFit()
    {
        var search = new SearchScreen();

        search.Found(
            "sheep",
            new SearchResults
            {
                Hashtags =
                [
                    AHashtag.With("photography", recentPosts: 1_204, recentAccounts: 7),
                    AHashtag.With("ewe", recentPosts: 8, recentAccounts: 1_204),
                ],
            });

        // One column of gutter and 30 of room, where the widest row wants 12 + 2 + 28.
        Assert.Equal(
            [
                "#photography  1,204 posts · 7…",
                "#ewe  8 posts · 1,204 accounts",
            ],
            search.Lines(new Drawing(31, AShell.Now)).Skip(4).Select(line => line.Text[1..]));
    }

    /// <summary>
    ///     Posts keep the rule a feed puts between them, because a run of them here is a feed — and between rather
    ///     than after, so nothing is left hanging under the last result on the screen (#62).
    /// </summary>
    [Fact]
    public void ASearchScreenRulesBetweenPostsAndLeavesNoneUnderTheLast()
    {
        var rows = Drawn(Searched(accounts: 0, hashtags: 0, posts: 3));

        Assert.Equal(2, rows.Count(Rules));
        Assert.False(Rules(rows[^1]));
    }

    /// <summary>
    ///     One kind is parted from the next by the blank over its heading — the same blank an account's header puts
    ///     over each of its sections. The first heading has the prompt's own blank above it and takes none of its own.
    /// </summary>
    [Fact]
    public void ASearchScreenPartsItsKindsWithTheBlankOverEachHeadingButTheFirst()
    {
        var rows = Drawn(Searched());
        var heads = rows.Select((line, at) => (line, at)).Where(row => row.line.Heads).Select(row => row.at).ToList();

        Assert.Equal(2, heads[0]);
        Assert.All(heads.Skip(1), at => Assert.Equal(string.Empty, rows[at - 1].Text.Trim()));
    }

    /// <summary>
    ///     The status row says <c>[/]:section</c> only where there are two or more runs to move between, and says it
    ///     immediately after the key that walks the results — a key announced where it does nothing reads as a shell
    ///     that missed the press.
    /// </summary>
    /// <remarks>
    ///     Both branches of the row: an account picked out has no post for the marks to act on, and a post picked out
    ///     puts this screen's own keys ahead of theirs.
    /// </remarks>
    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    public void ASearchScreenSaysTheSectionKeyWhereTwoKindsFoundSomething(int picked)
    {
        var search = Searched();

        search.Pick(picked);

        Assert.Equal(["j/k", "[/]"], search.Keys.Take(2).Select(key => key.Key));
        Assert.Equal("section", search.Keys[1].Does);
    }

    /// <summary>One kind found is one run, and one run has nowhere to jump to — so the key is left off the row.</summary>
    [Fact]
    public void ASearchScreenLeavesTheSectionKeyOffWhereOnlyOneKindFoundAnything() =>
        Assert.DoesNotContain(Searched(hashtags: 0, posts: 0).Keys, key => key.Key == "[/]");

    /// <summary>And never while the prompt is taking letters, where every key on the row would be typed instead.</summary>
    [Fact]
    public void ASearchScreenSaysNothingOfSectionsWhileThePromptIsTakingLetters() =>
        Assert.DoesNotContain(new SearchScreen().Keys, key => key.Key == "[/]");

    /// <summary>
    ///     Whether a row is a rule between two things — a heading is drawn out of the same glyph and is not one.
    /// </summary>
    private static bool Rules(Line line) =>
        !line.Heads && line.Text.Length > 0 && line.Text.All(letter => letter == '─');

    /// <summary>What the rows this screen draws head their runs with.</summary>
    private static IEnumerable<string> Headings(SearchScreen search) =>
        Drawn(search).Where(line => line.Heads).Select(line => line.Text);

    /// <summary>
    ///     A search of <paramref name="accounts" /> accounts, <paramref name="hashtags" /> hashtags and
    ///     <paramref name="posts" /> posts, with the first result picked out.
    /// </summary>
    private static SearchScreen Searched(int accounts = 2, int hashtags = 1, int posts = 2)
    {
        var search = new SearchScreen();

        search.Found(
            "sheep",
            new SearchResults
            {
                Accounts = [.. Enumerable.Range(0, accounts).Select(at => AnAccount.With(address: $"a{at}@ha.io"))],
                Hashtags = [.. Enumerable.Range(0, hashtags).Select(at => AHashtag.With(name: $"tag{at}"))],
                Posts = [.. Enumerable.Range(0, posts).Select(at => APost.With(id: $"{at}0"))],
            });

        return search;
    }

    /// <summary>Where <paramref name="by" /> takes the pick on that screen, as its own rows say.</summary>
    private static int Along(SearchScreen search, int by) =>
        Sections.Along(Drawn(search), by, reclaiming: null) ?? -1;

    private static IReadOnlyList<Line> Drawn(SearchScreen search) => search.Lines(new Drawing(61, AShell.Now));

    /// <summary>
    ///     Rows of headed runs, <paramref name="runs" /> giving how many things stand under each heading — one row
    ///     apiece, numbered across the runs as the one list a screen walks.
    /// </summary>
    /// <param name="picked">Which thing carries the selection, or <c>-1</c> for a screen with nothing picked out.</param>
    /// <param name="from">The ordinal the first thing takes, for rows that begin above the first heading.</param>
    private static IReadOnlyList<Line> Headed(int picked, IReadOnlyList<int> runs, int from = 0)
    {
        var lines = new List<Line>();
        var item = from;

        foreach (var run in runs)
        {
            lines.Add(Line.Of("── heading ──", Role.Muted).Heading());

            for (var of = 0; of < run; of++, item++)
            {
                lines.Add(Line.Of("text", item == picked ? Role.Selection : Role.Body).PartOf(item));
            }
        }

        return lines;
    }
}
