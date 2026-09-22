using Wooly.Tui.Rendering;
using Wooly.Tui.Theme;

namespace Wooly.Tui.Screens;

/// <summary>
///     What is on screen and is not a screen: the breadcrumb above the content, the status row along the bottom, and
///     the two seams between the regions. All of it is the frame rather than the thing being read, which is why none
///     of it moves and none of it scrolls.
/// </summary>
public static class ChromeLines
{
    /// <summary>What the breadcrumb says while a fetch is in flight, said here and nowhere else.</summary>
    private const string Fetching = "fetching…";

    /// <summary>What the rail is divided from the content by, one column wide.</summary>
    private const string Rule = "│";

    /// <summary>
    ///     What stands between two crumbs, said here and used wherever the trail is put together as one string —
    ///     <c>docs/tui-shell.md</c> and ADR-0014 call it the separator, and it keeps no role of its own.
    /// </summary>
    public const string Separator = " › ";

    /// <summary>What stands in front of a trail too long to draw whole, in place of the crumbs it gave up.</summary>
    private const string Elided = $"…{Separator}";

    /// <summary>
    ///     Where you are in the stack, with the fetch marker at its right. This is the one place a fetch in flight is
    ///     announced — the rail holds still (ADR-0014) — and it is beside the content it is about to replace.
    /// </summary>
    /// <remarks>
    ///     The crumb you are standing on is the last one, and it is drawn in <see cref="Role.CrumbCurrent" /> while
    ///     the ones you walked through to get there stay <see cref="Role.Chrome" /> — separators included, which keep
    ///     no role of their own (#216). The marker's columns come off the trail's room before any of that, which is
    ///     the order this has always worked in: a mark is beside the trail rather than over it.
    /// </remarks>
    /// <param name="crumbs">
    ///     Where you are, a crumb a screen deep, outermost first — the stack itself rather than the one string it
    ///     reads as. Handed over unjoined because this row is drawn crumb by crumb: a trail joined here and split
    ///     again there is a trail whose crumbs are wherever the separator happens to appear, and a reader is entitled
    ///     to search for <c>a › b</c>.
    /// </param>
    public static Line Breadcrumb(IReadOnlyList<string> crumbs, bool fetching, int width)
    {
        var mark = fetching ? Fetching : string.Empty;
        var room = Math.Max(0, width - Glyphs.Columns(mark) - 1);
        var shown = Trail(crumbs, room);
        var columns = shown.Sum(span => span.Width);

        return new Line([
            .. shown,
            new Span(new string(' ', Math.Max(1, width - columns - Glyphs.Columns(mark))), Role.Chrome),
            new Span(mark, Role.Loading),
        ]);
    }

    /// <summary>
    ///     <paramref name="crumbs" /> in the <paramref name="room" /> they have, eliding from the left: the crumb you
    ///     are standing on is kept whole and the ones you walked through are given up for <see cref="Elided" />.
    /// </summary>
    /// <remarks>
    ///     Which way round to elide was settled by what each end says (#168): the far end is the destination screen,
    ///     which the rail is already drawing 18 columns to the left, and the near end is the only thing on the row
    ///     that the rail does not say. Clipping from the right kept the first and lost the second.
    ///     <para>
    ///         A single crumb wider than the room is clipped from the right after all, which is the one case there is
    ///         nothing else to do with: what is left of it still starts with the words that tell it from its
    ///         neighbours, and the lead comes off rather than spending four of the few columns there are.
    ///     </para>
    /// </remarks>
    private static IReadOnlyList<Span> Trail(IReadOnlyList<string> crumbs, int room)
    {
        var standing = crumbs.Count - 1;
        var whole = crumbs.Sum(Glyphs.Columns) + standing * Glyphs.Columns(Separator);

        if (whole <= room)
        {
            return [.. crumbs.SelectMany((crumb, at) => Spans(crumb, at, at == standing))];
        }

        var budget = room - Glyphs.Columns(Elided);

        if (budget < Glyphs.Columns(crumbs[standing]))
        {
            return [new Span(TextWrap.Clip(crumbs[standing], room), Role.CrumbCurrent)];
        }

        // Leftwards from where you are standing, taking whole crumbs while there is room for one — so the row ends in
        // the crumb the reader is on however deep the trail behind it is.
        var kept = standing;
        var used = Glyphs.Columns(crumbs[standing]);

        while (kept > 0 && used + Glyphs.Columns(Separator) + Glyphs.Columns(crumbs[kept - 1]) <= budget)
        {
            kept--;
            used += Glyphs.Columns(Separator) + Glyphs.Columns(crumbs[kept]);
        }

        return
        [
            new Span(Elided, Role.Chrome),
            .. crumbs.Skip(kept).SelectMany((crumb, at) => Spans(crumb, at, kept + at == standing)),
        ];
    }

    /// <summary>One crumb and the separator in front of it, where it is not the first thing on the row.</summary>
    private static IEnumerable<Span> Spans(string crumb, int at, bool standing)
    {
        if (at > 0)
        {
            yield return new Span(Separator, Role.Chrome);
        }

        yield return new Span(crumb, standing ? Role.CrumbCurrent : Role.Chrome);
    }

    /// <summary>
    ///     The row between the breadcrumb and the content: nothing, in the seam's own background. A blank row is how
    ///     every screen in this shell divides one thing from the next, and it is what keeps the breadcrumb from
    ///     reading as the first line of what is being read (#216).
    /// </summary>
    /// <remarks>
    ///     Drawn rather than left to the window underneath, for the reason the column beside it is: a cell nothing
    ///     paints is a cell Terminal.Gui paints, in a grey no theme chose. Under <c>NO_COLOR</c> the background goes
    ///     and the row is blank, which is the whole of what it was before the background was added — the division is
    ///     carried there by the row existing at all.
    /// </remarks>
    public static Line Seam(int width) => Line.Of(new string(' ', Math.Max(0, width)), Role.Seam);

    /// <summary>
    ///     The column between the rail and the content, <paramref name="height" /> rows of it. A rule rather than a
    ///     band alone, so that the two are still divided on a terminal drawing no colour — the same <c>─</c> the rail
    ///     already ends itself with, stood on end (<see cref="RailLines" />).
    /// </summary>
    public static IReadOnlyList<Line> Gutter(int height) =>
        [.. Enumerable.Repeat(Line.Of(Rule, Role.Seam), Math.Max(0, height))];

    /// <summary>
    ///     The status row: what this screen's keys are, or — when there is one — the thing the shell has to say
    ///     instead. A confirmation displaces the keys because it is the only thing on screen worth answering.
    /// </summary>
    public static Line Status(
        IReadOnlyList<KeyHint> keys,
        string? notice,
        bool noticeIsError,
        Shell.Confirmation? asking,
        int width)
    {
        if (asking is { } question)
        {
            return Line.Of([
                new Span(TextWrap.Clip($" {question.Question}", width), Role.Destructive),
                new Span(
                    TextWrap.Clip(
                        $"  {question.Confirm} {question.Going} · esc keep",
                        Math.Max(0, width - Glyphs.Columns(question.Question) - 1)),
                    Role.Muted),
            ]);
        }

        if (notice is { } said)
        {
            return Line.Of(TextWrap.Clip($" {said}", width), noticeIsError ? Role.Error : Role.Muted);
        }

        return new Line(Clipped([new Span(" ", Role.Chrome), .. KeySpans(keys)], width));
    }

    /// <summary>
    ///     The status row's keys, each glued to its explanation (<see cref="KeyHint.Spans" />) and separated from the
    ///     next by the same looser dot the rest of the shell uses.
    /// </summary>
    private static IEnumerable<Span> KeySpans(IReadOnlyList<KeyHint> keys)
    {
        for (var i = 0; i < keys.Count; i++)
        {
            if (i > 0)
            {
                yield return new Span(" · ", Role.Chrome);
            }

            foreach (var span in keys[i].Spans)
            {
                yield return span;
            }
        }
    }

    /// <summary>
    ///     <paramref name="spans" /> cut to <paramref name="width" /> columns, each run kept in its own role up to the
    ///     cut and the run straddling it clipped in place — the same one-ellipsis-at-the-edge rule
    ///     <see cref="TextWrap.Clip" /> applies to a single string, spread across however many roles the text carries.
    /// </summary>
    private static IReadOnlyList<Span> Clipped(IReadOnlyList<Span> spans, int width)
    {
        var result = new List<Span>();
        var used = 0;

        foreach (var span in spans)
        {
            if (used >= width)
            {
                break;
            }

            if (used + span.Width <= width)
            {
                result.Add(span);
                used += span.Width;

                continue;
            }

            var clipped = TextWrap.Clip(span.Text, width - used);

            if (clipped.Length > 0)
            {
                result.Add(new Span(clipped, span.Role));
            }

            break;
        }

        return result;
    }
}
