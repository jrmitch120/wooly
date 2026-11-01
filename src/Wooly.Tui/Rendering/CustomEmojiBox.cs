using Wooly.Core;
using Wooly.Tui.Media;
using Wooly.Tui.Theme;

namespace Wooly.Tui.Rendering;

/// <summary>
///     What a <b>custom emoji</b> costs the line it is written in: two columns and one row, about square in a cell
///     twice as tall as it is wide, held from the first frame wherever the <b>Raster</b> draws, and its shortcode's
///     letters, in a role of their own (<see cref="Role.CustomEmoji" />), where it draws nothing (ADR-0035).
/// </summary>
/// <remarks>
///     Which of the two is the Raster's alone, never whether the picture has landed: a line laid out as seven columns
///     of <c>:US_AZ:</c> until the picture came would be two columns once it had, and the paragraph under the reader
///     would rewrap. So a line's layout never depends on the picture cache, and the shaded room stands until the
///     picture lands, and for good where it is refused or never comes.
///     <para>
///         Laid out in two steps, so that whatever writes text that may carry one — a post's words and warning, a
///         name, a poll's options, a bio and a custom field — says only where each is written:
///         <see cref="Shortcode" /> marks the run, and <see cref="Boxed" /> sets every run marked on a line as its
///         box, knowing the columns each stands at. The wrap and the clip are told the same shortcodes wherever
///         <see cref="Draws" />, and hold each in <see cref="Columns" />, never cut.
///     </para>
/// </remarks>
public static class CustomEmojiBox
{
    /// <summary>How wide a custom emoji's box is: a Unicode emoji's width, so it sits in a line as one does.</summary>
    public const int Columns = 2;

    /// <summary>How tall it is: the line it is written in, and no more.</summary>
    public const int Rows = 1;

    /// <summary>
    ///     Whether a custom emoji is drawn as its box under <paramref name="drawing" /> — which is whether the
    ///     <b>Raster</b> draws pictures at all — rather than written as its shortcode.
    /// </summary>
    public static bool Draws(Drawing drawing) => Drawing.Draws(drawing.Pictures, drawing.Raster, out _);

    /// <summary>
    ///     The shortcodes a wrap or a clip of text under <paramref name="drawing" /> holds whole at
    ///     <see cref="Columns" />: all of <paramref name="shortcodes" /> where custom emoji are drawn, and none where
    ///     they are their letters.
    /// </summary>
    public static IReadOnlyList<Shortcode> Held(IReadOnlyList<Shortcode> shortcodes, Drawing drawing) =>
        Draws(drawing) ? shortcodes : [];

    /// <summary>
    ///     <paramref name="customEmoji" /> as written, its shortcode in a role of its own so that it reads as a picture that
    ///     was meant whatever it is written in, and marked as that custom emoji (<see cref="Span.CustomEmoji" />) for
    ///     <see cref="Boxed" /> to set as its box.
    /// </summary>
    public static Span Shortcode(CustomEmoji customEmoji) =>
        new(customEmoji.Written, Role.CustomEmoji) { CustomEmoji = customEmoji };

    /// <summary>
    ///     <paramref name="text" /> in <paramref name="role" />, but for each of <paramref name="shortcodes" /> written
    ///     whole in it, which is a run of its own (<see cref="Shortcode(CustomEmoji)" />) — and one only partly in it,
    ///     cut in two where nothing draws and the wrap does not hold it, its letters in the same role, but no custom
    ///     emoji to box.
    /// </summary>
    /// <param name="text">What is written.</param>
    /// <param name="role">What the rest of it is.</param>
    /// <param name="shortcodes">The custom emoji written in the text it is part of, left to right.</param>
    /// <param name="at">Where <paramref name="text" /> starts in the text they were found in.</param>
    public static IEnumerable<Span> Written(string text, Role role, IReadOnlyList<Shortcode> shortcodes, int at = 0)
    {
        var from = 0;

        foreach (var shortcode in shortcodes)
        {
            var starts = Math.Max(shortcode.At - at, from);
            var stops = Math.Min(shortcode.End - at, text.Length);

            if (stops <= starts)
            {
                continue;
            }

            if (starts > from)
            {
                yield return new Span(text[from..starts], role);
            }

            yield return starts == shortcode.At - at && stops == shortcode.End - at
                ? Shortcode(shortcode.CustomEmoji)
                : new Span(text[starts..stops], Role.CustomEmoji);

            from = stops;
        }

        if (text.Length > from)
        {
            yield return new Span(text[from..], role);
        }
    }

    /// <summary>
    ///     <paramref name="text" /> on one row of its own making — a name, a warning, the "boosted" line, a poll's
    ///     option, a preview's last row — clipped to <paramref name="room" />, with each custom emoji of
    ///     <paramref name="listed" /> written in it set as its box (<see cref="Boxed" />): held whole by the clip, so
    ///     one that does not fit is dropped with the rest of what is cut rather than drawn in half (#524).
    /// </summary>
    /// <remarks>
    ///     A row, not spans, so that what comes after it is measured from where the boxes leave it — two columns an
    ///     emoji where the Raster draws, its shortcode's letters where it does not — and added with
    ///     <see cref="Line.Then" />, which keeps the boxes' places. Its boxes start in its first column: put the row
    ///     behind anything with <see cref="Line.After" />, which moves them along.
    /// </remarks>
    /// <param name="text">What is written, as the instance wrote it, shortcodes and all.</param>
    /// <param name="listed">The custom emoji the text's owner lists.</param>
    /// <param name="room">How many columns it has.</param>
    /// <param name="role">What the rest of it is drawn as.</param>
    /// <param name="drawing">How the screen draws.</param>
    public static Line Clipped(string text, IReadOnlyList<CustomEmoji> listed, int room, Role role, Drawing drawing) =>
        Clipped(text, Rendering.Shortcode.In(text, listed), room, role, drawing);

    /// <summary>
    ///     <paramref name="text" /> on one row of its own making as
    ///     <see cref="Clipped(string, IReadOnlyList{CustomEmoji}, int, Role, Drawing)" /> makes one, its custom emoji
    ///     where <paramref name="shortcodes" /> says — for a row naming several accounts, each with a list of its own,
    ///     or one whose custom emoji are written in only part of it.
    /// </summary>
    /// <param name="text">What is written, shortcodes and all.</param>
    /// <param name="shortcodes">The custom emoji written in it, left to right.</param>
    /// <param name="room">How many columns it has.</param>
    /// <param name="role">What the rest of it is drawn as.</param>
    /// <param name="drawing">How the screen draws.</param>
    public static Line Clipped(string text, IReadOnlyList<Shortcode> shortcodes, int room, Role role, Drawing drawing)
    {
        var clipped = TextWrap.Clip(text, room, Held(shortcodes, drawing));

        // What is left of the text in front of the ellipsis, where it was cut: a shortcode cut there, which it is only
        // where nothing draws, is letters cut short like any other word and no custom emoji.
        var kept = clipped == text ? text.Length : clipped.Length - 1;

        return Boxed(Written(clipped, role, [.. shortcodes.Where(shortcode => shortcode.End <= kept)]), drawing);
    }

    /// <summary>
    ///     <paramref name="spans" /> as a row, each custom emoji written in them set as its box where
    ///     <paramref name="drawing" /> draws (<see cref="Boxed(Line, Drawing)" />).
    /// </summary>
    public static Line Boxed(IEnumerable<Span> spans, Drawing drawing) => Boxed(new Line([.. spans]), drawing);

    /// <summary>
    ///     <paramref name="line" /> with each custom emoji written on it set as its box where
    ///     <paramref name="drawing" /> draws, and as it was where it does not: the shortcode's letters, which is what
    ///     the line was laid out for.
    /// </summary>
    /// <remarks>
    ///     A box is the Stand-in's shade until the picture lands and the picture after, set at the column it stands at
    ///     and wanted after anything else the line wants, so a name's emoji come after its avatar — alike on every way
    ///     that draws. On the ways that draw through a box, sixel and Kitty's, it is drawn from boxes and cuts of
    ///     custom emoji's own, and painted as its shade again where none is left for it (<see cref="Placing" />, #527).
    /// </remarks>
    public static Line Boxed(Line line, Drawing drawing)
    {
        if (!Draws(drawing) || !line.Spans.Any(span => span.CustomEmoji is not null))
        {
            return line;
        }

        var spans = new List<Span>(line.Spans.Count);
        var insets = new List<Inset>(line.Insets);
        var wanted = new List<Drawn>();
        var column = 0;

        foreach (var span in line.Spans)
        {
            if (span.CustomEmoji is not { } customEmoji)
            {
                spans.Add(span);
                column += span.Width;

                continue;
            }

            var box = PictureBox.Fixed(Drawn.CustomEmoji(customEmoji), Columns, Rows, drawing.Pictures, drawing.Raster);

            spans.Add(box.Cells(0) with { CustomEmoji = customEmoji });
            insets.AddRange(box.Insets(0, column));
            wanted.Add(box.Wanted!);
            column += Columns;
        }

        return (line.Respanned(spans) with { Insets = insets }).Wanting([.. line.Wanted, .. wanted]);
    }
}
