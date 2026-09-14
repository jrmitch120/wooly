using Wooly.Tui.Rendering;

namespace Wooly.Tui.Prototype.Breadcrumb;

/// <summary>How the current crumb is told from the ones behind it.</summary>
/// <param name="Name">What to call it while looking at it.</param>
/// <param name="Cost">What it costs — new roles, columns, and what a mono terminal is left with.</param>
/// <param name="Ancestors">The paint a walked-through crumb takes.</param>
/// <param name="Here">The paint the crumb you are standing on takes.</param>
/// <param name="Separators">The paint the `›` takes.</param>
/// <param name="Glyph">What goes in front of the current crumb, or empty for nothing.</param>
/// <param name="Banded">Whether the whole row is drawn in a band of its own.</param>
/// <param name="HereBanded">Whether the crumb you are standing on gets a band, the way the rail's current row does.</param>
public sealed record Style(
    string Name,
    string Cost,
    Paint Ancestors,
    Paint Here,
    Paint Separators,
    string Glyph,
    bool Banded,
    bool HereBanded = false);

/// <summary>What a trail too long for the row loses.</summary>
public enum Elide
{
    /// <summary>Today: `TextWrap.Clip` cuts the right, which is the crumb saying where you are.</summary>
    ClipRight,

    /// <summary>Drop whole crumbs off the front, `…` in their place.</summary>
    FromLeft,

    /// <summary>Keep the first and the last, `…` for everything between.</summary>
    DropMiddle,
}

/// <summary>The breadcrumb row, drawn the way a candidate would draw it.</summary>
public static class Trail
{
    private const string Separator = " › ";

    private const string Fetching = "fetching…";

    /// <summary>The seven candidates, none privileged.</summary>
    public static readonly IReadOnlyList<Style> Styles =
    [
        new(
            "1. today",
            "one `chrome` span; nothing says where you are",
            Paint.Ancestor,
            Paint.Ancestor,
            Paint.Ancestor,
            string.Empty,
            false),
        new(
            "2. foreground",
            "one new role; nothing in mono",
            Paint.Ancestor,
            Paint.Here,
            Paint.Separator,
            string.Empty,
            false),
        new(
            "3. dim the past",
            "no new role — ancestors take `loading`'s grey, here keeps `chrome`; nothing in mono",
            Paint.Separator,
            Paint.Ancestor,
            Paint.Separator,
            string.Empty,
            false),
        new(
            "4. glyph",
            "no new role, 2 columns, holds in mono",
            Paint.Ancestor,
            Paint.Ancestor,
            Paint.Separator,
            "▸ ",
            false),
        new(
            "5. glyph + foreground",
            "one new role, 2 columns, holds in mono",
            Paint.Ancestor,
            Paint.Here,
            Paint.Separator,
            "▸ ",
            false),
        new(
            "6. band + foreground",
            "one new role + a band; draws the seam under the row; nothing in mono",
            Paint.Ancestor,
            Paint.Here,
            Paint.Separator,
            string.Empty,
            true),
        new(
            "7. band + glyph + foreground",
            "one new role + a band, 2 columns, holds in mono",
            Paint.Ancestor,
            Paint.Here,
            Paint.Separator,
            "▸ ",
            true),
        new(
            "8. the current crumb in a band of its own",
            "one new role + its band, no columns; says what `rail-current` says, the way it says it; nothing in mono",
            Paint.Ancestor,
            Paint.Here,
            Paint.Separator,
            string.Empty,
            false,
            true),
        new(
            "9. band on the current crumb + glyph",
            "one new role + its band, 2 columns, holds in mono",
            Paint.Ancestor,
            Paint.Here,
            Paint.Separator,
            "▸ ",
            false,
            true),
    ];

    /// <summary>One row: the trail in <paramref name="style" />, the fetch mark at its right, cut to fit.</summary>
    public static IReadOnlyList<Ribbon> Row(
        IReadOnlyList<string> crumbs,
        Style style,
        Elide elide,
        bool fetching,
        int width)
    {
        var mark = fetching ? Fetching : string.Empty;
        var room = Math.Max(0, width - Glyphs.Columns(mark) - (fetching ? 1 : 0));
        var runs = Cut(Runs(crumbs, style), room, elide, style);
        var used = runs.Sum(run => Glyphs.Columns(run.Text));
        var filler = new string(' ', Math.Max(fetching ? 1 : 0, width - used - Glyphs.Columns(mark)));

        return [.. runs, new Ribbon(filler, style.Ancestors), new Ribbon(mark, Paint.Mark)];
    }

    /// <summary>The trail as runs, before anything is cut off it.</summary>
    private static IReadOnlyList<Ribbon> Runs(IReadOnlyList<string> crumbs, Style style)
    {
        var runs = new List<Ribbon>();

        for (var i = 0; i < crumbs.Count; i++)
        {
            var last = i == crumbs.Count - 1;

            if (i > 0)
            {
                runs.Add(new Ribbon(Separator, style.Separators));
            }

            if (last && style.Glyph.Length > 0)
            {
                runs.Add(new Ribbon(style.Glyph, style.Here, style.HereBanded));
            }

            runs.Add(new Ribbon(crumbs[i], last ? style.Here : style.Ancestors, last && style.HereBanded));
        }

        return runs;
    }

    /// <summary>The trail cut to <paramref name="room" /> columns, losing whichever end this candidate loses.</summary>
    private static IReadOnlyList<Ribbon> Cut(
        IReadOnlyList<Ribbon> runs,
        int room,
        Elide elide,
        Style style)
    {
        if (runs.Sum(run => Glyphs.Columns(run.Text)) <= room)
        {
            return runs;
        }

        return elide switch
        {
            Elide.ClipRight => ClipRight(runs, room),
            Elide.FromLeft => FromLeft(runs, room, style),
            _ => DropMiddle(runs, room, style),
        };
    }

    /// <summary>What ships: keep the front, cut the back — so a deep stack loses the crumb saying where you are.</summary>
    private static IReadOnlyList<Ribbon> ClipRight(IReadOnlyList<Ribbon> runs, int room)
    {
        var kept = new List<Ribbon>();
        var used = 0;

        foreach (var run in runs)
        {
            var wide = Glyphs.Columns(run.Text);

            if (used + wide <= room)
            {
                kept.Add(run);
                used += wide;

                continue;
            }

            var clipped = TextWrap.Clip(run.Text, room - used);

            if (clipped.Length > 0)
            {
                kept.Add(run with { Text = clipped });
            }

            break;
        }

        return kept;
    }

    /// <summary>Drop whole crumbs off the front until the tail fits, `…` standing in for what went.</summary>
    private static IReadOnlyList<Ribbon> FromLeft(IReadOnlyList<Ribbon> runs, int room, Style style)
    {
        var lead = new Ribbon("… › ", style.Separators);
        var tail = runs.ToList();

        while (tail.Count > 1
               && Glyphs.Columns(lead.Text) + tail.Sum(run => Glyphs.Columns(run.Text)) > room)
        {
            // A crumb and the separator in front of it go together; the first run is a crumb, so drop two.
            tail.RemoveRange(0, Math.Min(2, tail.Count - 1));
        }

        return [lead, .. ClipRight(tail, Math.Max(0, room - Glyphs.Columns(lead.Text)))];
    }

    /// <summary>Keep where you started and where you are; `…` for the walk between them.</summary>
    private static IReadOnlyList<Ribbon> DropMiddle(IReadOnlyList<Ribbon> runs, int room, Style style)
    {
        if (runs.Count < 5)
        {
            return FromLeft(runs, room, style);
        }

        var first = runs[0];
        var middle = new Ribbon(" › … › ", style.Separators);
        var fixedWidth = Glyphs.Columns(first.Text) + Glyphs.Columns(middle.Text);

        // As much of the tail as fits after the root and the `…`, taken from the right so the crumb you are standing
        // on is the last thing to go rather than the first.
        var tail = new List<Ribbon>();

        for (var i = runs.Count - 1; i > 0; i--)
        {
            List<Ribbon> candidate = [runs[i], .. tail];

            if (fixedWidth + candidate.Sum(run => Glyphs.Columns(run.Text)) > room)
            {
                break;
            }

            tail = candidate;
        }

        if (tail.Count == 0)
        {
            return FromLeft(runs, room, style);
        }

        // A tail that starts on a separator is a `›` with nothing in front of it but the `…` that already said so.
        while (tail.Count > 0 && tail[0].Text == Separator)
        {
            tail.RemoveAt(0);
        }

        return [first, middle, .. tail];
    }
}
