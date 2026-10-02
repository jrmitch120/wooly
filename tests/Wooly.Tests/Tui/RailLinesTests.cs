using Wooly.Core.Http;
using Wooly.Tests.Fakes;
using Wooly.Tui.Rendering;
using Wooly.Tui.Screens;
using Wooly.Tui.Shell;
using Wooly.Tui.Theme;

namespace Wooly.Tests.Tui;

/// <summary>
///     The stacked rail (#272, ADR-0021): the four rail groups, each a panel titled with its name, the API budget in a
///     fifth at the foot, and the rail stepping down to compact and then scrolled on a short terminal. In colour the
///     rail's marks are bands; without, the one-column <c>▶</c>/<c>▷</c> #78 settled.
/// </summary>
public class RailLinesTests
{
    private static readonly RateLimitQuota Plenty = new(250, 300, null);

    private static (Rail Rail, FakeShellHost Host) TheTen(int unreadNotifications = 0)
    {
        var host = new FakeShellHost();
        var rail = new Rail(
            [
                new Destination(DestinationKind.Home, "Home"),
                new Destination(DestinationKind.Local, "Local"),
                new Destination(DestinationKind.Federated, "Federated"),
                new Destination(DestinationKind.Hashtag, "Hashtag"),
                new Destination(DestinationKind.Discover, "Discover"),
                new Destination(DestinationKind.Search, "Search"),
                new Destination(DestinationKind.Notifications, "Notifications") { Unread = unreadNotifications },
                new Destination(DestinationKind.Messages, "Direct messages"),
                new Destination(DestinationKind.Requests, "Follow requests"),
                new Destination(DestinationKind.Profile, "@jeff"),
            ],
            host,
            TimeSpan.FromMilliseconds(250));

        return (rail, host);
    }

    private static string[] Texts(IEnumerable<Line> lines) => [.. lines.Select(line => line.Text)];

    /// <summary>The row whose text, inside whatever frame it has, starts with <paramref name="label" />.</summary>
    private static Line Row(IReadOnlyList<Line> lines, string label) =>
        lines.Single(line => line.Text.TrimStart('│', ' ', '▶', '▷').StartsWith(label, StringComparison.Ordinal));

    /// <summary>
    ///     The rail needs 22 rows framed and 15 compact, so the five heights either side of those two lines step down
    ///     framed, framed, compact, compact and scrolled (docs/tui-shell.md).
    /// </summary>
    [Theory]
    [InlineData(23, "framed")]
    [InlineData(22, "framed")]
    [InlineData(21, "compact")]
    [InlineData(15, "compact")]
    [InlineData(12, "scrolled")]
    public void Of_StepsDownAsTheRailShortens(int height, string expected)
    {
        var (rail, _) = TheTen();

        var drawn = Texts(RailLines.Of(rail, Plenty, height));

        var framed = drawn.Count(text => text.StartsWith('╭'));
        var labels = new[] { "Home", "Local", "Federated", "Hashtag", "Discover", "Search", "Notifications", "Direct messages", "Follow requests", "@jeff" };
        var shown = labels.Count(label => drawn.Any(text => text.TrimStart('│', ' ', '▶', '▷').StartsWith(label, StringComparison.Ordinal)));

        var mode = framed == 5 ? "framed" : framed == 0 && shown == 10 ? "compact" : framed == 0 ? "scrolled" : "mixed";

        Assert.Equal(expected, mode);
        Assert.Equal(height, drawn.Length);
        Assert.All(RailLines.Of(rail, Plenty, height), line => Assert.Equal(RailLines.Width, line.Width));
    }

    /// <summary>
    ///     Framed, the rail is the four groups top to bottom, each titled with its name and holding its destinations in
    ///     the rail's order, and the API panel held at the foot however tall the terminal is.
    /// </summary>
    [Fact]
    public void Of_FramesEachGroupWithItsNameAndHoldsTheApiPanelAtTheFoot()
    {
        var (rail, _) = TheTen();

        var drawn = Texts(RailLines.Of(rail, Plenty, 30));

        Assert.Equal(
            [
                "╭ Timelines ─────╮",
                "│▶ Home          │",
                "│  Local         │",
                "│  Federated     │",
                "│  Hashtag       │",
                "╰────────────────╯",
                "╭ Explore ───────╮",
                "│  Discover      │",
                "│  Search        │",
                "╰────────────────╯",
                "╭ Inbox ─────────╮",
                "│  Notifications │",
                "│  Direct messag…│",
                "│  Follow reques…│",
                "╰────────────────╯",
                "╭ You ───────────╮",
                "│  @jeff         │",
                "╰────────────────╯",
            ],
            drawn.Take(18));

        Assert.Equal("╭ API ───────────╮", drawn[^3]);
        Assert.Equal("╰────────────────╯", drawn[^1]);
        Assert.All(drawn[18..^3], text => Assert.Equal(new string(' ', RailLines.Width), text));
    }

    /// <summary>
    ///     The group holding the cursor is the one framed in the active role, and it moves the moment the cursor does —
    ///     on the press, not when the settle window closes — so a jump into another group says at once where it landed.
    ///     The selection's band stays where it was until the window closes, in a frame no longer lit.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Of_LightsTheGroupHoldingTheCursorAndMovesItWithTheCursor(bool coloured)
    {
        var (rail, host) = TheTen();

        Role EdgeOf(IReadOnlyList<Line> lines, string group) =>
            lines.Single(line => line.Text.StartsWith($"╭ {group} ", StringComparison.Ordinal)).Spans[0].Role;

        var atRest = RailLines.Of(rail, Plenty, 23, coloured: coloured);

        Assert.Equal(Role.PanelBorderActive, EdgeOf(atRest, "Timelines"));
        Assert.Equal(Role.PanelBorder, EdgeOf(atRest, "Inbox"));
        Assert.Equal(Role.PanelBorder, EdgeOf(atRest, "API"));

        rail.Step(6);

        var tabbing = RailLines.Of(rail, Plenty, 23, coloured: coloured);

        Assert.Equal(Role.PanelBorder, EdgeOf(tabbing, "Timelines"));
        Assert.Equal(Role.PanelBorderActive, EdgeOf(tabbing, "Inbox"));
        Assert.Equal(Role.PanelBorderActive, Row(tabbing, "Notifications").Spans[0].Role);
        Assert.Equal(Role.RailCurrent, Row(tabbing, "Home").Spans[1].Role);

        host.Settle();

        var settled = RailLines.Of(rail, Plenty, 23, coloured: coloured);

        Assert.Equal(Role.PanelBorder, EdgeOf(settled, "Timelines"));
        Assert.Equal(Role.PanelBorderActive, EdgeOf(settled, "Inbox"));
    }

    /// <summary>
    ///     In colour no rail row carries a mark: at rest the selected row is banded on <c>rail-current</c>, label and
    ///     all, and nothing is on <c>rail-cursor</c>.
    /// </summary>
    [Theory]
    [InlineData(23)]
    [InlineData(18)]
    [InlineData(12)]
    public void Of_InColour_BandsTheSelectedRowAtRestWithNoMark(int height)
    {
        var (rail, _) = TheTen();

        var drawn = RailLines.Of(rail, Plenty, height, coloured: true);

        Assert.All(drawn, line => Assert.DoesNotContain('▶', line.Text));
        Assert.All(drawn, line => Assert.DoesNotContain('▷', line.Text));
        Assert.Contains(Row(drawn, "Home").Spans, span => span is { Role: Role.RailCurrent, Text: var text } && text.Contains("Home", StringComparison.Ordinal));
        Assert.DoesNotContain(drawn, line => line.Has(Role.RailCursor));
    }

    /// <summary>
    ///     Mid-tab in colour the cursor's row is on <c>rail-cursor</c>, a lighter band, and the selected row keeps
    ///     <c>rail-current</c> until the settle window closes — still with no mark on either.
    /// </summary>
    [Theory]
    [InlineData(23)]
    [InlineData(18)]
    public void Of_InColour_BandsTheCursorsRowApartWhileTheSelectionLags(int height)
    {
        var (rail, _) = TheTen();

        rail.Step(2);

        var drawn = RailLines.Of(rail, Plenty, height, coloured: true);

        Assert.All(drawn, line => Assert.DoesNotContain('▶', line.Text));
        Assert.All(drawn, line => Assert.DoesNotContain('▷', line.Text));
        Assert.Contains(Row(drawn, "Federated").Spans, span => span is { Role: Role.RailCursor } && span.Text.Contains("Federated", StringComparison.Ordinal));
        Assert.Contains(Row(drawn, "Home").Spans, span => span is { Role: Role.RailCurrent } && span.Text.Contains("Home", StringComparison.Ordinal));
        Assert.False(Row(drawn, "Local").Has(Role.RailCursor) || Row(drawn, "Local").Has(Role.RailCurrent));
    }

    /// <summary>
    ///     Without colour the one mark column is exactly as #78 left it: <c>▶</c> on the cursor's row, <c>▷</c> on the
    ///     selected row only while the two differ, a blank otherwise — and no row on <c>rail-cursor</c>.
    /// </summary>
    [Theory]
    [InlineData(23)]
    [InlineData(18)]
    [InlineData(12)]
    public void Of_WithoutColour_MarksTheCursorFilledAndTheLaggingSelectionHollow(int height)
    {
        var (rail, _) = TheTen();

        var atRest = RailLines.Of(rail, Plenty, height);

        Assert.StartsWith("▶ Home", Row(atRest, "Home").Text.TrimStart('│'), StringComparison.Ordinal);
        Assert.Single(atRest, line => line.Text.Contains('▶', StringComparison.Ordinal));
        Assert.DoesNotContain(atRest, line => line.Text.Contains('▷', StringComparison.Ordinal));

        rail.Step(1);

        var tabbing = RailLines.Of(rail, Plenty, height);

        Assert.StartsWith("▷ Home", Row(tabbing, "Home").Text.TrimStart('│'), StringComparison.Ordinal);
        Assert.StartsWith("▶ Local", Row(tabbing, "Local").Text.TrimStart('│'), StringComparison.Ordinal);
        Assert.StartsWith("  Federated", Row(tabbing, "Federated").Text.TrimStart('│'), StringComparison.Ordinal);
        Assert.DoesNotContain(tabbing, line => line.Has(Role.RailCursor));
    }

    /// <summary>Each destination keeps its unread count, at the end of its row and in its own role, in every mode.</summary>
    [Theory]
    [InlineData(23, true)]
    [InlineData(23, false)]
    [InlineData(18, true)]
    [InlineData(18, false)]
    public void Of_KeepsEachDestinationsUnreadCount(int height, bool coloured)
    {
        var (rail, _) = TheTen(unreadNotifications: 4);

        var row = Row(RailLines.Of(rail, Plenty, height, coloured: coloured), "Notifications");

        Assert.EndsWith("4", row.Text.TrimEnd('│'), StringComparison.Ordinal);
        Assert.Contains(row.Spans, span => span is { Role: Role.RailUnread, Text: "4" });
    }

    /// <summary>
    ///     The API panel holds the budget as a gauge: filled cells in <c>gauge</c>, empty ones in <c>gauge-empty</c>,
    ///     and the percentage left at its end.
    /// </summary>
    [Fact]
    public void Of_DrawsTheBudgetAsAGaugeWithAPercentage()
    {
        var (rail, _) = TheTen();

        var gauge = RailLines.Of(rail, Plenty, 23)[^2];

        Assert.Equal("│ ████████░░  83%│", gauge.Text);
        Assert.Contains(gauge.Spans, span => span is { Role: Role.Gauge, Text: "████████" });
        Assert.Contains(gauge.Spans, span => span is { Role: Role.GaugeEmpty, Text: "░░" });
    }

    /// <summary>Nearly spent, the gauge's filled cells and its percentage are drawn as such.</summary>
    [Fact]
    public void Of_DrawsANearlySpentBudgetInQuotaLow()
    {
        var (rail, _) = TheTen();

        var gauge = RailLines.Of(rail, new RateLimitQuota(15, 300, null), 23)[^2];

        Assert.Equal("│ █░░░░░░░░░   5%│", gauge.Text);
        Assert.Contains(gauge.Spans, span => span is { Role: Role.QuotaLow, Text: "█" });
        Assert.Contains(gauge.Spans, span => span.Role == Role.QuotaLow && span.Text.Contains("5%", StringComparison.Ordinal));
        Assert.DoesNotContain(gauge.Spans, span => span.Role == Role.Gauge);
    }

    /// <summary>Before anything has asked there is no budget to draw, so the gauge's row is blank rather than a guess.</summary>
    [Fact]
    public void Of_DrawsNoGaugeBeforeAnythingHasAsked()
    {
        var (rail, _) = TheTen();

        var drawn = RailLines.Of(rail, null, 23);

        Assert.Equal("│                │", drawn[^2].Text);
        Assert.DoesNotContain(drawn, line => line.Has(Role.Gauge) || line.Has(Role.GaugeEmpty));
    }

    /// <summary>
    ///     With two or more profiles the API panel holds the instance acted as, above the gauge, in the quota's role
    ///     and clipped to the panel (ADR-0020, #241).
    /// </summary>
    [Fact]
    public void Of_PutsTheInstanceInTheApiPanelAboveTheGauge()
    {
        var (rail, _) = TheTen();

        var drawn = RailLines.Of(rail, Plenty, 23, instance: "social.a-very-long-instance.example");

        Assert.Equal("╭ API ───────────╮", drawn[^4].Text);
        Assert.StartsWith("│ social.", drawn[^3].Text, StringComparison.Ordinal);
        Assert.EndsWith("…│", drawn[^3].Text, StringComparison.Ordinal);
        Assert.Contains(drawn[^3].Spans, span => span.Role == Role.Quota && span.Text.Contains("social", StringComparison.Ordinal));
        Assert.Contains("83%", drawn[^2].Text, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Compact, each group's title is a heading row with its destinations under it, there are no frames, and the
    ///     API panel is one gauge row at the foot.
    /// </summary>
    [Fact]
    public void Of_Compact_HeadsEachGroupAndDrawsTheBudgetAsOneRow()
    {
        var (rail, _) = TheTen();

        var drawn = RailLines.Of(rail, Plenty, 15);

        Assert.Equal(
            [
                "Timelines",
                "▶ Home",
                "  Local",
                "  Federated",
                "  Hashtag",
                "Explore",
                "  Discover",
                "  Search",
                "Inbox",
                "  Notifications",
                "  Direct messages",
                "  Follow requests",
                "You",
                "  @jeff",
                " ██████████░░  83%",
            ],
            drawn.Select(line => line.Text.TrimEnd()));

        Assert.Equal(Role.PanelTitle, drawn[0].Spans[0].Role);
    }

    /// <summary>Compact with rows to spare, the instance still sits above the gauge; with none, the gauge alone does.</summary>
    [Fact]
    public void Of_Compact_KeepsTheInstanceWhereThereIsARowForIt()
    {
        var (rail, _) = TheTen();

        var roomy = RailLines.Of(rail, Plenty, 18, instance: "hachyderm.io");
        var tight = RailLines.Of(rail, Plenty, 15, instance: "hachyderm.io");

        Assert.Equal(" hachyderm.io", roomy[^2].Text.TrimEnd());
        Assert.DoesNotContain(tight, line => line.Text.Contains("hachyderm", StringComparison.Ordinal));
    }

    /// <summary>
    ///     Below 15 rows the compact rail scrolls to keep the cursor's group in view, its heading included, with the
    ///     gauge still held at the foot.
    /// </summary>
    [Theory]
    [InlineData(0, "Timelines", "Home")]
    [InlineData(7, "Inbox", "Direct messages")]
    [InlineData(9, "You", "@jeff")]
    public void Of_Scrolled_KeepsTheCursorsGroupInView(int cursor, string group, string label)
    {
        var (rail, _) = TheTen();

        rail.Step(cursor);

        var drawn = RailLines.Of(rail, Plenty, 8);
        var texts = drawn.Select(line => line.Text.TrimEnd()).ToList();

        Assert.Equal(8, drawn.Count);
        Assert.Contains(group, texts);
        Assert.Contains(texts, text => text.EndsWith(label, StringComparison.Ordinal) && text.StartsWith('▶'));
        Assert.Contains("83%", texts[^1], StringComparison.Ordinal);
    }

    /// <summary>
    ///     A rail entry is as many columns wide as the room it has, whatever its label is written in. A hashtag rail
    ///     entry takes the tag's own name, and one written in a two-column script padded out by its characters would
    ///     be drawn past the rail's frame and into the content beside it (#207).
    /// </summary>
    [Theory]
    [InlineData("#ドット絵", true)]
    [InlineData("#ドット絵のアカウントですどうぞ", true)]
    [InlineData("#ドット絵のアカウントですどうぞ", false)]
    [InlineData("#photography", false)]
    public void Of_PadsARailEntryToTheRailsColumnsWhateverItsLabelIsWrittenIn(string label, bool coloured)
    {
        var rail = new Rail(
            [new Destination(DestinationKind.Hashtag, label)],
            new FakeShellHost(),
            TimeSpan.FromMilliseconds(250));

        foreach (var height in new[] { 4, 10 })
        {
            Assert.All(RailLines.Of(rail, Plenty, height, coloured: coloured), line => Assert.Equal(RailLines.Width, line.Width));
        }
    }
}
