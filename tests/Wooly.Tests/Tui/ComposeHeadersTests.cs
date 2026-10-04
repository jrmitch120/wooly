using Wooly.Core.Posts;
using Wooly.Tests.Fakes;
using Wooly.Tui.Rendering;
using Wooly.Tui.Screens;
using Wooly.Tui.Theme;

namespace Wooly.Tests.Tui;

/// <summary>
///     Compose laid out as a mail client's compose (#317): a short block of right-aligned headers — who the post is
///     from, what it answers, and the warning under the feed's own <c>⚠</c> — a hairline, the post, and a hairline at
///     the foot. The same on a fresh post, a reply and an edit, but for the reply header.
/// </summary>
public class ComposeHeadersTests
{
    /// <summary>The content panel's inside at an 80 × 20 terminal.</summary>
    private const int Width = 60;

    private const int Height = 17;

    private static readonly string Rule = ComposeRows.Hairline(Width);

    private static readonly Post Bens = APost.With(id: "220", account: "ben@hachyderm.io");

    private static readonly Post Mine = APost.With(id: "110", account: "jeff@mastodon.social");

    /// <summary>
    ///     A fresh post and an edit read the same: a blank, From, the warning, a hairline, a blank, the editor's rows, a
    ///     hairline and the count.
    /// </summary>
    [Theory]
    [InlineData(ComposeFor.Post)]
    [InlineData(ComposeFor.Edit)]
    public async Task APostAndAnEditDrawTheFromAndWarningHeadersBetweenTwoHairlines(ComposeFor opening)
    {
        var compose = await Opening(opening, Mine);
        var rows = Texts(compose);

        Assert.Equal(
            [
                string.Empty,
                "  From  @jeff · mastodon.social",
                ComposeRows.NoWarning,
                Rule,
                string.Empty,
            ],
            rows.Take(5));
        Assert.All(rows.Skip(5).Take(Height - 7), row => Assert.Equal(string.Empty, row));
        Assert.Equal(Rule, rows[Height - 2]);
        Assert.EndsWith(" / 500", rows[Height - 1], StringComparison.Ordinal);
        Assert.Equal(Height, rows.Count);
    }

    /// <summary>A reply adds its own header, marked with the feed's reply mark, and the quote under it.</summary>
    [Fact]
    public async Task AReplyDrawsTheReplyHeaderAndItsQuoteBetweenFromAndTheWarning()
    {
        var compose = await Opening(ComposeFor.Reply, Bens);

        Assert.Equal(
            [
                string.Empty,
                "  From  @jeff · mastodon.social",
                "     ↳  answering @ben@hachyderm.io",
                "        │ Hello world",
                ComposeRows.NoWarning,
                Rule,
                string.Empty,
            ],
            Texts(compose).Take(7));
    }

    /// <summary>A reply to the profile's own post continues it rather than answering it, as the feed says.</summary>
    [Fact]
    public async Task ASelfReplyIsAContinuation()
    {
        var compose = await Opening(ComposeFor.Reply, Mine);

        Assert.Equal("     ↳  continuing", Texts(compose)[2]);
    }

    /// <summary>A From too long for its row is cut at the padding, the instance before the handle.</summary>
    [Fact]
    public async Task FromIsCutAtThePadding()
    {
        var compose = await Opening(ComposeFor.Post, Mine);
        var from = compose.Lines(new Drawing(20, AShell.Now, Height: Height))[1];

        Assert.Equal("  From  @jeff · m…", from.Text);
    }

    /// <summary>The From header: the handle as a byline's, the instance muted after it.</summary>
    [Fact]
    public async Task FromIsTheHandleThenTheInstance()
    {
        var compose = await Opening(ComposeFor.Post, Mine);
        var from = Lines(compose)[1];

        Assert.Equal(Role.Muted, Assert.Single(from.Spans, span => span.Text == "From").Role);
        Assert.Equal(Role.BylineHandle, Assert.Single(from.Spans, span => span.Text == "@jeff").Role);
        Assert.Equal(Role.Muted, Assert.Single(from.Spans, span => span.Text == " · mastodon.social").Role);
    }

    /// <summary>
    ///     Three rows of what was said, blank ones not among them (#141), behind a gutter in the panel's border and
    ///     muted.
    /// </summary>
    [Fact]
    public async Task TheQuoteSkipsBlankRowsAndStopsAtThree()
    {
        var paragraphs = APost.With(
            id: "220",
            account: "ben@hachyderm.io",
            content: "The first thing said.\n\nThe second thing said.\n\nThe third thing said.\n\nThe fourth.");

        var compose = await Opening(ComposeFor.Reply, paragraphs);
        var lines = Lines(compose);

        Assert.Equal(
            [
                "        │ The first thing said.",
                "        │ The second thing said.",
                "        │ The third thing said.",
                ComposeRows.NoWarning,
            ],
            lines.Skip(3).Take(4).Select(line => line.Text));
        Assert.All(
            lines.Skip(3).Take(3),
            line =>
            {
                Assert.Equal(Role.PanelBorder, Assert.Single(line.Spans, span => span.Text == "│ ").Role);
                Assert.Equal(Role.Muted, line.Spans[^1].Role);
            });
    }

    /// <summary>Both hairlines are the panel's border.</summary>
    [Fact]
    public async Task TheHairlinesAreThePanelsBorder()
    {
        var compose = await Opening(ComposeFor.Post, Mine);
        var lines = Lines(compose);

        Assert.All(
            new[] { lines[3], lines[^2] },
            line => Assert.Equal(Role.PanelBorder, Assert.Single(line.Spans, span => span.Text.Trim().Length > 0).Role));
    }

    /// <summary>With no warning the mark is dim and the row says how to add one.</summary>
    [Fact]
    public async Task TheMarkIsDimWithNoWarning()
    {
        var compose = await Opening(ComposeFor.Post, Mine);
        var warning = Lines(compose)[2];

        Assert.DoesNotContain(warning.Spans, span => span.Role == Role.ContentWarning);
        Assert.Equal(Role.Muted, Assert.Single(warning.Spans, span => span.Text.Contains('⚠')).Role);
    }

    /// <summary>With one written, the mark and the warning are both in the warning's role.</summary>
    [Fact]
    public async Task TheMarkIsLitWithAWarningWritten()
    {
        var warned = APost.With(id: "220", account: "ben@hachyderm.io", contentWarning: "spoilers");

        var compose = await Opening(ComposeFor.Reply, warned);
        var warning = Lines(compose)[4];

        Assert.Equal(ComposeRows.Warning("spoilers"), warning.Text);
        Assert.Equal(Role.ContentWarning, Assert.Single(warning.Spans, span => span.Text.Contains('⚠')).Role);
        Assert.Equal(Role.ContentWarning, Assert.Single(warning.Spans, span => span.Text == "spoilers").Role);
    }

    /// <summary>
    ///     And lit while one is being written, even before a letter is in it — with a hint at what goes there in place
    ///     of how to get there.
    /// </summary>
    [Fact]
    public async Task TheMarkIsLitWhileAWarningIsBeingWritten()
    {
        var compose = await Opening(ComposeFor.Post, Mine);

        compose.WriteTheWarning();

        var warning = Lines(compose)[2];

        Assert.Equal(ComposeRows.Warning("▌say what it's about"), warning.Text);
        Assert.Equal(Role.ContentWarning, Assert.Single(warning.Spans, span => span.Text.Contains('⚠')).Role);
        Assert.Equal(Role.Muted, Assert.Single(warning.Spans, span => span.Text == "say what it's about").Role);
    }

    /// <summary>The editor starts under the headers, two columns in from either side, and runs to the foot's hairline.</summary>
    [Theory]
    [InlineData(ComposeFor.Post, 5)]
    [InlineData(ComposeFor.Edit, 5)]
    [InlineData(ComposeFor.Reply, 7)]
    public async Task TheEditorStartsBelowTheHeaders(ComposeFor opening, int top)
    {
        var compose = await Opening(opening, Mine);

        Assert.Equal(
            new System.Drawing.Rectangle(2, top, Width - 4, Height - top - 2),
            compose.EditorAt(new System.Drawing.Size(Width, Height)));
    }

    /// <summary>
    ///     On a short terminal the quote gives way first, then the blanks, then the reply header — and the warning row
    ///     and three rows of editor are kept however short it gets.
    /// </summary>
    [Theory]
    [InlineData(12, 7)]
    [InlineData(11, 6)]
    [InlineData(9, 4)]
    [InlineData(8, 3)]
    [InlineData(4, 1)]
    public async Task OnAShortTerminalTheQuoteGivesWayBeforeTheWarningAndTheEditor(int height, int top)
    {
        var compose = await Opening(ComposeFor.Reply, Bens);
        var lines = compose.Lines(new Drawing(Width, AShell.Now, Height: height));

        Assert.Equal(
            new System.Drawing.Rectangle(2, top, Width - 4, 3),
            compose.EditorAt(new System.Drawing.Size(Width, height)));
        Assert.Equal(height, lines.Count);
        Assert.Contains(lines.Take(top), line => line.Text == ComposeRows.NoWarning);
        Assert.Equal(height >= 9, lines.Any(line => line.Text.Contains("↳", StringComparison.Ordinal)));
        Assert.Equal(height >= 12, lines.Any(line => line.Text.Contains("│ Hello", StringComparison.Ordinal)));
    }

    private static IReadOnlyList<Line> Lines(ComposeScreen compose) =>
        compose.Lines(new Drawing(Width, AShell.Now, Height: Height));

    private static IReadOnlyList<string> Texts(ComposeScreen compose) => [.. Lines(compose).Select(line => line.Text)];

    private static async Task<ComposeScreen> Opening(ComposeFor opening, Post post)
    {
        var shell = await new AShell { Timelines = FakeTimelineReader.Holding(post) }.Opened();

        return ComposeRows.Open(shell, opening);
    }
}
