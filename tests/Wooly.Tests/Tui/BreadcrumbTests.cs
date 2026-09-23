using Wooly.Tui.Rendering;
using Wooly.Tui.Screens;
using Wooly.Tui.Theme;

namespace Wooly.Tests.Tui;

/// <summary>
///     The row that says where you are standing. Two things are asserted here and nowhere else: that the crumb you
///     are on is told from the ones you walked through, and that a trail too long for its width loses the ancestors
///     rather than the destination (#216). What divides the row from the content under it is
///     <see cref="SeamTests" />'s.
/// </summary>
/// <remarks>
///     Role selection and layout with no terminal in the room (ADR-0005, ADR-0014): what the row is made of, in the
///     columns it has, which is the whole of what this change moved.
/// </remarks>
public class BreadcrumbTests
{
    /// <summary>The content region's width at an 80-column terminal, which is what the rail leaves (RailLines).</summary>
    private const int ContentWidth = 61;

    /// <summary>
    ///     The crumb you are standing on takes <see cref="Role.CrumbCurrent" /> and every crumb you walked through to
    ///     get there takes <see cref="Role.Crumb" /> — separators included, which keep no role of their own.
    /// </summary>
    [Fact]
    public void Breadcrumb_DrawsTheCrumbYouAreStandingOnInItsOwnRole()
    {
        var row = ChromeLines.Breadcrumb(["Home", "Post by @ben", "@ben@hachyderm.io"], dots: 0, ContentWidth);

        var said = Written(row);

        Assert.Equal(
            [
                (Role.Crumb, "Home"),
                (Role.Crumb, " › "),
                (Role.Crumb, "Post by @ben"),
                (Role.Crumb, " › "),
                (Role.CrumbCurrent, "@ben@hachyderm.io"),
            ],
            said);
    }

    /// <summary>
    ///     And a trail one crumb long is a trail you are standing at the end of, so that crumb is the current one —
    ///     the state every rail destination opens in.
    /// </summary>
    [Fact]
    public void Breadcrumb_DrawsAOneCrumbTrailAsTheCrumbYouAreStandingOn()
    {
        var row = ChromeLines.Breadcrumb(["Home"], dots: 0, ContentWidth);

        Assert.Equal([(Role.CrumbCurrent, "Home")], Written(row));
    }

    /// <summary>A trail that fits is drawn as it arrived, with nothing led in front of it.</summary>
    [Fact]
    public void Breadcrumb_LeadsATrailThatFitsWithNothing()
    {
        var row = ChromeLines.Breadcrumb(["Home", "Post by @ben"], dots: 0, ContentWidth);

        Assert.StartsWith("Home", row.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("…", row.Text, StringComparison.Ordinal);
    }

    /// <summary>
    ///     A trail too long for its width keeps the crumb you are standing on whole and gives up the ones you walked
    ///     through, saying so with <c>… › </c> — the clip this replaced cut from the right, which kept the crumbs
    ///     saying where you came from and lost the one saying where you are.
    /// </summary>
    /// <remarks>
    ///     Five deep at 61 columns, which is an 80-column terminal: the last crumb is the one the acceptance criteria
    ///     names, and the lead is what says the row is not the whole trail.
    /// </remarks>
    [Fact]
    public void Breadcrumb_ElidesADeepTrailFromTheLeft()
    {
        var row = ChromeLines.Breadcrumb(
            [
                "Home",
                "Post by @ben@hachyderm.io",
                "@maria@mastodon.social",
                "@maria@mastodon.social following",
                "Keys",
            ],
            dots: 0,
            ContentWidth);

        Assert.StartsWith("… › ", row.Text, StringComparison.Ordinal);
        Assert.EndsWith("› Keys", row.Text.TrimEnd(), StringComparison.Ordinal);
        Assert.True(row.Width <= ContentWidth, $"The row is {row.Width} columns wide.");

        Assert.Contains(row.Spans, span => span is { Role: Role.CrumbCurrent, Text: "Keys" });
        Assert.Contains(row.Spans, span => span is { Role: Role.Crumb, Text: "… › " });
    }

    /// <summary>
    ///     And a trail only two deep elides the same way, since a single crumb can be longer than the row on its
    ///     own: what is dropped is whatever will not fit, and the destination the rail is already drawing is the
    ///     first to go.
    /// </summary>
    [Fact]
    public void Breadcrumb_ElidesAShallowTrailFromTheLeftToo()
    {
        var row = ChromeLines.Breadcrumb(
            ["@maria@mastodon.social", "Post by @somebodywithaverylongname@instance.example"],
            dots: 0,
            ContentWidth);

        Assert.StartsWith("… › ", row.Text, StringComparison.Ordinal);
        Assert.Contains(
            row.Spans,
            span => span is { Role: Role.CrumbCurrent } && span.Text.StartsWith("Post by", StringComparison.Ordinal));
        Assert.True(row.Width <= ContentWidth, $"The row is {row.Width} columns wide.");
    }

    /// <summary>
    ///     The fetch mark's columns come off the trail before the elision is worked out, which is what
    ///     <see cref="ChromeLines.Breadcrumb" /> already did with its clip: a trail that fits beside no mark elides
    ///     beside one rather than being drawn over it.
    /// </summary>
    [Fact]
    public void Breadcrumb_WorksTheTrailsRoomOutAfterTheFetchMark()
    {
        // 55 columns: inside the 60 the row leaves a trail with no mark beside it, and past the 51 it leaves one
        // with a mark on it.
        string[] trail = ["Home", "Post by @ben@hachyderm.io", "@maria@fosstodon.org"];

        var still = ChromeLines.Breadcrumb(trail, dots: 0, ContentWidth);
        var busy = ChromeLines.Breadcrumb(trail, dots: 1, ContentWidth);

        Assert.DoesNotContain("…", still.Text, StringComparison.Ordinal);
        Assert.StartsWith("… › ", busy.Text, StringComparison.Ordinal);
        Assert.Contains("fetching.", busy.Text);
        Assert.True(busy.Width <= ContentWidth, $"The row is {busy.Width} columns wide.");
    }

    /// <summary>
    ///     No dots is no mark at all — not the bare word, which on the one row whose job is to say <em>working</em>
    ///     reads as <em>finished</em>. It is what a fetch not yet a tick old draws, so one that lands in 80ms shows
    ///     nothing (#217).
    /// </summary>
    [Fact]
    public void Breadcrumb_DrawsNoMarkForNoDots()
    {
        var row = ChromeLines.Breadcrumb(["Home"], dots: 0, ContentWidth);

        Assert.DoesNotContain("fetching", row.Text, StringComparison.Ordinal);
        Assert.DoesNotContain(row.Spans, span => span.Role == Role.Loading && span.Text.Length > 0);
    }

    /// <summary>
    ///     One, two and three dots each take the same rightmost 11 columns, the word at the left of them and the dots
    ///     growing rightward into the rest — so the word never moves, and the trail beside it is the same string on
    ///     every tick rather than re-elided on each one (#217).
    /// </summary>
    [Theory]
    [InlineData(1, "fetching.  ")]
    [InlineData(2, "fetching.. ")]
    [InlineData(3, "fetching...")]
    public void Breadcrumb_DrawsEveryPhaseOfTheMarkInTheSameElevenColumns(int dots, string mark)
    {
        string[] trail = ["Home", "Post by @ben@hachyderm.io"];

        var row = ChromeLines.Breadcrumb(trail, dots, ContentWidth);
        var first = ChromeLines.Breadcrumb(trail, dots: 1, ContentWidth);

        Assert.Equal(ContentWidth, row.Width);
        Assert.EndsWith(mark, row.Text, StringComparison.Ordinal);
        Assert.Equal(first.Text[..^11], row.Text[..^11]);
    }

    /// <summary>
    ///     A trail deep enough to elide elides the same way on every tick of a whole cycle — which is what padding the
    ///     mark to its widest buys: a mark 9 columns wide on one tick and 11 on the next would move the row at both
    ///     ends on each of them.
    /// </summary>
    /// <remarks>
    ///     And the 11 columns come off the trail, leaving it 49 of the 61 rather than the 51 <c>fetching…</c> left it.
    /// </remarks>
    [Fact]
    public void Breadcrumb_ElidesADeepTrailTheSameWayAcrossAWholeCycle()
    {
        string[] trail =
        [
            "Home",
            "Post by @ben@hachyderm.io",
            "@maria@mastodon.social",
            "@maria@mastodon.social following",
            "Keys",
        ];

        var cycle = new[] { 1, 2, 3, 1 }.Select(dots => ChromeLines.Breadcrumb(trail, dots, ContentWidth)).ToList();
        var trails = cycle.Select(row => row.Text[..^11]).Distinct().ToList();

        Assert.Single(trails);
        Assert.StartsWith("… › ", trails[0], StringComparison.Ordinal);
        Assert.True(trails[0].TrimEnd().Length <= 49, $"The trail is {trails[0].TrimEnd().Length} columns wide.");
        Assert.All(cycle, row => Assert.Equal(ContentWidth, row.Width));
    }

    /// <summary>
    ///     A crumb that has the separator inside it is still one crumb, because the row is drawn from the stack
    ///     rather than from the one string it reads as: <c>/</c> takes whatever is typed at it, so <c>a › b</c> is a
    ///     query somebody is entitled to search for.
    /// </summary>
    [Fact]
    public void Breadcrumb_DrawsACrumbThatHasTheSeparatorInItAsOneCrumb()
    {
        var row = ChromeLines.Breadcrumb(["Home", "Search a › b"], dots: 0, ContentWidth);

        Assert.Equal(
            [(Role.Crumb, "Home"), (Role.Crumb, " › "), (Role.CrumbCurrent, "Search a › b")],
            Written(row));
    }

    /// <summary>
    ///     The whole row is one band, however many things are saying something on it: the crumbs, the separators, the
    ///     lead a long trail elides with, the room left over and the fetch mark. That is what makes the row read as
    ///     the frame rather than as the first line of what is being read — a band under half of it would read as a
    ///     highlight on the half it was under (#216).
    /// </summary>
    /// <remarks>
    ///     Asserted as the backgrounds the theme answers with, which is where a band lives in this client: a span
    ///     carries its own, and a row is banded by every role on it being banded. Against
    ///     <see cref="Themes.Dark" />, since <see cref="Themes.Plain" /> has one pair for everything and would pass
    ///     whatever the roles were.
    /// </remarks>
    [Fact]
    public void Breadcrumb_DrawsTheWholeRowOnOneBand()
    {
        var row = ChromeLines.Breadcrumb(
            ["Home", "Post by @ben@hachyderm.io", "@maria@mastodon.social following", "Keys"],
            dots: 1,
            ContentWidth);

        var band = Themes.Dark.For(Role.Crumb).Background;

        Assert.NotEqual(Themes.Dark.For(Role.Body).Background, band);
        Assert.All(row.Spans, span => Assert.Equal(band, Themes.Dark.For(span.Role).Background));

        // And every one of those roles is on the row, so the assertion above is not passing over an empty list.
        Assert.Contains(row.Spans, span => span.Role == Role.Crumb);
        Assert.Contains(row.Spans, span => span.Role == Role.CrumbCurrent);
        Assert.Contains(row.Spans, span => span.Role == Role.Loading);
    }

    /// <summary>What the row says, up to the padding the fetch mark is pushed to the right of.</summary>
    private static IReadOnlyList<(Role Role, string Text)> Written(Line row) =>
    [
        .. row.Spans
             .TakeWhile(span => !string.IsNullOrWhiteSpace(span.Text))
             .Select(span => (span.Role, span.Text)),
    ];
}
