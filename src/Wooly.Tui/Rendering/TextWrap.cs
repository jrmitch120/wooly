namespace Wooly.Tui.Rendering;

/// <summary>
///     Breaking a post's text into the rows a terminal has. The narrow case is what this is for: the contract puts the
///     content region at 61 columns on an 80-column terminal, and a post is written by somebody who could not know
///     that.
/// </summary>
public static class TextWrap
{
    /// <summary>The rows <paramref name="text" /> takes at <paramref name="width" /> columns.</summary>
    /// <remarks>
    ///     The author's own line breaks are kept — a post laid out in short lines was laid out that way on purpose —
    ///     and only what overflows is wrapped. A word longer than the whole width is cut rather than allowed to run
    ///     off the side, which is the one case where something the author wrote cannot be shown as written.
    /// </remarks>
    public static IReadOnlyList<string> Wrap(string text, int width)
    {
        if (width <= 0)
        {
            return [];
        }

        var rows = new List<string>();

        foreach (var paragraph in text.Split('\n'))
        {
            rows.AddRange(WrapParagraph(paragraph.TrimEnd(), width));
        }

        return rows;
    }

    /// <summary>
    ///     <paramref name="text" /> cut to <paramref name="width" />, with the cut marked, for the places that get one
    ///     row however long the text is — a rail entry, a one-line summary of a post.
    /// </summary>
    public static string Clip(string text, int width) => width <= 0 ? string.Empty
        : text.Length <= width ? text
        : width == 1 ? "…"
        : text[..(width - 1)] + "…";

    private static IEnumerable<string> WrapParagraph(string paragraph, int width)
    {
        if (paragraph.Length == 0)
        {
            return [string.Empty];
        }

        var rows = new List<string>();
        var row = string.Empty;

        foreach (var word in paragraph.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = row.Length == 0 ? word : $"{row} {word}";

            if (candidate.Length <= width)
            {
                row = candidate;

                continue;
            }

            if (row.Length > 0)
            {
                rows.Add(row);
                row = string.Empty;
            }

            // A single word too long for the whole row: nowhere to break it that keeps it readable, so it is cut in
            // width-sized pieces rather than shortened away. A URL is the usual one, and half of a URL is worth more
            // to a reader than an ellipsis.
            var rest = word;

            while (rest.Length > width)
            {
                rows.Add(rest[..width]);
                rest = rest[width..];
            }

            row = rest;
        }

        if (row.Length > 0)
        {
            rows.Add(row);
        }

        return rows;
    }
}
