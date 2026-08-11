namespace Wooly.Tui.Rendering;

/// <summary>
///     Breaking a post's text into the rows a terminal has. The narrow case is what this is for: the contract puts the
///     content region at 61 columns on an 80-column terminal, and a post is written by somebody who could not know
///     that.
/// </summary>
/// <remarks>
///     Every row says where in the text it came from, and is a slice of that text at that offset — which is what lets
///     a **reference** be found once on the whole post and then drawn a row at a time (#83). Keeping that property is
///     why this breaks the text by index rather than by splitting it into words: a run of spaces somebody typed
///     between two words stays on the row it was written on instead of being collapsed away, because a row that read
///     shorter than the text it came from would put every reference after it in the wrong column.
/// </remarks>
public static class TextWrap
{
    /// <summary>One wrapped row, and where in the text it came out of it starts.</summary>
    /// <remarks>
    ///     <see cref="Text" /> is always <c>text.Substring(At, Text.Length)</c> of whatever was wrapped, so an offset
    ///     into the text and a column on the row differ by <see cref="At" /> and by nothing else.
    /// </remarks>
    /// <param name="Text">What is written on the row.</param>
    /// <param name="At">Where the row starts in the text it was wrapped out of.</param>
    public readonly record struct Row(string Text, int At)
    {
        /// <summary>Where the row stops in that text, one past its last character.</summary>
        public int End => At + Text.Length;
    }

    /// <summary>The rows <paramref name="text" /> takes at <paramref name="width" /> columns.</summary>
    /// <remarks>
    ///     The author's own line breaks are kept — a post laid out in short lines was laid out that way on purpose —
    ///     and only what overflows is wrapped. A word longer than the whole width is cut rather than allowed to run
    ///     off the side, which is the one case where something the author wrote cannot be shown as written.
    /// </remarks>
    public static IReadOnlyList<string> Wrap(string text, int width) =>
        [.. Rows(text, width).Select(row => row.Text)];

    /// <summary>The same rows, each saying where in <paramref name="text" /> it starts.</summary>
    /// <remarks>
    ///     What <see cref="Screens.PostLines" /> draws a post's body from, because the roles inside it are worked out
    ///     on the whole text before this is called: a row on its own cannot tell whether it is holding the second half
    ///     of an address that was cut in two.
    /// </remarks>
    public static IReadOnlyList<Row> Rows(string text, int width)
    {
        if (width <= 0)
        {
            return [];
        }

        var rows = new List<Row>();
        var at = 0;

        while (true)
        {
            var line = text.IndexOf('\n', at);

            WrapParagraph(text, at, line < 0 ? text.Length : line, width, rows);

            if (line < 0)
            {
                return rows;
            }

            at = line + 1;
        }
    }

    /// <summary>
    ///     <paramref name="text" /> cut to <paramref name="width" />, with the cut marked, for the places that get one
    ///     row however long the text is — a rail entry, a one-line summary of a post.
    /// </summary>
    public static string Clip(string text, int width) => width <= 0 ? string.Empty
        : text.Length <= width ? text
        : width == 1 ? "…"
        : text[..(width - 1)] + "…";

    /// <summary>
    ///     The rows <c>text[from..to]</c> takes, added to <paramref name="rows" /> — one paragraph, which is as far as
    ///     a row ever reaches, since the author's own line break ends one.
    /// </summary>
    private static void WrapParagraph(string text, int from, int to, int width, List<Row> rows)
    {
        // What the author put at the end of the line is not something a reader can see, and a row of trailing spaces
        // would be a row of the screen spent on nothing.
        while (to > from && char.IsWhiteSpace(text[to - 1]))
        {
            to--;
        }

        if (to == from)
        {
            // A paragraph with nothing on it is still a row of the screen, so it goes back as one empty row rather
            // than as no rows at all.
            rows.Add(new Row(string.Empty, from));

            return;
        }

        var at = from;

        while (at < to)
        {
            // A row starts on something, so the spaces the break left behind are stepped over rather than drawn.
            while (at < to && text[at] == ' ')
            {
                at++;
            }

            if (to - at <= width)
            {
                rows.Add(new Row(text[at..to], at));

                return;
            }

            var edge = at + width;

            // The last place a space could break this row, which is the most words that fit on it. Searching back
            // from the edge rather than forward from the start is the same greedy fit said in one call.
            var space = text.LastIndexOf(' ', edge, edge - at);

            if (space < 0)
            {
                // A single word too long for the whole row: nowhere to break it that keeps it readable, so it is cut
                // at the edge rather than shortened away. An address is the usual one, and half of an address is
                // worth more to a reader than an ellipsis — and the half below still knows where it came from, which
                // is what keeps it part of the same reference.
                rows.Add(new Row(text[at..edge], at));
                at = edge;

                continue;
            }

            rows.Add(new Row(text[at..space].TrimEnd(' '), at));
            at = space;
        }
    }
}
