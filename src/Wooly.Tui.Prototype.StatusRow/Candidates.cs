using Wooly.Tui.Rendering;
using Wooly.Tui.Screens;

namespace Wooly.Tui.Prototype.StatusRow;

/// <summary>One drawing of the row: the rows it takes, and how many hints it left unsaid without marking them.</summary>
/// <param name="Rows">One row, or two for the candidate that spends two.</param>
/// <param name="Silent">Hints that are neither drawn nor counted by a mark — what a reader has no way to know about.</param>
public sealed record Drawn(IReadOnlyList<IReadOnlyList<Ribbon>> Rows, int Silent)
{
    /// <summary>Whether <c>?</c> — the reference the pruned row leans on — survived the cut.</summary>
    public bool Asks => Rows.Any(row => string.Concat(row.Select(r => r.Text)).Contains("?:keys", StringComparison.Ordinal));
}

/// <summary>A way of drawing the row, and the rule it would ship as.</summary>
public sealed record Candidate(string Name, string Rule, Func<Case, int, Drawn> Draw);

/// <summary>The six candidates the ticket names, plus the combination of the ones that survive.</summary>
public static class Candidates
{
    private const string Dot = " · ";

    /// <summary>The keys that mean the same thing on every screen with posts on it.</summary>
    private static readonly IReadOnlyList<KeyHint> Shared = [.. PostKeys.OnAPost, PostKeys.Scrolling];

    /// <summary>The rare end of those: own-posts-only, and the reveal.</summary>
    private static readonly IReadOnlyList<KeyHint> Rare =
        [.. PostKeys.OnAPost.Where(key => key.Key is "p" or "e" or "d" or "x"), PostKeys.Scrolling];

    /// <summary>
    ///     What a group is called, and which hints it swallows. Matched by <b>hint</b> rather than by letter, for the
    ///     reason #193 gives: <c>d</c> is dismiss on the inbox, and a group keyed on the letter would swallow it and
    ///     announce the inbox's own key as <c>p/e/d:yours</c>.
    /// </summary>
    private static readonly (string Word, KeyHint[] Keys)[] Groups =
    [
        ("poll", [new KeyHint("1-0", "option"), new KeyHint("v", "vote")]),
        ("write", [new KeyHint("c", "compose"), new KeyHint("r", "reply")]),
        ("marks", [new KeyHint("b", "boost"), new KeyHint("f", "favorite")]),
        ("yours", [new KeyHint("p", "pin"), new KeyHint("e", "edit"), new KeyHint("d", "delete")]),
    ];

    /// <summary>The candidates, in the order the ticket lists them.</summary>
    public static IReadOnlyList<Candidate> All { get; } =
    [
        new(
            "1 · today",
            "Everything the screen answers to, cut at the right wherever the cut lands.",
            (row, width) => new Drawn([Clip(Hints(row.Keys), width)], Unsaid(row.Keys, width))),
        new(
            "2 · only what is actionable",
            "A key that can do nothing here comes off, the rule #87, #119 and #193 already apply in three places.",
            (row, width) =>
            {
                var keys = Acting(row);

                return new Drawn([Clip(Hints(keys), width)], Unsaid(keys, width));
            }),
        new(
            "3 · prune to the screen's own",
            "The row says what this screen alone answers to, its walk and the way out. Every shared post key goes to `?`.",
            (row, width) =>
            {
                var keys = Own(row.Keys);

                return new Drawn([Clip(Hints(keys), width)], Unsaid(keys, width));
            }),
        new(
            "4 · prune the rare",
            "The marks stay; `p`, `e`, `d`, `x` and the row scroll go to `?`.",
            (row, width) =>
            {
                var keys = Without(row.Keys, Rare);

                return new Drawn([Clip(Hints(keys), width)], Unsaid(keys, width));
            }),
        new(
            "5 · group",
            "`marks:b/f`, `write:c/r`, `yours:p/e/d` — the colon stops being key-to-explanation.",
            (row, width) =>
            {
                var keys = Grouped(row.Keys);

                return new Drawn([Clip(Hints(keys), width)], Unsaid(keys, width));
            }),
        new(
            "6 · an overflow mark",
            "Today's list, whole hints only, with `…+N` and `?:keys` pinned at the right.",
            (row, width) => Marked(row.Keys, width)),
        new(
            "7 · a second row",
            "Today's list across two rows, which costs a content row on every screen.",
            (row, width) => Twice(row.Keys, width)),
        new(
            "8 · actionable, pruned to the screen's own, and marked",
            "2 and 3 and 6 together: idle keys off, shared post keys to `?`, and `…+N` saying how many went.",
            (row, width) => Marked(Own(Acting(row)), width)),
        new(
            "9 · actionable and marked, nothing pruned",
            "2 and 6 without 3: the order decides what falls off, and the mark says how much did.",
            (row, width) => Marked(Acting(row), width)),
    ];

    /// <summary>The row's keys with the ones that can do nothing here taken out.</summary>
    private static IReadOnlyList<KeyHint> Acting(Case row) => [.. row.Keys.Where(row.Acts)];

    /// <summary>The same, less every key that means what it means on every screen with posts on it.</summary>
    private static IReadOnlyList<KeyHint> Own(IReadOnlyList<KeyHint> keys) => Without(keys, Shared);

    private static IReadOnlyList<KeyHint> Without(IReadOnlyList<KeyHint> keys, IReadOnlyList<KeyHint> these) =>
        [.. keys.Where(key => !these.Contains(key))];

    /// <summary>Those keys with each group said once, in the place its first member stood.</summary>
    private static IReadOnlyList<KeyHint> Grouped(IReadOnlyList<KeyHint> keys)
    {
        var said = new HashSet<string>(StringComparer.Ordinal);
        var result = new List<KeyHint>();

        foreach (var key in keys)
        {
            var group = Groups.FirstOrDefault(g => g.Keys.Contains(key));

            if (group.Word is null)
            {
                result.Add(key);

                continue;
            }

            if (said.Add(group.Word))
            {
                result.Add(new KeyHint(string.Join("/", group.Keys.Select(k => k.Key)), group.Word));
            }
        }

        return result;
    }

    /// <summary>
    ///     As many whole hints as fit, then <c>…+N</c> for the ones that did not, then <c>?</c> — which is the key the
    ///     whole approach leans on and so the one key that may never be cut.
    /// </summary>
    private static Drawn Marked(IReadOnlyList<KeyHint> keys, int width)
    {
        var ask = keys[^1];
        IReadOnlyList<KeyHint> rest = [.. keys.Take(keys.Count - 1)];

        if (Width(Hints(keys)) <= width)
        {
            return new Drawn([Hints(keys)], 0);
        }

        for (var shown = rest.Count; shown >= 0; shown--)
        {
            var dropped = rest.Count - shown;
            var row = (List<Ribbon>)
            [
                .. Hints([.. rest.Take(shown)]),
                new Ribbon(Dot, Paint.Sep),
                new Ribbon($"…+{dropped}", Paint.More),
                new Ribbon(Dot, Paint.Sep),
                .. Spans(ask),
            ];

            if (Width(row) <= width)
            {
                return new Drawn([row], 0);
            }
        }

        return new Drawn([Clip(Hints(keys), width)], Unsaid(keys, width));
    }

    /// <summary>The list poured onto two rows, whole hints only, marking anything that still does not fit.</summary>
    private static Drawn Twice(IReadOnlyList<KeyHint> keys, int width)
    {
        var first = new List<KeyHint>();
        var second = new List<KeyHint>();

        foreach (var key in keys)
        {
            var into = Width(Hints([.. first, key])) <= width ? first : second;
            into.Add(key);
        }

        var over = Width(Hints(second)) > width;

        return new Drawn(
            over ? [Hints(first), Marked(second, width).Rows[0]] : [Hints(first), Hints(second)],
            0);
    }

    /// <summary>How many of those hints a clip at <paramref name="width" /> leaves out without saying so.</summary>
    private static int Unsaid(IReadOnlyList<KeyHint> keys, int width)
    {
        var used = 1;
        var shown = 0;

        foreach (var key in keys)
        {
            var next = used + (shown > 0 ? Glyphs.Columns(Dot) : 0) + Columns(key);

            if (next > width)
            {
                break;
            }

            used = next;
            shown++;
        }

        return keys.Count - shown;
    }

    /// <summary>The hints as the shipped row draws them: a leading space, then each glued pair, dot-separated.</summary>
    private static IReadOnlyList<Ribbon> Hints(IReadOnlyList<KeyHint> keys)
    {
        var row = new List<Ribbon> { new(" ", Paint.Sep) };

        for (var i = 0; i < keys.Count; i++)
        {
            if (i > 0)
            {
                row.Add(new Ribbon(Dot, Paint.Sep));
            }

            row.AddRange(Spans(keys[i]));
        }

        return row;
    }

    private static IEnumerable<Ribbon> Spans(KeyHint key) =>
        [new Ribbon(key.Key, Paint.Key), new Ribbon($":{key.Does}", Paint.Does)];

    private static int Columns(KeyHint key) => Glyphs.Columns(key.ToString());

    /// <summary>Columns the row draws in, by the one measure the shell uses.</summary>
    public static int Width(IEnumerable<Ribbon> row) => row.Sum(run => Glyphs.Columns(run.Text));

    /// <summary>
    ///     Today's cut: runs kept whole up to the edge and the one straddling it clipped in place, which is
    ///     <c>ChromeLines.Clipped</c> — so it can land in the middle of a word or between a key and its explanation.
    /// </summary>
    private static IReadOnlyList<Ribbon> Clip(IReadOnlyList<Ribbon> row, int width)
    {
        var result = new List<Ribbon>();
        var used = 0;

        foreach (var run in row)
        {
            var runWidth = Glyphs.Columns(run.Text);

            if (used >= width)
            {
                break;
            }

            if (used + runWidth <= width)
            {
                result.Add(run);
                used += runWidth;

                continue;
            }

            var clipped = TextWrap.Clip(run.Text, width - used);

            if (clipped.Length > 0)
            {
                result.Add(new Ribbon(clipped, run.Paint));
            }

            break;
        }

        return result;
    }
}
