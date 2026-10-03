using Wooly.Tui.Media;
using Wooly.Tui.Rendering;
using Wooly.Tui.Theme;

namespace Wooly.Tests.Tui;

/// <summary>
///     A panel's frame as rows (#270, ADR-0021): a rounded edge in <c>panel-border</c> or <c>panel-border-active</c>,
///     the title on its top edge in <c>panel-title</c> with no <c>┤ ├</c> round it, and the rows inside inset by it.
/// </summary>
public class PanelTests
{
    [Fact]
    public void AFrameDrawsAsRowsOfItsWidthAndHeight()
    {
        var lines = Panel.Framed("Inbox", [Line.Of("Notifications", Role.Rail)], width: 18, height: 4, active: false);

        Assert.Equal(
            [
                "╭ Inbox ─────────╮",
                "│Notifications   │",
                "│                │",
                "╰────────────────╯",
            ],
            lines.Select(line => line.Text));
    }

    [Fact]
    public void EveryRowIsAsWideAsThePanel()
    {
        var lines = Panel.Framed(
            "Timelines",
            [Line.Of("Home", Role.Rail), Line.Of("a row far wider than the panel it is in", Role.Body)],
            width: 18,
            height: 5,
            active: true);

        Assert.All(lines, line => Assert.Equal(18, line.Width));
    }

    [Fact]
    public void ARowWiderThanTheInsideIsCutToIt()
    {
        var lines = Panel.Framed("You", [Line.Of("@somebody.with.a.long.name", Role.Rail)], 12, 3, active: false);

        Assert.Equal("│@somebody.│", lines[1].Text);
    }

    [Fact]
    public void RowsPastTheFootAreNotDrawn()
    {
        var lines = Panel.Framed("You", [Line.Of("one", Role.Rail), Line.Of("two", Role.Rail)], 10, 3, active: false);

        Assert.Equal(["╭ You ───╮", "│one     │", "╰────────╯"], lines.Select(line => line.Text));
    }

    [Fact]
    public void ATitleTooLongIsClippedFromItsEnd()
    {
        var top = Panel.Framed("Direct messages", [], width: 12, height: 2, active: false)[0];

        Assert.Equal("╭ Direct m ╮", top.Text);
    }

    [Fact]
    public void ATitleIsClippedByTheColumnsItIsDrawnIn()
    {
        // ドット is three characters and six columns: a cut by characters would draw the edge two columns too far right.
        var top = Panel.Framed("ドット絵", [], width: 10, height: 2, active: false)[0];

        Assert.Equal("╭ ドット ╮", top.Text);
        Assert.Equal(10, top.Width);
    }

    [Fact]
    public void APanelTooNarrowForATitleIsAllEdge()
    {
        var lines = Panel.Framed("Inbox", [], width: 4, height: 2, active: false);

        Assert.Equal(["╭──╮", "╰──╯"], lines.Select(line => line.Text));
    }

    [Fact]
    public void AFrameOfOneRowIsItsTopEdge()
    {
        Assert.Equal(["╭ API ─╮"], Panel.Framed("API", [], 8, 1, active: false).Select(line => line.Text));
    }

    [Fact]
    public void AFrameOfNoRoomIsNoRows()
    {
        Assert.Empty(Panel.Framed("API", [], 8, 0, active: false));
        Assert.All(Panel.Framed("API", [], 0, 3, active: false), line => Assert.Equal(string.Empty, line.Text));
    }

    [Theory]
    [InlineData(false, Role.PanelBorder)]
    [InlineData(true, Role.PanelBorderActive)]
    public void TheEdgeIsInTheBorderRoleAndTheTitleInTheTitleRole(bool active, Role edge)
    {
        var lines = Panel.Framed("Inbox", [Line.Of("Notifications", Role.Rail)], 18, 3, active);

        var title = Assert.Single(lines[0].Spans, span => span.Text.Contains("Inbox", StringComparison.Ordinal));

        // The spaces either side are the title's too, so a theme giving the title a background pads it rather than
        // stopping hard against its letters.
        Assert.Equal(Role.PanelTitle, title.Role);
        Assert.All(
            lines[0].Spans,
            span => Assert.Equal(span.Text.Trim().Length == 0 || span == title ? Role.PanelTitle : edge, span.Role));
        Assert.Equal(edge, lines[1].Spans[0].Role);
        Assert.Equal(edge, lines[1].Spans[^1].Role);
        Assert.Equal(edge, lines[2].Role);
    }

    [Fact]
    public void ARowInsideKeepsItsOwnRoles()
    {
        var lines = Panel.Framed("Inbox", [Line.Of(new Span("Notifications", Role.Rail), new Span("4", Role.RailUnread))], 18, 3, false);

        Assert.Contains(new Span("4", Role.RailUnread), lines[1].Spans);
    }

    [Fact]
    public void ATitleOfSpansKeepsTheirRoles()
    {
        var top = Panel.Top([new Span("Home", Role.PanelTitle), new Span(" ✳", Role.Spinner)], 24, active: true);

        Assert.Contains(new Span(" ✳", Role.Spinner), top.Spans);
        Assert.Equal(24, top.Width);
    }

    [Fact]
    public void APictureInsideMovesInByTheEdge()
    {
        var drawn = new Drawn("m1", "https://example.com/m1.png");
        var row = Line.Of("▒▒▒▒", Role.Media) with { Insets = [new Inset(drawn, Column: 0, Columns: 4, Rows: 2)] };

        var lines = Panel.Framed("Home", [row], 18, 4, active: false);

        Assert.Equal(1, Assert.Single(lines[1].Insets).Column);
    }

    [Fact]
    public void WhatARowIsPartOfIsKept()
    {
        var row = Line.Of("Home", Role.Rail).PartOf(2).Heading();

        var inside = Panel.Framed("Timelines", [row], 18, 3, active: false)[1];

        Assert.Equal(2, inside.Item);
        Assert.True(inside.Heads);
    }
}
