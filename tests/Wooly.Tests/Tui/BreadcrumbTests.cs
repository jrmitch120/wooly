using Wooly.Tui.Rendering;
using Wooly.Tui.Screens;
using Wooly.Tui.Theme;

namespace Wooly.Tests.Tui;

/// <summary>
///     The content panel's top edge, which is where the breadcrumb is drawn since ADR-0021: the trail as its title,
///     the crumb you are standing on told from the ones you walked through, a trail too long for the edge losing the
///     ancestors rather than the destination (#216), and the fetch mark at its end (#271). Where the panel sits in the
///     shell is <see cref="ContentPanelTests" />'s.
/// </summary>
/// <remarks>
///     Role selection and layout with no terminal in the room (ADR-0005, ADR-0014): what the edge is made of, in the
///     columns it has.
/// </remarks>
public class BreadcrumbTests
{
    /// <summary>The content panel's width at an 80-column terminal, its edges included: what the rail leaves.</summary>
    private const int PanelWidth = 62;

    /// <summary>
    ///     The crumb you are standing on takes the title's own role and every crumb you walked through to get there is
    ///     <see cref="Role.Muted" /> — separators included, which keep no role of their own.
    /// </summary>
    [Fact]
    public void Breadcrumb_DrawsTheCrumbYouAreStandingOnInTheTitlesRole()
    {
        var edge = ChromeLines.Breadcrumb(["Home", "Post by @ben", "@ben@hachyderm.io"], dots: 0, PanelWidth);

        Assert.Equal(
            [
                (Role.Muted, "Home"),
                (Role.Muted, " › "),
                (Role.Muted, "Post by @ben"),
                (Role.Muted, " › "),
                (Role.PanelTitle, "@ben@hachyderm.io"),
            ],
            Trail(edge));
    }

    /// <summary>
    ///     And a trail one crumb long is a trail you are standing at the end of, so that crumb is the current one —
    ///     the state every rail destination opens in.
    /// </summary>
    [Fact]
    public void Breadcrumb_DrawsAOneCrumbTrailAsTheCrumbYouAreStandingOn()
    {
        var edge = ChromeLines.Breadcrumb(["Home"], dots: 0, PanelWidth);

        Assert.Equal([(Role.PanelTitle, "Home")], Trail(edge));
    }

    /// <summary>
    ///     The trail is the panel's title: on its top edge, between the corners, with a space either side of it and
    ///     the content panel's edge in the active role, since it is the panel you are reading.
    /// </summary>
    [Fact]
    public void Breadcrumb_IsTheContentPanelsTopEdge()
    {
        var edge = ChromeLines.Breadcrumb(["Home", "Post by @ben"], dots: 0, PanelWidth);

        Assert.StartsWith("╭ Home › Post by @ben ─", edge.Text, StringComparison.Ordinal);
        Assert.EndsWith("─╮", edge.Text, StringComparison.Ordinal);
        Assert.Equal(PanelWidth, edge.Width);
        Assert.Equal(Role.PanelBorderActive, edge.Spans[0].Role);
        Assert.Equal(Role.PanelBorderActive, edge.Spans[^1].Role);
    }

    /// <summary>
    ///     A trail too long for the edge keeps the crumb you are standing on whole and gives up the ones you walked
    ///     through, saying so with <c>… › </c>.
    /// </summary>
    [Fact]
    public void Breadcrumb_ElidesADeepTrailFromTheLeft()
    {
        var edge = ChromeLines.Breadcrumb(
            [
                "Home",
                "Post by @ben@hachyderm.io",
                "@maria@mastodon.social",
                "@maria@mastodon.social following",
                "Keys",
            ],
            dots: 0,
            PanelWidth);

        Assert.StartsWith("╭ … › ", edge.Text, StringComparison.Ordinal);
        Assert.Contains("› Keys ─", edge.Text, StringComparison.Ordinal);
        Assert.Equal(PanelWidth, edge.Width);

        Assert.Contains(edge.Spans, span => span is { Role: Role.PanelTitle, Text: "Keys" });
        Assert.Contains(edge.Spans, span => span is { Role: Role.Muted, Text: "… › " });
    }

    /// <summary>
    ///     And a trail only two deep elides the same way, since a single crumb can be longer than the edge on its
    ///     own: what is dropped is whatever will not fit, and the destination the rail is already drawing is the
    ///     first to go.
    /// </summary>
    [Fact]
    public void Breadcrumb_ElidesAShallowTrailFromTheLeftToo()
    {
        var edge = ChromeLines.Breadcrumb(
            ["@maria@mastodon.social", "Post by @someone@instance.example"],
            dots: 0,
            PanelWidth);

        Assert.StartsWith("╭ … › ", edge.Text, StringComparison.Ordinal);
        Assert.Contains(
            edge.Spans,
            span => span is { Role: Role.PanelTitle } && span.Text.StartsWith("Post by", StringComparison.Ordinal));
        Assert.Equal(PanelWidth, edge.Width);
    }

    /// <summary>
    ///     The fetch mark never moves the trail: its columns are held for it whether or not it is drawn, so a trail
    ///     elides the same way at rest as on every tick of a fetch, and the crumb you are standing on stays put as the
    ///     mark comes and goes (#217, #271).
    /// </summary>
    [Fact]
    public void Breadcrumb_ElidesTheSameWayWithTheMarkAsWithout()
    {
        string[] trail =
        [
            "Home",
            "Post by @ben@hachyderm.io",
            "@maria@mastodon.social",
            "@maria@mastodon.social following",
            "Keys",
        ];

        var edges = new[] { 0, 1, 2, 3, 1 }.Select(dots => ChromeLines.Breadcrumb(trail, dots, PanelWidth)).ToList();
        var trails = edges.Select(Trail).Select(spans => string.Concat(spans.Select(span => span.Text))).Distinct();

        Assert.Equal("… › @maria@mastodon.social following › Keys", Assert.Single(trails));
        Assert.All(edges, edge => Assert.Equal(PanelWidth, edge.Width));
    }

    /// <summary>
    ///     A trail that fits beside the mark is drawn whole at rest and on every tick — the room held for the mark is
    ///     the room it would have taken anyway.
    /// </summary>
    [Fact]
    public void Breadcrumb_DrawsATrailThatFitsBesideTheMarkWhole()
    {
        string[] trail = ["Home", "Post by @ben@hachyderm.io"];

        Assert.All(
            new[] { 0, 1, 2, 3 },
            dots => Assert.StartsWith(
                "╭ Home › Post by @ben@hachyderm.io ─",
                ChromeLines.Breadcrumb(trail, dots, PanelWidth).Text,
                StringComparison.Ordinal));
    }

    /// <summary>
    ///     No dots is no mark at all — not the bare word, which on the edge whose job is to say <em>working</em> reads
    ///     as <em>finished</em>. It is what a fetch not yet a tick old draws, so one that lands in 80ms shows nothing
    ///     (#217).
    /// </summary>
    [Fact]
    public void Breadcrumb_DrawsNoMarkForNoDots()
    {
        var edge = ChromeLines.Breadcrumb(["Home"], dots: 0, PanelWidth);

        Assert.DoesNotContain("fetching", edge.Text, StringComparison.Ordinal);
        Assert.DoesNotContain(edge.Spans, span => span.Role == Role.Loading);
    }

    /// <summary>
    ///     One, two and three dots each take the same 11 columns at the end of the edge, the word at the left of them
    ///     and the dots growing rightward into the rest — so the word never moves, and nothing else on the edge does
    ///     either (#217).
    /// </summary>
    [Theory]
    [InlineData(1, "─ fetching.   ╮")]
    [InlineData(2, "─ fetching..  ╮")]
    [InlineData(3, "─ fetching... ╮")]
    public void Breadcrumb_DrawsEveryPhaseOfTheMarkInTheSameElevenColumnsAtTheEnd(int dots, string end)
    {
        string[] trail = ["Home", "Post by @ben@hachyderm.io"];

        var edge = ChromeLines.Breadcrumb(trail, dots, PanelWidth);
        var first = ChromeLines.Breadcrumb(trail, dots: 1, PanelWidth);

        Assert.Equal(PanelWidth, edge.Width);
        Assert.EndsWith(end, edge.Text, StringComparison.Ordinal);
        Assert.Equal(first.Text[..^13], edge.Text[..^13]);
        Assert.Contains(edge.Spans, span => span.Role == Role.Loading && span.Text.StartsWith("fetching", StringComparison.Ordinal));
    }

    /// <summary>
    ///     A crumb that has the separator inside it is still one crumb, because the edge is drawn from the stack
    ///     rather than from the one string it reads as: <c>/</c> takes whatever is typed at it, so <c>a › b</c> is a
    ///     query somebody is entitled to search for.
    /// </summary>
    [Fact]
    public void Breadcrumb_DrawsACrumbThatHasTheSeparatorInItAsOneCrumb()
    {
        var edge = ChromeLines.Breadcrumb(["Home", "Search a › b"], dots: 0, PanelWidth);

        Assert.Equal(
            [(Role.Muted, "Home"), (Role.Muted, " › "), (Role.PanelTitle, "Search a › b")],
            Trail(edge));
    }

    /// <summary>
    ///     On a panel too narrow to hold the mark, the edge is still the panel's width and still a frame: what does not
    ///     fit is left off rather than drawn past the corner.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(6)]
    [InlineData(16)]
    public void Breadcrumb_IsAsWideAsTheNarrowestPanel(int width)
    {
        var edge = ChromeLines.Breadcrumb(["Home", "Post by @ben"], dots: 2, width);

        Assert.Equal(width, edge.Width);
    }

    /// <summary>
    ///     A panel too narrow ever to draw the mark holds no room for it, so the crumb you are standing on is still the
    ///     title rather than nothing at all.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    public void Breadcrumb_KeepsTheCurrentCrumbOnAPanelTooNarrowForTheMark(int dots)
    {
        var edge = ChromeLines.Breadcrumb(["Home", "Post by @ben"], dots, width: 16);

        Assert.Equal("╭ Post by @ben ╮", edge.Text);
    }

    /// <summary>What the trail says: the spans between the space after the corner and the space before the rule.</summary>
    private static IReadOnlyList<(Role Role, string Text)> Trail(Line edge) =>
    [
        .. edge.Spans
               .Skip(2)
               .TakeWhile(span => span.Role is Role.Muted or Role.PanelTitle && span.Text != " ")
               .Select(span => (span.Role, span.Text)),
    ];
}
