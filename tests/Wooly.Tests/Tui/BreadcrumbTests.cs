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
    ///     get there stays <see cref="Role.Chrome" /> — separators included, which keep no role of their own.
    /// </summary>
    [Fact]
    public void Breadcrumb_DrawsTheCrumbYouAreStandingOnInItsOwnRole()
    {
        var row = ChromeLines.Breadcrumb(["Home", "Post by @ben", "@ben@hachyderm.io"], fetching: false, ContentWidth);

        var said = Written(row);

        Assert.Equal(
            [
                (Role.Chrome, "Home"),
                (Role.Chrome, " › "),
                (Role.Chrome, "Post by @ben"),
                (Role.Chrome, " › "),
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
        var row = ChromeLines.Breadcrumb(["Home"], fetching: false, ContentWidth);

        Assert.Equal([(Role.CrumbCurrent, "Home")], Written(row));
    }

    /// <summary>A trail that fits is drawn as it arrived, with nothing led in front of it.</summary>
    [Fact]
    public void Breadcrumb_LeadsATrailThatFitsWithNothing()
    {
        var row = ChromeLines.Breadcrumb(["Home", "Post by @ben"], fetching: false, ContentWidth);

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
            fetching: false,
            ContentWidth);

        Assert.StartsWith("… › ", row.Text, StringComparison.Ordinal);
        Assert.EndsWith("› Keys", row.Text.TrimEnd(), StringComparison.Ordinal);
        Assert.True(row.Width <= ContentWidth, $"The row is {row.Width} columns wide.");

        Assert.Contains(row.Spans, span => span is { Role: Role.CrumbCurrent, Text: "Keys" });
        Assert.Contains(row.Spans, span => span is { Role: Role.Chrome, Text: "… › " });
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
            fetching: false,
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

        var still = ChromeLines.Breadcrumb(trail, fetching: false, ContentWidth);
        var busy = ChromeLines.Breadcrumb(trail, fetching: true, ContentWidth);

        Assert.DoesNotContain("…", still.Text, StringComparison.Ordinal);
        Assert.StartsWith("… › ", busy.Text, StringComparison.Ordinal);
        Assert.Contains("fetching…", busy.Text);
        Assert.True(busy.Width <= ContentWidth, $"The row is {busy.Width} columns wide.");
    }

    /// <summary>
    ///     A crumb that has the separator inside it is still one crumb, because the row is drawn from the stack
    ///     rather than from the one string it reads as: <c>/</c> takes whatever is typed at it, so <c>a › b</c> is a
    ///     query somebody is entitled to search for.
    /// </summary>
    [Fact]
    public void Breadcrumb_DrawsACrumbThatHasTheSeparatorInItAsOneCrumb()
    {
        var row = ChromeLines.Breadcrumb(["Home", "Search a › b"], fetching: false, ContentWidth);

        Assert.Equal(
            [(Role.Chrome, "Home"), (Role.Chrome, " › "), (Role.CrumbCurrent, "Search a › b")],
            Written(row));
    }

    /// <summary>What the row says, up to the padding the fetch mark is pushed to the right of.</summary>
    private static IReadOnlyList<(Role Role, string Text)> Written(Line row) =>
    [
        .. row.Spans
             .TakeWhile(span => !string.IsNullOrWhiteSpace(span.Text))
             .Select(span => (span.Role, span.Text)),
    ];
}
