using Wooly.Tui.Theme;

namespace Wooly.Tui.Rendering;

/// <summary>
///     A panel's frame as rows: a rounded edge, the title on its top edge, and the rows inside inset by it (ADR-0021).
///     The one way a panel is drawn — the rail's groups are framed here as rows, and the content region's frame is
///     these same edges painted round rows the view does not build (<c>PaintedView</c>).
/// </summary>
/// <remarks>
///     This client's own frame rather than Terminal.Gui's <c>Border</c>, which titles itself <c>┤title├</c> with no
///     way to drop the brackets and takes its colour from a scheme outside the roles. Here the edge is
///     <see cref="Role.PanelBorder" />, or <see cref="Role.PanelBorderActive" /> for the panel you are in, and the title
///     <see cref="Role.PanelTitle" />, so a theme answers for every cell of it and the no-colour theme still divides
///     the regions with the box characters alone.
/// </remarks>
public static class Panel
{
    /// <summary>
    ///     How many columns of a panel's width its top edge spends on anything but the title: a corner either side and
    ///     a space either side of the title.
    /// </summary>
    private const int TitleMargin = 4;

    /// <summary>
    ///     How many columns of a panel's top edge an end piece spends on anything but itself: a space either side of it,
    ///     and at least one column of edge parting it from the title.
    /// </summary>
    private const int EndMargin = 3;

    private const string Across = "─";

    private const string Down = "│";

    /// <summary>
    ///     The panel, <paramref name="height" /> rows of it: the top edge titled <paramref name="title" />, then
    ///     <paramref name="rows" /> between its sides, and the bottom edge. Rows past the bottom edge are not drawn, and
    ///     the rows short of it are blank.
    /// </summary>
    /// <param name="title">What the panel is called, in <see cref="Role.PanelTitle" />.</param>
    /// <param name="rows">What is inside it, each cut or padded to the inside's width.</param>
    /// <param name="width">How many columns the panel takes, its edges included.</param>
    /// <param name="height">How many rows it takes, its edges included.</param>
    /// <param name="active">Whether this is the panel you are in, which draws its edge in the active role.</param>
    public static IReadOnlyList<Line> Framed(
        string title,
        IReadOnlyList<Line> rows,
        int width,
        int height,
        bool active) =>
        Framed(Top([new Span(title, Role.PanelTitle)], width, active), rows, width, height, active);

    /// <summary>
    ///     The same, under a top edge already drawn — one whose title says more than a name, like the content panel's
    ///     trail and fetch mark (<see cref="Top(IReadOnlyList{Span}, IReadOnlyList{Span}, int, bool)" />).
    /// </summary>
    /// <param name="top">The top edge, as wide as the panel.</param>
    public static IReadOnlyList<Line> Framed(Line top, IReadOnlyList<Line> rows, int width, int height, bool active)
    {
        if (height <= 0)
        {
            return [];
        }

        var lines = new List<Line> { top };

        for (var at = 0; at < height - 2; at++)
        {
            lines.Add(Between(at < rows.Count ? rows[at] : Line.Blank, width, active));
        }

        if (height > 1)
        {
            lines.Add(Bottom(width, active));
        }

        return lines;
    }

    /// <summary>
    ///     The top edge, with <paramref name="title" /> on it clipped from its end to the room there is, and no title at
    ///     all where there is no room for one.
    /// </summary>
    /// <param name="title">
    ///     The title as spans, so that one whose parts say different things — the content panel's trail and the fetch
    ///     mark at its end — keeps each its own role.
    /// </param>
    public static Line Top(IReadOnlyList<Span> title, int width, bool active) => Top(title, [], width, active);

    /// <summary>
    ///     The top edge with <paramref name="title" /> at its start and <paramref name="end" /> at its end, the edge
    ///     running between them: <c>╭ title ──── end ╮</c>.
    /// </summary>
    /// <param name="end">
    ///     What sits at the far end of the edge — the content panel's fetch mark — drawn whole or not at all, and
    ///     given its room before the title is, so that the title never runs into it. Nothing draws no end and no
    ///     spaces for one.
    /// </param>
    public static Line Top(IReadOnlyList<Span> title, IReadOnlyList<Span> end, int width, bool active)
    {
        var edge = Edge(active);

        if (width < 2)
        {
            return Line.Of(Dashes(width), edge);
        }

        var ending = end.Sum(span => span.Width);
        var ends = ending > 0 && TitleRoom(width, ending) >= 0;
        var room = TitleRoom(width, ends ? ending : 0);

        var shown = Cut(title, room);
        var columns = shown.Sum(span => span.Width);

        // The rule runs from the corner, or from the space after the title, to the space before the end or the far
        // corner — every column the other pieces do not take.
        var rule = width - 2 - (columns > 0 ? columns + 2 : 0) - (ends ? ending + 2 : 0);

        Span[] spans =
        [
            new Span("╭", edge),
            .. columns > 0 ? [new Span(" ", Role.PanelTitle), .. shown, new Span(" ", Role.PanelTitle)] : Array.Empty<Span>(),
            new Span(Dashes(rule), edge),
            .. ends ? [new Span(" ", Role.PanelTitle), .. end, new Span(" ", Role.PanelTitle)] : Array.Empty<Span>(),
            new Span("╮", edge),
        ];

        return new Line([.. spans.Where(span => span.Text.Length > 0)]);
    }

    /// <summary>
    ///     How many columns a top edge <paramref name="width" /> wide leaves its title beside an end piece
    ///     <paramref name="ending" /> columns wide — or beside none, for nought. Asked by a caller that has to fit a title
    ///     to the room before handing it over, as the content panel's trail is elided rather than cut.
    /// </summary>
    public static int TitleRoom(int width, int ending) =>
        width - TitleMargin - (ending > 0 ? EndMargin + ending : 0);

    /// <summary>The bottom edge.</summary>
    public static Line Bottom(int width, bool active) =>
        Line.Of(width < 2 ? Dashes(width) : $"╰{Dashes(width - 2)}╯", Edge(active));

    /// <summary>
    ///     <paramref name="row" /> between the panel's two sides, cut or padded to the inside's width — moved in a column
    ///     by the left side, a picture's box with it, and still part of whatever it was part of.
    /// </summary>
    /// <remarks>
    ///     A picture's box is moved, not cut: a frame built as rows holds what fits it, which the rail's does. Rows that
    ///     scroll and carry pictures are framed by <c>PaintedView</c> instead, where the viewport does the cutting.
    /// </remarks>
    public static Line Between(Line row, int width, bool active)
    {
        var edge = Edge(active);

        if (width < 2)
        {
            return Line.Of(width == 1 ? Down : string.Empty, edge);
        }

        var inside = width - 2;
        var spans = Cut(row.Spans, inside);
        var gap = inside - spans.Sum(span => span.Width);

        return row
            .Respanned([.. spans, .. gap > 0 ? [new Span(new string(' ', gap), Role.Body)] : Array.Empty<Span>(), new Span(Down, edge)])
            .After(new Span(Down, edge));
    }

    private static Role Edge(bool active) => active ? Role.PanelBorderActive : Role.PanelBorder;

    /// <summary>The edge's line, <paramref name="columns" /> of it.</summary>
    private static string Dashes(int columns) => string.Concat(Enumerable.Repeat(Across, Math.Max(0, columns)));

    /// <summary>
    ///     As much of the front of <paramref name="spans" /> as fits in <paramref name="columns" />, cut in the columns a
    ///     terminal draws in (#207).
    /// </summary>
    private static IReadOnlyList<Span> Cut(IReadOnlyList<Span> spans, int columns)
    {
        var kept = new List<Span>();
        var left = Math.Max(0, columns);

        foreach (var span in spans)
        {
            var text = Glyphs.Cut(span.Text, left);

            if (text.Length > 0)
            {
                kept.Add(span with { Text = text });
            }

            left -= Glyphs.Columns(text);

            if (text.Length < span.Text.Length)
            {
                break;
            }
        }

        return kept;
    }
}
