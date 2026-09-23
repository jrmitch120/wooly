using Wooly.Tui.Rendering;
using Wooly.Tui.Theme;

namespace Wooly.Tui.Screens;

/// <summary>
///     What is on screen and is not a screen: the breadcrumb above the content, the status row along the bottom, and
///     the column dividing the rail from both. All of it is the frame rather than the thing being read, which is why
///     none of it moves and none of it scrolls.
/// </summary>
public static class ChromeLines
{
    /// <summary>
    ///     What the breadcrumb says while a fetch is in flight, said here and nowhere else — and never on its own: it is
    ///     always followed by at least one dot (<see cref="Mark" />).
    /// </summary>
    private const string Fetching = "fetching";

    /// <summary>How many dots the mark grows to before it starts over at one.</summary>
    public const int MostDots = 3;

    /// <summary>
    ///     The columns the mark owns while it is drawn: the word and the most dots it ever has, so that it is as wide
    ///     on its first tick as on its last and nothing beside it moves between them (#217).
    /// </summary>
    private static readonly int MarkColumns = Glyphs.Columns(Fetching) + MostDots;

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
    ///     the ones you walked through to get there take <see cref="Role.Crumb" /> — separators included, which keep
    ///     no role of their own (#216). Both sit on the same band, and so does the mark: it is one row, and what
    ///     makes it read as the frame is the row being banded rather than any one thing on it. The marker's columns
    ///     come off the trail's room before any of that, which is the order this has always worked in: a mark is
    ///     beside the trail rather than over it.
    ///     <para>
    ///         The mark is handed over as a count of dots rather than a flag, because the view counts and this spells:
    ///         the spelling and its 11 columns stay here beside the trail arithmetic they have to agree with (#217).
    ///     </para>
    /// </remarks>
    /// <param name="crumbs">
    ///     Where you are, a crumb a screen deep, outermost first — the stack itself rather than the one string it
    ///     reads as. Handed over unjoined because this row is drawn crumb by crumb: a trail joined here and split
    ///     again there is a trail whose crumbs are wherever the separator happens to appear, and a reader is entitled
    ///     to search for <c>a › b</c>.
    /// </param>
    /// <param name="dots">
    ///     How many dots the fetch mark has on this tick, one to <see cref="MostDots" />. Nought draws no mark at all,
    ///     which is both "nothing in flight" and "in flight, but not yet for a whole tick".
    /// </param>
    /// <param name="width">The columns the row has.</param>
    public static Line Breadcrumb(IReadOnlyList<string> crumbs, int dots, int width)
    {
        var mark = Mark(dots);
        var room = Math.Max(0, width - Glyphs.Columns(mark) - 1);
        var shown = Trail(crumbs, room);
        var columns = shown.Sum(span => span.Width);

        return new Line([
            .. shown,
            new Span(new string(' ', Math.Max(1, width - columns - Glyphs.Columns(mark))), Role.Crumb),
            new Span(mark, Role.Loading),
        ]);
    }

    /// <summary>
    ///     The fetch mark with <paramref name="dots" /> dots on it, padded to <see cref="MarkColumns" /> — the word at
    ///     the left of its field and the dots growing rightward into the rest, so the word never moves.
    /// </summary>
    private static string Mark(int dots) =>
        dots <= 0 ? string.Empty : $"{Fetching}{new string('.', Math.Min(dots, MostDots))}".PadRight(MarkColumns);

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
            new Span(Elided, Role.Crumb),
            .. crumbs.Skip(kept).SelectMany((crumb, at) => Spans(crumb, at, kept + at == standing)),
        ];
    }

    /// <summary>One crumb and the separator in front of it, where it is not the first thing on the row.</summary>
    private static IEnumerable<Span> Spans(string crumb, int at, bool standing)
    {
        if (at > 0)
        {
            yield return new Span(Separator, Role.Crumb);
        }

        yield return new Span(crumb, standing ? Role.CrumbCurrent : Role.Crumb);
    }

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
            var answer = $"  {question.Confirm} {question.Going} · esc keep";

            // The answer's columns are reserved first. A row narrower than the answer alone is narrower than the shell
            // draws: the question gets no room at all there, and the answer is clipped rather than left blank.
            return Line.Of([
                new Span(Asked(question, width - Glyphs.Columns(answer)), Role.Destructive),
                new Span(TextWrap.Clip(answer, width), Role.Muted),
            ]);
        }

        if (notice is { } said)
        {
            return Line.Of(TextWrap.Clip($" {said}", width), noticeIsError ? Role.Error : Role.Muted);
        }

        return new Line(Fitted(keys, width));
    }

    /// <summary>
    ///     The question a confirmation puts, in the <paramref name="room" /> its answer leaves: the ask and the warning
    ///     where both fit, else the ask alone, and the ask clipped only where not even it will fit (#219,
    ///     <c>docs/tui-shell.md</c>).
    /// </summary>
    /// <remarks>
    ///     The warning goes whole rather than being clipped, because a <c>…</c> says <i>something you cannot see was
    ///     cut</i> — true of an ask, and false of a sentence identical on every confirmation.
    /// </remarks>
    private static string Asked(Shell.Confirmation question, int room)
    {
        var whole = $" {question.Ask} {question.Warning}";

        return Glyphs.Columns(whole) <= room ? whole : TextWrap.Clip($" {question.Ask}", room);
    }

    /// <summary>
    ///     As many of <paramref name="keys" /> as the row has room for, whole and in the order they were handed over,
    ///     then <c>…+N</c> for the ones it could not fit, then <see cref="PostKeys.Asking" /> — which is never cut
    ///     (#218, <c>docs/tui-shell.md</c>).
    /// </summary>
    /// <remarks>
    ///     The order handed over is the rank (<see cref="PostKeys.Around(KeyHint, IReadOnlyList{KeyHint}, KeyHint[])" />),
    ///     so the fill stops at the first hint that does not fit rather than skipping it for a narrower one behind it:
    ///     skipping would buy a few columns at the price of the row no longer reading as a ranking, and of the keys
    ///     shown reshuffling as the terminal is resized.
    ///     <para>
    ///         <c>?</c> is pinned only where the screen said it — a prompt taking letters leaves it off because <c>?</c>
    ///         is a letter there, and the pin adds nothing the screen did not answer to. The overflow mark is
    ///         <see cref="Role.Muted" /> and adds no role: <c>…</c> is the shell's own <i>there was more</i> glyph, so a
    ///         terminal drawing no colour draws the overflow mark exactly as one drawing colour does.
    ///     </para>
    /// </remarks>
    private static IReadOnlyList<Span> Fitted(IReadOnlyList<KeyHint> keys, int width)
    {
        var pinned = keys.Contains(PostKeys.Asking);
        IReadOnlyList<KeyHint> ranked = [.. keys.Where(key => key != PostKeys.Asking)];

        var shown = 0;

        while (shown < ranked.Count && Filled(ranked, shown + 1, pinned).Sum(span => span.Width) <= width)
        {
            shown++;
        }

        // Narrower than the floor — the mark and ? alone — is narrower than the shell draws, and is clipped rather than
        // left blank.
        return Clipped(Filled(ranked, shown, pinned), width);
    }

    /// <summary>
    ///     The first <paramref name="shown" /> of <paramref name="ranked" />, the overflow mark for the rest, and
    ///     <c>?</c> where it is <paramref name="pinned" />.
    /// </summary>
    private static IReadOnlyList<Span> Filled(IReadOnlyList<KeyHint> ranked, int shown, bool pinned)
    {
        var over = ranked.Count - shown;

        IReadOnlyList<IReadOnlyList<Span>> said =
        [
            .. ranked.Take(shown).Select(key => key.Spans),
            .. over > 0 ? [[new Span($"…+{over}", Role.Muted)]] : Array.Empty<IReadOnlyList<Span>>(),
            .. pinned ? [PostKeys.Asking.Spans] : Array.Empty<IReadOnlyList<Span>>(),
        ];

        return [new Span(" ", Role.Chrome), .. Dotted(said)];
    }

    /// <summary>
    ///     The status row's hints, each glued to its explanation (<see cref="KeyHint.Spans" />) and separated from the
    ///     next by the same looser dot the rest of the shell uses.
    /// </summary>
    private static IEnumerable<Span> Dotted(IReadOnlyList<IReadOnlyList<Span>> said)
    {
        for (var i = 0; i < said.Count; i++)
        {
            if (i > 0)
            {
                yield return new Span(" · ", Role.Chrome);
            }

            foreach (var span in said[i])
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
