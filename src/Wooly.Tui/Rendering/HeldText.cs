using Terminal.Gui.ViewBase;

namespace Wooly.Tui.Rendering;

/// <summary>
///     Text put into a view's cells the way <see cref="Glyphs.Columns" /> measured it: a character carrying a spacing
///     mark in a cell of its own with the next left blank, every other run as Terminal.Gui places it (ADR-0031).
/// </summary>
/// <remarks>
///     Terminal.Gui gives such a character one cell, and a terminal draws it in one, two, or more; the blank is the
///     room a terminal drawing it two wide covers, and the output draws the character after the rest of its row so
///     that one drawing it wider still moves nothing after it (<see cref="Views.HeldOutput" />). The text itself is
///     left as it was written — what a reader copies, searches or wraps is what the author wrote.
/// </remarks>
internal static class HeldText
{
    /// <summary>
    ///     <paramref name="text" /> put into <paramref name="view" /> from <paramref name="column" /> of
    ///     <paramref name="row" />, in the attribute set already.
    /// </summary>
    public static void AddHeld(this View view, int column, int row, string text)
    {
        if (Glyphs.MeasuredAlike(text))
        {
            view.AddStr(column, row, text);

            return;
        }

        var from = 0;

        for (var at = 0; at < text.Length;)
        {
            var end = Glyphs.Past(text, at, text.Length);

            if (Glyphs.MeasuredAlike(text[at..end]))
            {
                at = end;

                continue;
            }

            var run = text[from..at];

            view.AddStr(column, row, run);
            column += Glyphs.Columns(run);
            view.AddStr(column, row, text[at..end] + new string(' ', Glyphs.HeldColumns - 1));
            column += Glyphs.HeldColumns;
            from = at = end;
        }

        view.AddStr(column, row, text[from..]);
    }
}
