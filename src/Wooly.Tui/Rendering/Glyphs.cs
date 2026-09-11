using System.Globalization;
using Terminal.Gui.Text;

namespace Wooly.Tui.Rendering;

/// <summary>
///     Somebody else's words as a terminal will deal with them: how wide they come out in the columns it draws in,
///     where they can be cut, and the marks taken out that this client and a terminal would count differently — so
///     that a row laid out to fill its width is a row drawn to fill its width, and no wider.
/// </summary>
/// <remarks>
///     A display name is written by somebody who has no idea a terminal has columns, and a character is not a column:
///     <c>ドット絵アカウント</c> is nine characters and eighteen columns, <c>é</c> written as a letter and a combining
///     mark is two characters and one column, and <c>👨‍👩‍👧</c> is eight characters and two. Every layout in the shell
///     weighs text against the room it has, so all of them ask <see cref="Columns" /> and none of them ask
///     <see cref="string.Length" /> (#207).
///     <para>
///         Measured by Terminal.Gui's own reading — <c>Rune.GetColumns()</c>, and the grapheme-aware sum of it over a
///         string — because that is what places the cells this client's rows are painted into: agreeing with it is
///         what makes the model and the grid the same shape.
///     </para>
///     <para>
///         The one mark that is dropped rather than measured is the <em>variation selector</em>: <c>U+FE0F</c> after a
///         character asks for the emoji form of it, and <c>An Amazing Wizard➡️KICKSTARTER</c> is three numbers where a
///         layout needs one. The characters count thirty; Terminal.Gui packs the pair into a single cell and counts
///         twenty-nine; the terminal paints the emoji two columns wide and counts thirty. Every cell after it is
///         painted a column right of the one Terminal.Gui put it in, so the last cell of a full-width row crosses the
///         right margin and the terminal wraps it — which is how the <c>y</c> of <c>3y</c> came to be drawn on the
///         rail, and an age to read as a count with no unit (#206). Measuring only settles what <em>this</em> client
///         thinks: the cell Terminal.Gui reserves is one cell whatever the layout believes, and nothing here can widen
///         it. Without the selector a terminal draws the plain form of the character, one column, which is the width
///         all three had already counted.
///     </para>
/// </remarks>
public static class Glyphs
{
    private const char FirstSelector = '\uFE00';

    private const char LastSelector = '\uFE0F';

    /// <summary>The last character whose column can be counted without asking anybody: one character, one column.</summary>
    private const char LastPlainAscii = '~';

    /// <summary>The first of them, everything below it being a control character and none of them a column wide.</summary>
    private const char FirstPlainAscii = ' ';

    /// <summary>
    ///     <paramref name="text" /> as a terminal will draw it, which is the form every layout is measured against.
    /// </summary>
    /// <remarks>
    ///     The text itself where it carries no selector, which is nearly everything this client draws: the scan is
    ///     the whole cost on that path, and every row is laid out afresh on every frame.
    /// </remarks>
    public static string Plain(string text) =>
        text.AsSpan().IndexOfAnyInRange(FirstSelector, LastSelector) < 0
            ? text
            : new string([.. text.Where(character => character is < FirstSelector or > LastSelector)]);

    /// <summary>How wide <paramref name="text" /> is, in the columns a terminal will draw it in.</summary>
    /// <remarks>
    ///     The single measure every layout reads. Plain ASCII — which is most of what a client of an English-speaking
    ///     instance draws, and all of its own chrome — is a column a character and answered by the scan alone; anything
    ///     else is handed to Terminal.Gui, whose reading of it is the one that places the cells.
    /// </remarks>
    public static int Columns(string text) =>
        text.AsSpan().ContainsAnyExceptInRange(FirstPlainAscii, LastPlainAscii) ? text.GetColumns() : text.Length;

    /// <summary>As much of the front of <paramref name="text" /> as fits in <paramref name="columns" />.</summary>
    /// <remarks>
    ///     The text itself where the whole of it fits, so the common case costs a measure and nothing else. What comes
    ///     back is never wider than it was asked for and may be a column narrower: a region with an odd number of
    ///     columns left and a two-column character at the cut cannot hold half of one.
    /// </remarks>
    public static string Cut(string text, int columns) =>
        columns <= 0 ? string.Empty
        : Columns(text) <= columns ? text
        : text[..Reach(text, 0, text.Length, columns)];

    /// <summary>
    ///     <paramref name="text" /> with spaces after it until it fills <paramref name="columns" /> — and as it was
    ///     where it already fills them or more.
    /// </summary>
    /// <remarks>
    ///     <see cref="string.PadRight(int)" /> counts the characters it is adding against the characters already
    ///     there, which is the same mistake as measuring a row by them: a rail entry holding a nine-character,
    ///     eighteen-column name and padded to twenty would be drawn twenty-nine columns wide.
    /// </remarks>
    public static string Padded(string text, int columns)
    {
        var wanting = columns - Columns(text);

        return wanting <= 0 ? text : text + new string(' ', wanting);
    }

    /// <summary>
    ///     How far into <c>text[from..to]</c> <paramref name="columns" /> columns reach — the index the text stops at,
    ///     which is always a boundary between two of the things a reader would call characters.
    /// </summary>
    /// <remarks>
    ///     An index into the text rather than a count of columns, because that is what a layout needs to cut on and
    ///     what <see cref="TextWrap.Row" /> hands back (#83). Grapheme clusters are stepped over whole: a cut between
    ///     a base character and its combining mark, or inside a ZWJ sequence, paints something nobody wrote.
    /// </remarks>
    public static int Reach(string text, int from, int to, int columns)
    {
        var at = from;

        while (at < to)
        {
            // Plain ASCII is a character a column, answered without cutting the text up — which is what this walks
            // over on nearly every row, and every row is laid out afresh on every frame.
            if (text[at] is >= FirstPlainAscii and <= LastPlainAscii)
            {
                if (columns < 1)
                {
                    return at;
                }

                columns--;
                at++;

                continue;
            }

            var end = Past(text, at, to);
            var wide = Columns(text[at..end]);

            if (wide > columns)
            {
                return at;
            }

            columns -= wide;
            at = end;
        }

        return to;
    }

    /// <summary>
    ///     Where the character at <paramref name="at" /> ends — the whole grapheme cluster, which is the smallest step
    ///     any layout here takes.
    /// </summary>
    public static int Past(string text, int at, int to) =>
        text[at] is >= FirstPlainAscii and <= LastPlainAscii
            ? at + 1
            : at + StringInfo.GetNextTextElementLength(text.AsSpan(at, to - at));
}
