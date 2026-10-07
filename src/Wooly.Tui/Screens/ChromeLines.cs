using Wooly.Tui.Rendering;
using Wooly.Tui.Theme;

namespace Wooly.Tui.Screens;

/// <summary>
///     What is on screen and is not a screen: the breadcrumb on the content panel's top edge, and the status row along
///     the bottom. Both are the frame rather than the thing being read, which is why neither moves nor scrolls.
/// </summary>
public static class ChromeLines
{
    /// <summary>
    ///     The fetch mark's frames, one a tick and the first again after the last, said here and nowhere else: Claude
    ///     Code's star, growing from a dot and shrinking back. A frame is never drawn at rest, so with colour off its
    ///     presence and its motion are what tell a fetch in flight from none (#281).
    /// </summary>
    /// <remarks>
    ///     Stars rather than braille, which was tried first: a braille frame's dots sit in the top rows of the cell, so
    ///     it floated above the title it follows. Each star is drawn about the middle of the line, as a letter is.
    ///     <para>
    ///         None of them an emoji. Code's third frame is <c>✳</c>, which Unicode marks as one; Windows' console font
    ///         has none of these stars, and the one it falls back to Segoe UI Emoji for comes out as a green square.
    ///         <c>✱</c> is the heavy asterisk in its place, the same size between <c>✢</c> and <c>✶</c>.
    ///     </para>
    /// </remarks>
    private static readonly string[] Spinner = ["·", "✢", "✱", "✶", "✻", "✽", "✻", "✶", "✱", "✢"];

    /// <summary>How many frames the spinner has before it starts over at the first.</summary>
    public static int SpinnerFrames => Spinner.Length;

    /// <summary>
    ///     What parts the spinner from the trail it follows: one space, as the edge leaves one after it, so the star
    ///     sits evenly between the trail and the rule.
    /// </summary>
    private const string Gap = " ";

    /// <summary>
    ///     The columns the mark owns, drawn or not: the gap and one frame, so that nothing beside it moves between
    ///     frames (#217), nor as it comes and goes (#271).
    /// </summary>
    private static readonly int MarkColumns = Glyphs.Columns(Gap) + Spinner.Max(Glyphs.Columns);

    /// <summary>What divides one hint on the status row from the next.</summary>
    private const string Bar = " | ";

    /// <summary>
    ///     What stands between two crumbs, said here and used wherever the trail is put together as one string —
    ///     <c>docs/tui-shell.md</c> and ADR-0014 call it the separator, and it keeps no role of its own.
    /// </summary>
    public const string Separator = " › ";

    /// <summary>What stands in front of a trail too long to draw whole, in place of the crumbs it gave up.</summary>
    private const string Elided = $"…{Separator}";

    /// <summary>
    ///     The content panel's top edge, titled with where you are in the stack and with the fetch mark straight after
    ///     it (ADR-0021). This is the one place a fetch in flight is announced — the rail holds still (ADR-0014) — and
    ///     it is on the frame of the content it is about to replace, beside the crumb it is about to replace.
    /// </summary>
    /// <remarks>
    ///     The crumb you are standing on is the last one, and it is drawn in <see cref="Role.PanelTitle" /> — it is
    ///     what the panel is showing, so it is the panel's title — while the ones you walked through to get there are
    ///     <see cref="Role.Muted" />, separators included, which keep no role of their own (#216, #271).
    ///     <para>
    ///         The mark's columns are held whether or not it is drawn, so the trail is elided in the same room at rest
    ///         as on every frame, and the crumb you are standing on never moves as a fetch starts and ends. A trail
    ///         long enough to elide fills its room, so the spinner lands at the end of the edge; a short one is
    ///         followed by it (#281). The mark is handed over as a frame number rather than a flag, because the view
    ///         counts and this spells: the frames and their columns stay here beside the trail arithmetic they have to
    ///         agree with (#217).
    ///     </para>
    ///     <para>
    ///         Always the active edge: the content panel is the one being read, whichever rail group is lit.
    ///     </para>
    /// </remarks>
    /// <param name="crumbs">
    ///     Where you are, a crumb a screen deep, outermost first — the stack itself rather than the one string it
    ///     reads as. Handed over unjoined because the trail is drawn crumb by crumb: a trail joined here and split
    ///     again there is a trail whose crumbs are wherever the separator happens to appear, and a reader is entitled
    ///     to search for <c>a › b</c>.
    /// </param>
    /// <param name="frame">
    ///     Which of the spinner's frames to draw on this tick, counted from one and wrapping after
    ///     <see cref="SpinnerFrames" />. Nought draws no mark at all, which is both "nothing in flight" and "in flight,
    ///     but not yet for a whole tick".
    /// </param>
    /// <param name="width">The columns the panel has, its corners included.</param>
    public static Line Breadcrumb(IReadOnlyList<string> crumbs, int frame, int width)
    {
        // A panel too narrow to hold the mark beside a column of trail never draws it, so there is nothing to hold its
        // room for.
        var titled = Panel.TitleRoom(width);
        var holds = titled - MarkColumns > 0;
        var trail = Trail(crumbs, Math.Max(0, holds ? titled - MarkColumns : titled));

        return Panel.Top(holds && frame > 0 ? [.. trail, Mark(frame)] : trail, width, active: true);
    }

    /// <summary>The fetch mark on its <paramref name="frame" />th frame: the gap, then the frame.</summary>
    private static Span Mark(int frame) => new($"{Gap}{Spinner[(frame - 1) % Spinner.Length]}", Role.Spinner);

    /// <summary>
    ///     <paramref name="crumbs" /> in the <paramref name="room" /> they have, eliding from the left: the crumb you
    ///     are standing on is kept whole and the ones you walked through are given up for <see cref="Elided" />.
    /// </summary>
    /// <remarks>
    ///     Which way round to elide was settled by what each end says (#168): the far end is the destination screen,
    ///     which the rail is already drawing 20 columns to the left, and the near end is the only thing on the row
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
            return [.. crumbs.SelectMany((crumb, at) => Spans(crumb, at, at, at == standing))];
        }

        var budget = room - Glyphs.Columns(Elided);

        if (budget < Glyphs.Columns(crumbs[standing]))
        {
            return [new Span(TextWrap.Clip(crumbs[standing], room), Role.PanelTitle) { Item = standing }];
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
            new Span(Elided, Role.Muted),
            .. crumbs.Skip(kept).SelectMany((crumb, at) => Spans(crumb, at, kept + at, kept + at == standing)),
        ];
    }

    /// <summary>
    ///     One crumb and the separator in front of it, where it is not the first thing on the row. The crumb carries
    ///     <paramref name="depth" />, its place in the stack, which is what a click on it walks back to (#308); the
    ///     separator carries nothing, so a click between two crumbs is a click on neither.
    /// </summary>
    private static IEnumerable<Span> Spans(string crumb, int at, int depth, bool standing)
    {
        if (at > 0)
        {
            yield return new Span(Separator, Role.Muted);
        }

        yield return new Span(crumb, standing ? Role.PanelTitle : Role.Muted) { Item = depth };
    }

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
            // The two keys take the role a key has and the words beside them do not (#221): these are the columns
            // #219 reserved as the ones that must never be cut, and the keys in them are the ones a reader presses.
            // A question esc may agree to is answered yes or no (#373): "esc keep" would be a lie about one esc put.
            IReadOnlyList<Span> answer = !question.YesOrNo
                ?
                [
                    new Span("  ", Role.Chrome),
                    new Span(question.Confirm, Role.Key),
                    new Span($" {question.Going}", Role.Muted),
                    new Span(" · ", Role.Chrome),
                    new Span("esc", Role.Key),
                    new Span(" keep", Role.Muted),
                ]
                :
                [
                    new Span("  ", Role.Chrome),
                    new Span(question.Confirm, Role.Key),
                    new Span(" / ", Role.Chrome),
                    new Span("n", Role.Key),
                ];

            // The answer's columns are reserved first. A row narrower than the answer alone is narrower than the shell
            // draws: the question gets no room at all there, and the answer is clipped rather than left blank.
            return Line.Of([
                new Span(Asked(question, width - answer.Sum(span => span.Width)), Role.Destructive),
                .. Clipped(answer, width),
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
        var whole = question.Warning.Length == 0 ? $" {question.Ask}" : $" {question.Ask} {question.Warning}";

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

        return [new Span(" ", Role.Chrome), .. Barred(said)];
    }

    /// <summary>
    ///     The status row's hints, each its explanation then its key (<see cref="KeyHint.Spans" />), divided from the
    ///     next by a bar — the strip ADR-0021 took from lazygit (#268). A bar rather than the shell's looser dot, which a
    ///     hint's own colon and space would crowd: <c>Read: ⏎ · Reply: r</c> reads as one run.
    /// </summary>
    private static IEnumerable<Span> Barred(IReadOnlyList<IReadOnlyList<Span>> said)
    {
        for (var i = 0; i < said.Count; i++)
        {
            if (i > 0)
            {
                yield return new Span(Bar, Role.Chrome);
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
