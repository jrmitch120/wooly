using Wooly.Tui.Rendering;
using Wooly.Tui.Screens;
using Wooly.Tui.Theme;

namespace Wooly.Tests.Tui;

/// <summary>
///     The content panel's top edge, which is where the breadcrumb is drawn since ADR-0021: the trail as its title,
///     the crumb you are standing on told from the ones you walked through, a trail too long for the edge losing the
///     ancestors rather than the destination (#216), and the fetch mark straight after it (#271, #281). Where the panel sits in the
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
        var edge = ChromeLines.Breadcrumb(["Home", "Post by @ben", "@ben@hachyderm.io"], frame: 0, PanelWidth);

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
        var edge = ChromeLines.Breadcrumb(["Home"], frame: 0, PanelWidth);

        Assert.Equal([(Role.PanelTitle, "Home")], Trail(edge));
    }

    /// <summary>
    ///     The trail is the panel's title: on its top edge, between the corners, with a space either side of it and
    ///     the content panel's edge in the active role, since it is the panel you are reading.
    /// </summary>
    [Fact]
    public void Breadcrumb_IsTheContentPanelsTopEdge()
    {
        var edge = ChromeLines.Breadcrumb(["Home", "Post by @ben"], frame: 0, PanelWidth);

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
            frame: 0,
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
            frame: 0,
            PanelWidth);

        Assert.StartsWith("╭ … › ", edge.Text, StringComparison.Ordinal);
        Assert.Contains(
            edge.Spans,
            span => span is { Role: Role.PanelTitle } && span.Text.StartsWith("Post by", StringComparison.Ordinal));
        Assert.Equal(PanelWidth, edge.Width);
    }

    /// <summary>
    ///     The fetch mark never moves the trail: its columns are held for it whether or not it is drawn, so a trail
    ///     elides the same way at rest as on every frame of a fetch, and the crumb you are standing on stays put as the
    ///     spinner comes and goes (#217, #271, #281).
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

        var edges = Enumerable.Range(0, ChromeLines.Frames + 2)
                              .Append(0)
                              .Select(frame => ChromeLines.Breadcrumb(trail, frame, PanelWidth))
                              .ToList();
        var trails = edges.Select(Trail).Distinct(new SpansComparer());

        Assert.Equal(
            "… › @maria@mastodon.social following › Keys",
            string.Concat(Assert.Single(trails).Select(span => span.Text)));
        Assert.All(edges, edge => Assert.Equal(PanelWidth, edge.Width));
    }

    /// <summary>
    ///     A trail long enough to elide is followed by the spinner all the same, after the crumb you are standing on —
    ///     and one that fills its room puts the spinner at the far end of the edge, where nothing is left to move it.
    /// </summary>
    [Fact]
    public void Breadcrumb_DrawsTheSpinnerAfterAnElidedTrail()
    {
        var elided = ChromeLines.Breadcrumb(
            ["Home", "Post by @ben@hachyderm.io", "@maria@mastodon.social", "@maria@mastodon.social following", "Keys"],
            frame: 1,
            PanelWidth);
        var filled = ChromeLines.Breadcrumb(["Home", new string('a', 70)], frame: 1, PanelWidth);

        Assert.Contains(" › Keys  ⠋ ─", elided.Text, StringComparison.Ordinal);
        Assert.EndsWith("a…  ⠋ ╮", filled.Text, StringComparison.Ordinal);
        Assert.Equal(PanelWidth, filled.Width);
    }

    /// <summary>
    ///     A trail that fits beside the mark is drawn whole at rest and on every frame — the room held for the mark is
    ///     the room it would have taken anyway — and the spinner follows it, beside the crumb the fetch is about to
    ///     replace, rather than sitting at the far end of the edge.
    /// </summary>
    [Fact]
    public void Breadcrumb_DrawsTheSpinnerStraightAfterATrailThatFits()
    {
        string[] trail = ["Home", "Post by @ben@hachyderm.io"];

        Assert.StartsWith(
            "╭ Home › Post by @ben@hachyderm.io ─",
            ChromeLines.Breadcrumb(trail, frame: 0, PanelWidth).Text,
            StringComparison.Ordinal);
        Assert.StartsWith(
            "╭ Home › Post by @ben@hachyderm.io  ⠙ ─",
            ChromeLines.Breadcrumb(trail, frame: 2, PanelWidth).Text,
            StringComparison.Ordinal);
    }

    /// <summary>
    ///     No frame is no mark at all. It is what a fetch not yet a tick old draws, so one that lands in 80ms shows
    ///     nothing (#217) — and with colour off, the spinner's presence alone is what tells a fetch in flight from none.
    /// </summary>
    [Fact]
    public void Breadcrumb_DrawsNoMarkForFrameNought()
    {
        var edge = ChromeLines.Breadcrumb(["Home"], frame: 0, PanelWidth);

        Assert.StartsWith("╭ Home ─", edge.Text, StringComparison.Ordinal);
        Assert.DoesNotContain(edge.Text, character => "⠋⠙⠹⠸⠼⠴⠦⠧⠇⠏".Contains(character));
        Assert.DoesNotContain(edge.Spans, span => span.Role == Role.Loading);
    }

    /// <summary>
    ///     Each frame is the title's last glyph, two spaces after the trail, in <see cref="Role.Loading" />, and the
    ///     tenth is followed by the first again.
    /// </summary>
    [Theory]
    [InlineData(1, "⠋")]
    [InlineData(2, "⠙")]
    [InlineData(3, "⠹")]
    [InlineData(4, "⠸")]
    [InlineData(5, "⠼")]
    [InlineData(6, "⠴")]
    [InlineData(7, "⠦")]
    [InlineData(8, "⠧")]
    [InlineData(9, "⠇")]
    [InlineData(10, "⠏")]
    [InlineData(11, "⠋")]
    public void Breadcrumb_DrawsEachFrameOfTheSpinnerAfterTheTrail(int frame, string glyph)
    {
        var edge = ChromeLines.Breadcrumb(["Home"], frame, PanelWidth);

        Assert.StartsWith($"╭ Home  {glyph} ─", edge.Text, StringComparison.Ordinal);
        Assert.Equal(PanelWidth, edge.Width);
        Assert.Contains(edge.Spans, span => span is { Role: Role.Loading } && span.Text == $"  {glyph}");
    }

    /// <summary>
    ///     The mark holds three columns — two spaces and the glyph — so at 80 columns the trail has the panel's title
    ///     room less three: 55 columns, where the 11-column <c>fetching...</c> left it 44.
    /// </summary>
    [Fact]
    public void Breadcrumb_HoldsThreeColumnsForTheMarkAt80Columns()
    {
        var fits = new string('a', 55);
        var over = new string('a', 56);

        Assert.Equal([(Role.PanelTitle, fits)], Trail(ChromeLines.Breadcrumb([fits], frame: 0, PanelWidth)));
        Assert.NotEqual([(Role.PanelTitle, over)], Trail(ChromeLines.Breadcrumb([over], frame: 0, PanelWidth)));
    }

    /// <summary>
    ///     A crumb that has the separator inside it is still one crumb, because the edge is drawn from the stack
    ///     rather than from the one string it reads as: <c>/</c> takes whatever is typed at it, so <c>a › b</c> is a
    ///     query somebody is entitled to search for.
    /// </summary>
    [Fact]
    public void Breadcrumb_DrawsACrumbThatHasTheSeparatorInItAsOneCrumb()
    {
        var edge = ChromeLines.Breadcrumb(["Home", "Search a › b"], frame: 0, PanelWidth);

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
        var edge = ChromeLines.Breadcrumb(["Home", "Post by @ben"], frame: 2, width);

        Assert.Equal(width, edge.Width);
    }

    /// <summary>
    ///     A panel too narrow ever to draw the mark beside a column of trail holds no room for it, so the crumb you are
    ///     standing on is still the title rather than nothing at all, and is the same with a fetch in flight as without.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    public void Breadcrumb_KeepsTheCurrentCrumbOnAPanelTooNarrowForTheMark(int frame)
    {
        var edge = ChromeLines.Breadcrumb(["Home", "Post by @ben"], frame, width: 7);

        Assert.Equal("╭ Po… ╮", edge.Text);
        Assert.DoesNotContain(edge.Spans, span => span.Role == Role.Loading);
    }

    /// <summary>What the trail says: the spans between the space after the corner and the space before the rule.</summary>
    private static IReadOnlyList<(Role Role, string Text)> Trail(Line edge) =>
    [
        .. edge.Spans
               .Skip(2)
               .TakeWhile(span => span.Role is Role.Muted or Role.PanelTitle && span.Text != " ")
               .Select(span => (span.Role, span.Text)),
    ];

    /// <summary>Two trails alike span for span, role and text.</summary>
    private sealed class SpansComparer : IEqualityComparer<IReadOnlyList<(Role Role, string Text)>>
    {
        public bool Equals(IReadOnlyList<(Role Role, string Text)>? x, IReadOnlyList<(Role Role, string Text)>? y) =>
            x is not null && y is not null && x.SequenceEqual(y);

        public int GetHashCode(IReadOnlyList<(Role Role, string Text)> obj) => obj.Count;
    }
}
