using System.Globalization;
using Wooly.Core.Http;
using Wooly.Tui.Rendering;
using Wooly.Tui.Shell;
using Wooly.Tui.Theme;

namespace Wooly.Tui.Screens;

/// <summary>
///     The rail as rows: the four rail groups, each a panel titled with its name and holding its destinations with
///     their unread counts, and an <c>API</c> panel at the foot holding the budget as a gauge — with the instance above
///     it where there are profiles to tell apart. Twenty columns, frames included, full height less the status row
///     (<c>docs/tui-shell.md</c>, ADR-0021).
/// </summary>
/// <remarks>
///     On a terminal too short to frame every group the rail steps down rather than clipping: compact, with each
///     group's title a heading row, no frames, and the budget one gauge row; then, where even that does not fit, the
///     compact rail scrolled to keep the cursor's group in view.
/// </remarks>
public static class RailLines
{
    /// <summary>
    ///     How wide the rail is, which is what leaves the content panel 60 columns at an 80-column terminal, and 58 inside
    ///     its edges. Twenty rather than ADR-0014's eighteen so that, inside a group's frame and beside the mark column,
    ///     every label fits whole and a count still has a column parting it from its label (#272).
    /// </summary>
    public const int Width = 20;

    /// <summary>
    ///     Where the cursor is — where the tabbing has got to. Shown on the cursor's row however it stands, where colour
    ///     is not drawn.
    /// </summary>
    private const string CursorMark = "▶";

    /// <summary>
    ///     Where the selection has settled, shown only while it differs from the cursor — the ~250ms of a settle
    ///     window, and never at rest, since the two coincide there and the filled mark already covers the row (#78).
    /// </summary>
    private const string SettledMark = "▷";

    /// <summary>
    ///     The column of the shell's bottom frame row the toggle's mark is in, shown or hidden: just inside the corner
    ///     the rail's foot or the content panel starts at, where a pointer finds the corner (#445).
    /// </summary>
    public const int ToggleColumn = 1;

    /// <summary>
    ///     The toggle's mark on the shown rail, a click on which hides it, and on the content panel once it is hidden,
    ///     a click on which shows it — pointing the way the rail would go. Latin-1 and not East Asian ambiguous, so a
    ///     column wide on every terminal, which a symbol's width is not (ADR-0031).
    /// </summary>
    private const string Hides = "«";

    /// <inheritdoc cref="Hides" />
    private const string Shows = "»";

    /// <summary>Below this share of the budget, the quota is drawn as nearly spent.</summary>
    private const double NearlySpent = 0.1;

    /// <summary>
    ///     The rows the <c>API</c> panel is counted at when deciding whether the rail fits framed: its two edges, the
    ///     instance and the gauge. Counted at four with one profile too, so that whether the rail is framed is a fact
    ///     about the terminal's height and not about how many profiles there are.
    /// </summary>
    private const int ApiRows = 4;

    /// <summary>The columns the one mark column takes with the space after it.</summary>
    private const int MarkColumns = 2;

    /// <summary>The columns a panel's two sides take off the rows inside it.</summary>
    private const int Sides = 2;

    /// <summary>
    ///     The columns of a gauge row that are not its bar: a space in, a space after the bar, and the percentage's
    ///     four.
    /// </summary>
    private const int GaugeMargin = 6;

    /// <summary>The rail, drawn to <paramref name="height" /> rows with the budget held at the bottom.</summary>
    /// <param name="rail">The destinations, the cursor, and the selection.</param>
    /// <param name="quota">What the instance last said is left, or <see langword="null" /> before anything asked.</param>
    /// <param name="height">How many rows there are, which the groups and the budget share.</param>
    /// <param name="instance">
    ///     The instance the session is acting as, drawn above the gauge, or <see langword="null" /> where there is only
    ///     the one profile and nothing to tell apart (ADR-0020).
    /// </param>
    /// <param name="coloured">
    ///     Whether colour is drawn. Where it is, the cursor and the selection are bands and no row carries a mark; where
    ///     it is not, the one mark column carries them (ADR-0021).
    /// </param>
    public static IReadOnlyList<Line> Of(
        Rail rail,
        RateLimitQuota? quota,
        int height,
        string? instance = null,
        bool coloured = false)
    {
        if (height <= 0)
        {
            return [];
        }

        var groups = Groups(rail);

        return height >= groups.Sum(group => group.Places.Count + Sides) + ApiRows
            ? Framed(rail, groups, quota, height, instance, coloured)
            : Compact(rail, groups, quota, height, instance, coloured);
    }

    /// <summary>
    ///     Every group framed as a panel, the one holding the cursor in the active role, and the
    ///     <c>API</c> panel held at the foot however tall the terminal is — where a reader learns to look for it, and
    ///     load-bearing rather than decorative: the rail is the one thing that can spend the budget by accident
    ///     (ADR-0014).
    /// </summary>
    private static List<Line> Framed(
        Rail rail,
        IReadOnlyList<Run> groups,
        RateLimitQuota? quota,
        int height,
        string? instance,
        bool coloured)
    {
        const int inside = Width - Sides;
        var lines = new List<Line>();

        foreach (var (group, places) in groups)
        {
            lines.AddRange(Panel.Framed(
                Title(group),
                [.. places.Select(at => Entry(rail, at, inside, coloured ? string.Empty : Mark(rail, at), coloured))],
                Width,
                places.Count + Sides,
                // The cursor's group rather than the selection's, so the frame moves on the press: a jump into another group
                // says at once where it landed, rather than a settle window later (#272).
                active: places.Contains(rail.Cursor)));
        }

        List<Line> api = instance is null ? [Gauge(quota, inside)] : [Fact(instance, inside), Gauge(quota, inside)];
        var foot = Panel.Framed("API", api, Width, api.Count + Sides, active: false).ToList();

        // The toggle's mark on the foot's bottom edge, just inside the corner, which is the shell's bottom-left (#445).
        foot[^1] = WithToggleMark(foot[^1], RailState.Shown);

        return Footed(lines, foot, height);
    }

    /// <summary>
    ///     Each group's title as a heading row over its destinations, no frames, and the budget one gauge row at the
    ///     foot — scrolled, where even that does not fit, to keep the cursor's group in view.
    /// </summary>
    /// <remarks>
    ///     Worked out afresh on every frame from where the cursor is, rather than remembered: the rail has no scroll of
    ///     its own to keep, and a group in view is the whole of what it owes.
    /// </remarks>
    private static List<Line> Compact(
        Rail rail,
        IReadOnlyList<Run> groups,
        RateLimitQuota? quota,
        int height,
        string? instance,
        bool coloured)
    {
        var body = new List<Line>();
        int first = 0, last = 0, cursor = 0;

        foreach (var (group, places) in groups)
        {
            if (places.Contains(rail.Cursor))
            {
                first = body.Count;
                last = body.Count + places.Count;
                cursor = first + 1 + (rail.Cursor - places[0]);
            }

            body.Add(Line.Of(
                new Span(Glyphs.Padded(TextWrap.Clip(Title(group), Width), Width), Role.PanelTitle)));

            // Indented under the heading by the mark column, which colour leaves blank, so a heading and an entry are
            // told apart by where they start on every terminal.
            body.AddRange(places.Select(at => Entry(rail, at, Width, coloured ? new string(' ', MarkColumns) : Mark(rail, at), coloured)));
        }

        var spare = height - body.Count - 1;

        // The instance where there is a row for it, and the gauge alone where there is not: the gauge is what the rail
        // is the one thing able to spend. With no frame to carry it, the gauge row carries the toggle's mark in its
        // first cell, the shell's bottom-left, so that a pointer finds it at every height (#445).
        List<Line> foot = instance is not null && spare >= 1
            ? [Fact(instance, Width), Gauge(quota, Width, marked: true)]
            : [Gauge(quota, Width, marked: true)];

        var room = Math.Max(0, height - foot.Count);

        if (body.Count > room)
        {
            // The cursor's group, heading and all, as far as the room allows — and the cursor's own row whatever the
            // room is, since that is the row the tabbing is looking at.
            var top = Math.Min(first, Math.Max(0, last + 1 - room));
            top = Math.Max(top, cursor - room + 1);

            body = body.GetRange(Math.Min(top, body.Count), Math.Min(room, body.Count - top));
        }

        return Footed(body, foot, height);
    }

    /// <summary>
    ///     <paramref name="row" />, a bottom edge, with the toggle's mark in place of whatever it drew at
    ///     <see cref="ToggleColumn" />, in that cell's role — the glyph is the whole of what it says, with colour or
    ///     without: <c>«</c> on the shown rail's foot, and <c>»</c> on the content panel's once the rail is hidden
    ///     (#445).
    /// </summary>
    /// <param name="row">The bottom edge the mark is drawn on.</param>
    /// <param name="rail">Which rail a click on the mark would leave the other of.</param>
    public static Line WithToggleMark(Line row, RailState rail)
    {
        const int column = ToggleColumn;
        var mark = rail is RailState.Shown ? Hides : Shows;
        var spans = new List<Span>();
        var start = 0;

        foreach (var span in row.Spans)
        {
            var end = start + span.Width;

            if (column < start || column >= end)
            {
                spans.Add(span);
            }
            else
            {
                // Cut either side of the cell, padded where a two-column character straddled it, so the row keeps its
                // width and everything after the mark stays where it was.
                var before = Glyphs.Padded(Glyphs.Cut(span.Text, column - start), column - start);
                var reach = Glyphs.Reach(span.Text, 0, span.Text.Length, column - start + 1);
                var past = Glyphs.Columns(span.Text[..reach]);
                var after = new string(' ', Math.Max(0, past - (column - start) - 1)) + span.Text[reach..];

                spans.AddRange(before.Length > 0 ? [span with { Text = before }] : Array.Empty<Span>());
                spans.Add(new Span(mark, span.Role));
                spans.AddRange(after.Length > 0 ? [span with { Text = after }] : Array.Empty<Span>());
            }

            start = end;
        }

        return row.Respanned(spans);
    }

    /// <summary>
    ///     <paramref name="lines" /> with <paramref name="foot" /> held at the bottom of <paramref name="height" /> rows,
    ///     filled between with rail-width blanks rather than empty rows, so the rail is a column of one width all the way
    ///     down rather than a ragged edge.
    /// </summary>
    private static List<Line> Footed(List<Line> lines, IReadOnlyList<Line> foot, int height)
    {
        while (lines.Count < height - foot.Count)
        {
            lines.Add(Line.Of(new string(' ', Width), Role.Rail));
        }

        lines.AddRange(foot);

        return lines.Count > height ? lines.GetRange(lines.Count - height, height) : lines;
    }

    /// <summary>The rail's groups as runs of one group, in the order they are drawn, each with the places it places.</summary>
    private static List<Run> Groups(Rail rail)
    {
        var groups = new List<(RailGroup Group, List<int> Places)>();

        for (var at = 0; at < rail.Destinations.Count; at++)
        {
            var group = rail.Destinations[at].Group;

            if (groups.Count == 0 || groups[^1].Group != group)
            {
                groups.Add((group, []));
            }

            groups[^1].Places.Add(at);
        }

        return [.. groups.Select(run => new Run(run.Group, run.Places))];
    }

    /// <summary>What a rail group is called on its panel's edge, or on its heading row.</summary>
    private static string Title(RailGroup group) => group switch
    {
        RailGroup.Timelines => "Timelines",
        RailGroup.Explore => "Explore",
        RailGroup.Inbox => "Inbox",
        RailGroup.You => "You",
        _ => throw new ArgumentOutOfRangeException(nameof(group), group, "A rail group with no title."),
    };

    /// <summary>The one mark column and the space after it, for where colour is not drawn.</summary>
    private static string Mark(Rail rail, int at) =>
        (at == rail.Cursor ? CursorMark : at == rail.Current ? SettledMark : " ").PadRight(MarkColumns);

    /// <summary>
    ///     One destination, <paramref name="width" /> columns of it: <paramref name="lead" />, its label, and its unread
    ///     count at the end.
    /// </summary>
    /// <remarks>
    ///     In colour the band is the whole of the mark: the selected row on <see cref="Role.RailCurrent" />, its count
    ///     with it, and while tabbing the cursor's row on <see cref="Role.RailCursor" />. Without colour the selected row
    ///     keeps its role and the lead carries the marks.
    /// </remarks>
    private static Line Entry(Rail rail, int at, int width, string lead, bool coloured)
    {
        var destination = rail.Destinations[at];
        var current = at == rail.Current;
        var role = current ? Role.RailCurrent : coloured && at == rail.Cursor ? Role.RailCursor : Role.Rail;

        // Nothing waiting is no badge rather than a 0, and a read that came back full is at least that many (#396).
        var unread = destination.Unread > 0
            ? destination.Unread.ToString(CultureInfo.CurrentCulture) + (destination.Full ? "+" : string.Empty)
            : string.Empty;

        // A column between a label and its count, so that a label clipped to the room ends in its ellipsis and a gap
        // rather than running into the number.
        var room = width - Glyphs.Columns(lead) - (unread.Length > 0 ? Glyphs.Columns(unread) + 1 : 0);
        var label = Glyphs.Padded(TextWrap.Clip(destination.Label, room), room);

        // Part of the destination it draws, by its place on the rail, so that a click on the row can be answered from
        // the rows themselves in whichever layout drew them (#288) — a title, a heading and the foot are part of none.
        return Line.Of([
            .. lead.Length > 0 ? [new Span(lead, role)] : Array.Empty<Span>(),
            new Span(label, role),
            .. unread.Length > 0
                ? [new Span(" ", role), new Span(unread, coloured && current ? Role.RailCurrent : Role.RailUnread)]
                : Array.Empty<Span>(),
        ]).PartOf(at);
    }

    /// <summary>
    ///     The budget as a gauge <paramref name="width" /> columns wide: <c>█</c> for what is left and <c>░</c> for what
    ///     is spent, then the percentage left — or a blank row before anything has asked, rather than a guess.
    /// </summary>
    /// <param name="quota">What the instance last said is left, or <see langword="null" /> before anything did.</param>
    /// <param name="width">How many columns the row takes.</param>
    /// <param name="marked">
    ///     Whether the row carries the toggle's mark at <see cref="ToggleColumn" />, after the space in, as the compact
    ///     rail's does — the bar a cell shorter for it, rather than a cell of it drawn over (#445).
    /// </param>
    private static Line Gauge(RateLimitQuota? quota, int width, bool marked = false)
    {
        Span[] lead = marked ? [new Span(" ", Role.Quota), new Span(Hides, Role.Quota)] : [new Span(" ", Role.Quota)];

        if (quota is null)
        {
            return Line.Of([.. lead, new Span(new string(' ', Math.Max(0, width - lead.Length)), Role.Quota)]);
        }

        var low = quota.Fraction <= NearlySpent;
        var cells = Math.Max(0, width - GaugeMargin - (lead.Length - 1));
        var full = (int)Math.Round(cells * quota.Fraction, MidpointRounding.AwayFromZero);
        var percent = ((int)(quota.Fraction * 100)).ToString(CultureInfo.CurrentCulture);

        return Line.Of([
            .. lead,
            .. full > 0 ? [new Span(new string('█', full), low ? Role.QuotaLow : Role.Gauge)] : Array.Empty<Span>(),
            .. cells - full > 0 ? [new Span(new string('░', cells - full), Role.GaugeEmpty)] : Array.Empty<Span>(),
            new Span(Glyphs.Cut($" {percent,3}%", width - lead.Length - cells), low ? Role.QuotaLow : Role.Quota),
        ]);
    }

    /// <summary>
    ///     A row of the foot: one space in, clipped at its end, and padded out to <paramref name="width" />.
    /// </summary>
    /// <remarks>
    ///     The instance and not the profile's name, which is the reader's own label and says nothing about who they are.
    ///     Read with the @handle on the Profile row it makes the full address; the handle is never moved down here, so
    ///     the clip only ever takes the instance's end and never the part telling two accounts on it apart (#241).
    /// </remarks>
    private static Line Fact(string text, int width) =>
        Line.Of(Glyphs.Padded(TextWrap.Clip($" {text}", width), width), Role.Quota);

    /// <summary>One rail group as it stands on this rail: the places on the rail it holds, in the order they are drawn.</summary>
    private sealed record Run(RailGroup Group, IReadOnlyList<int> Places);
}
