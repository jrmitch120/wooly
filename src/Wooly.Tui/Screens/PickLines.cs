using Wooly.Tui.Rendering;
using Wooly.Tui.Theme;

namespace Wooly.Tui.Screens;

/// <summary>
///     A list a compose field offers to pick from, as rows (#318, #335): a rounded box in the panel's border, a row a
///     thing, the picked one marked <c>▌</c>, and the list's own keys along its bottom edge — every cell in a role. What
///     each row says is the field's; the people to mention are one such list (<see cref="MentionLines" />).
/// </summary>
public static class PickLines
{
    /// <summary>The narrowest the list is drawn, where the field under it is wide enough to hold it.</summary>
    public const int LeastWidth = 40;

    /// <summary>The rows the box spends on anything but things: its top and bottom edges.</summary>
    public const int Edges = 2;

    /// <summary>The columns a row spends on anything but the thing: the box's two sides, the mark and a space.</summary>
    private const int RowDressing = 4;

    /// <summary>
    ///     The columns the bottom edge keeps for its corners and the rule leading into the keys, without which the keys
    ///     are left off rather than squeezed.
    /// </summary>
    private const int EdgeDressing = 3;

    private static readonly Span[] Keys =
    [
        new(" ↑↓", Role.Key), new(" pick  ", Role.Muted),
        new("tab", Role.Key), new(" insert  ", Role.Muted),
        new("esc", Role.Key), new(" close ", Role.Muted),
    ];

    /// <summary>
    ///     How wide the list wants to be for things whose widest takes <paramref name="widest" /> columns: what that
    ///     row needs, or 40.
    /// </summary>
    public static int Width(int widest) => Math.Max(LeastWidth, widest + RowDressing);

    /// <summary>
    ///     The list <paramref name="width" /> wide, showing the things from <paramref name="top" /> on that fit in
    ///     <paramref name="height" /> rows: the thing at <paramref name="picked" /> marked <c>▌</c>, each row
    ///     <see cref="Line.PartOf" /> the thing it shows — which is what a click on it is answered from — and the keys
    ///     along the bottom edge.
    /// </summary>
    /// <param name="row">What a thing says between the mark and the box's side, given whether it is the picked one.</param>
    public static IReadOnlyList<Line> Rows<T>(
        IReadOnlyList<T> things,
        int picked,
        int top,
        int height,
        int width,
        Func<T, bool, IEnumerable<Span>> row)
    {
        var inside = Math.Max(0, width - 2);
        var rows = new List<Line> { Line.Of($"╭{new string('─', inside)}╮", Role.PanelBorder) };
        var shown = Math.Max(0, height - Edges);

        for (var at = top; at < things.Count && at < top + shown; at++)
        {
            rows.Add(Row(row(things[at], at == picked), at == picked, inside).PartOf(at));
        }

        var keys = Keys.Sum(span => span.Width);

        rows.Add(
            keys + EdgeDressing > inside
                ? Line.Of($"╰{new string('─', inside)}╯", Role.PanelBorder)
                : Line.Of(
                [
                    new Span($"╰{new string('─', inside - keys - 1)}", Role.PanelBorder),
                    .. Keys,
                    new Span("─╯", Role.PanelBorder),
                ]));

        return rows;
    }

    /// <summary>One thing, between the box's sides: its mark, and what it says cut to fit.</summary>
    private static Line Row(IEnumerable<Span> says, bool picked, int inside)
    {
        List<Span> spans =
        [
            new(picked ? "▌" : " ", picked ? Role.Selection : Role.Body),
            new(" ", Role.Body),
            .. says,
        ];

        var fitted = Clipped(spans, inside);
        var used = fitted.Sum(span => span.Width);

        return Line.Of(
        [
            new Span("│", Role.PanelBorder),
            .. fitted,
            new Span(new string(' ', Math.Max(0, inside - used)), Role.Body),
            new Span("│", Role.PanelBorder),
        ]);
    }

    /// <summary>As many of <paramref name="spans" /> as fit in <paramref name="width" /> columns, the last one cut.</summary>
    private static List<Span> Clipped(IEnumerable<Span> spans, int width)
    {
        var kept = new List<Span>();
        var left = width;

        foreach (var span in spans)
        {
            if (left <= 0)
            {
                break;
            }

            var cut = Glyphs.Columns(span.Text) <= left ? span.Text : TextWrap.Clip(span.Text, left);

            kept.Add(span with { Text = cut });
            left -= Glyphs.Columns(cut);
        }

        return kept;
    }
}
