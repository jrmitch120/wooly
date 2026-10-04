using Wooly.Tui.Rendering;
using Wooly.Tui.Shell;
using Wooly.Tui.Theme;

namespace Wooly.Tui.Screens;

/// <summary>
///     The list of people to mention, as rows (#318): a rounded box in the panel's border, a row a person, and the
///     list's own keys along its bottom edge — every cell in a role.
/// </summary>
public static class MentionLines
{
    /// <summary>The narrowest the list is drawn, where the editor is wide enough to hold it.</summary>
    public const int LeastWidth = 40;

    private static readonly Span[] Keys =
    [
        new(" ↑↓", Role.Key), new(" pick  ", Role.Muted),
        new("tab", Role.Key), new(" insert  ", Role.Muted),
        new("esc", Role.Key), new(" close ", Role.Muted),
    ];

    /// <summary>How wide the list wants to be for <paramref name="people" />: what its widest row needs, or 40.</summary>
    public static int Width(IReadOnlyList<Mentionable> people) =>
        Math.Max(
            LeastWidth,
            people.Select(person => Glyphs.Columns(person.Name) + Glyphs.Columns(person.Address) + 1).DefaultIfEmpty().Max()
            + 7);

    /// <summary>
    ///     The list <paramref name="width" /> wide: the person at <paramref name="picked" /> marked <c>▌</c> and lifted,
    ///     the letters <paramref name="query" /> matched picked out in each, and the keys along the bottom edge.
    /// </summary>
    public static IReadOnlyList<Line> Rows(IReadOnlyList<Mentionable> people, int picked, string query, int width)
    {
        var inside = Math.Max(0, width - 2);
        var rows = new List<Line> { Line.Of($"╭{new string('─', inside)}╮", Role.PanelBorder) };

        for (var at = 0; at < people.Count; at++)
        {
            rows.Add(Row(people[at], at == picked, query, inside));
        }

        var keys = Keys.Sum(span => span.Width);

        rows.Add(
            keys + 3 > inside
                ? Line.Of($"╰{new string('─', inside)}╯", Role.PanelBorder)
                : Line.Of(
                [
                    new Span($"╰{new string('─', inside - keys - 1)}", Role.PanelBorder),
                    .. Keys,
                    new Span("─╯", Role.PanelBorder),
                ]));

        return rows;
    }

    /// <summary>One person, between the box's sides: their name, then their handle.</summary>
    private static Line Row(Mentionable person, bool picked, string query, int inside)
    {
        var name = picked ? Role.ReferencePicked : Role.Body;
        var handle = picked ? Role.BylineHandle : Role.Muted;

        List<Span> spans =
        [
            new(picked ? "▌" : " ", picked ? Role.Selection : Role.Body),
            new(" ", Role.Body),
            .. Lit(person.Name, NameMatch(person.Name, query), query.Length, name),
            new(person.Name.Length > 0 ? "  " : string.Empty, Role.Body),
            new("@", handle),
            .. Lit(person.Address, HandleMatch(person.Address, query), query.Length, handle),
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

    /// <summary>Where in a name <paramref name="query" /> matched: the start of the first word it starts.</summary>
    private static int NameMatch(string name, string query)
    {
        if (query.Length == 0)
        {
            return -1;
        }

        for (var at = 0; at < name.Length; at++)
        {
            if ((at == 0 || name[at - 1] == ' ')
                && string.Compare(name, at, query, 0, query.Length, StringComparison.OrdinalIgnoreCase) == 0
                && at + query.Length <= name.Length)
            {
                return at;
            }
        }

        return -1;
    }

    /// <summary>Where in an address <paramref name="query" /> matched, its start first.</summary>
    private static int HandleMatch(string address, string query) =>
        query.Length == 0 ? -1 : address.IndexOf(query, StringComparison.OrdinalIgnoreCase);

    /// <summary><paramref name="text" /> in <paramref name="role" />, with the match at <paramref name="at" /> as a mention.</summary>
    private static IEnumerable<Span> Lit(string text, int at, int length, Role role) =>
        at < 0
            ? [new Span(text, role)]
            :
            [
                new Span(text[..at], role),
                new Span(text.Substring(at, length), Role.Mention),
                new Span(text[(at + length)..], role),
            ];

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
