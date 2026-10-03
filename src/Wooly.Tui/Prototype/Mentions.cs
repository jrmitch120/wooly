using Wooly.Tui.Rendering;
using Wooly.Tui.Theme;

namespace Wooly.Tui.Prototype;

// PROTOTYPE — throwaway. @-mention autocomplete for variant A: the accounts you follow, fetched once when compose first
// opens and filtered here as you type. Falls back to a made-up list where the fetch fails or comes back empty.

internal sealed record Mentionable(string Name, string Address);

internal static class Mentions
{
    public const int Most = 5;

    public static readonly Mentionable[] Sample =
    [
        new("Mira Okafor", "mira@hachyderm.io"),
        new("Sam Whitfield", "sam@fosstodon.org"),
        new("Priya Natarajan", "priya@mastodon.social"),
        new("Ada Lindqvist", "ada@photog.social"),
        new("Kenji Sato", "kenji@social.coop"),
        new("Tomás Reyes", "tomas@fosstodon.org"),
        new("Maria Gonzalez", "maria@mastodon.online"),
        new("Marcus Hale", "mhale@infosec.exchange"),
        new("Matt Ludwig", "mattl@ruby.social"),
        new("Ruth Abernathy", "ruth@chaos.social"),
        new("Dev Sharma", "devs@hachyderm.io"),
        new("Lena Fischer", "lena@norden.social"),
    ];

    /// <summary>Who can be suggested: the real follows once fetched, the made-up ones until then.</summary>
    public static IReadOnlyList<Mentionable> Known { get; set; } = Sample;

    /// <summary>Keeps <see cref="Sample" /> whatever the fetch says — for the headless dump, whose fake follows match nothing.</summary>
    public static bool Pinned { get; set; }

    /// <summary>Whether <see cref="Known" /> is the real list, for the footer to say so.</summary>
    public static bool Real { get; set; }

    /// <summary>
    ///     The @-word the caret is at the end of, as the column it starts at and what follows the @ — or
    ///     <see langword="null" /> where the caret is not in one. An @ only starts one at the start of a line or after a
    ///     space, so an email address in a post never opens the list.
    /// </summary>
    public static (int Start, string Query)? At(string line, int caret)
    {
        caret = Math.Clamp(caret, 0, line.Length);
        var at = caret;

        while (at > 0 && Fits(line[at - 1]))
        {
            at--;
        }

        if (at >= caret || line[at] != '@' || (at > 0 && !char.IsWhiteSpace(line[at - 1])))
        {
            return null;
        }

        var query = line[(at + 1)..caret];

        // A finished handle with its own @instance stops asking.
        return query.Contains('@') && query.Contains('.') && Known.Any(m => m.Address.Equals(query, StringComparison.OrdinalIgnoreCase))
            ? null
            : (at, query);

        static bool Fits(char c) => char.IsLetterOrDigit(c) || c is '_' or '.' or '-' or '@';
    }

    /// <summary>
    ///     The best few for <paramref name="query" />: a handle starting with it first, then a word of the name
    ///     starting with it, then a handle merely containing it.
    /// </summary>
    public static IReadOnlyList<Mentionable> For(string query)
    {
        int? Rank(Mentionable m)
        {
            if (m.Address.StartsWith(query, StringComparison.OrdinalIgnoreCase))
            {
                return 0;
            }

            if (m.Name.Split(' ').Any(word => word.StartsWith(query, StringComparison.OrdinalIgnoreCase)))
            {
                return 1;
            }

            return m.Address.Contains(query, StringComparison.OrdinalIgnoreCase) ? 2 : null;
        }

        return [.. Known.Select(m => (m, rank: Rank(m)))
                        .Where(x => x.rank is not null)
                        .OrderBy(x => x.rank)
                        .ThenBy(x => x.m.Name, StringComparer.OrdinalIgnoreCase)
                        .Take(Most)
                        .Select(x => x.m)];
    }

    /// <summary>
    ///     The list as rows: a rounded box in the dim border, the picked row marked and lifted, the part of each that
    ///     matched in the mention colour, and the keys along the bottom edge.
    /// </summary>
    public static IReadOnlyList<Line> Rows(IReadOnlyList<Mentionable> matches, int picked, string query, int width)
    {
        var inside = width - 2;
        var rows = new List<Line> { new([new Span("╭" + new string('─', inside) + "╮", Role.PanelBorder)]) };

        for (var at = 0; at < matches.Count; at++)
        {
            var m = matches[at];
            var on = at == picked;
            var spans = new List<Span>
            {
                new(on ? "▌" : " ", on ? Role.Selection : Role.Body),
                new(" ", Role.Body),
            };
            spans.AddRange(Lit(m.Name, query, on ? Role.ReferencePicked : Role.Body));
            spans.Add(new Span("  ", Role.Body));
            spans.AddRange(Lit("@" + m.Address, query, on ? Role.BylineHandle : Role.Muted, skip: 1));

            var used = spans.Sum(s => s.Width);
            var line = used > inside
                ? Line.Of(TextWrap.Clip(new Line(spans).Text, inside), on ? Role.ReferencePicked : Role.Body).Spans
                : [.. spans, new Span(new string(' ', inside - used), Role.Body)];

            rows.Add(new Line([new Span("│", Role.PanelBorder), .. line, new Span("│", Role.PanelBorder)]));
        }

        Span[] keys = [new(" ↑↓", Role.Key), new(" pick  ", Role.Muted), new("tab", Role.Key), new(" insert  ", Role.Muted), new("esc", Role.Key), new(" close ", Role.Muted)];
        var keyWidth = keys.Sum(k => k.Width);
        rows.Add(keyWidth + 3 > inside
            ? new Line([new Span("╰" + new string('─', inside) + "╯", Role.PanelBorder)])
            : new Line([new Span("╰" + new string('─', inside - keyWidth - 1), Role.PanelBorder), .. keys, new Span("─╯", Role.PanelBorder)]));

        return rows;
    }

    /// <summary>How wide the box wants to be for <paramref name="matches" />.</summary>
    public static int Width(IReadOnlyList<Mentionable> matches) =>
        Math.Max(40, matches.Max(m => Glyphs.Columns(m.Name) + Glyphs.Columns(m.Address) + 1) + 2 + 2 + 2 + 1);

    /// <summary><paramref name="text" /> with where <paramref name="query" /> matched it picked out.</summary>
    private static IEnumerable<Span> Lit(string text, string query, Role role, int skip = 0)
    {
        var at = query.Length == 0 ? -1 : text.IndexOf(query, skip, StringComparison.OrdinalIgnoreCase);

        if (at < 0)
        {
            return [new Span(text, role)];
        }

        return [new Span(text[..at], role), new Span(text.Substring(at, query.Length), Role.Mention), new Span(text[(at + query.Length)..], role)];
    }
}
