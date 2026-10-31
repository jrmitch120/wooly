using System.Globalization;
using System.Text;
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
    ///     else is handed to Terminal.Gui, whose reading of it is the one that places the cells. The exception is a
    ///     character carrying a spacing mark, which is <see cref="HeldColumns" /> wherever it is (ADR-0031).
    /// </remarks>
    public static int Columns(string text) =>
        !text.AsSpan().ContainsAnyExceptInRange(FirstPlainAscii, LastPlainAscii) ? text.Length
        : CarriesASpacingMark(text) ? ColumnsByCharacter(text)
        : text.GetColumns();

    /// <summary>
    ///     Whether this client and a terminal measure <paramref name="text" /> alike: false where it carries a
    ///     <em>spacing mark</em> (#432).
    /// </summary>
    /// <remarks>
    ///     A spacing mark is a vowel sign that takes up room of its own — the <c>ী</c> of <c>অসমীয়া</c>, most of the
    ///     vowel signs of the scripts of South and Southeast Asia. Terminal.Gui counts one as no column; a terminal
    ///     counts it its own way, and not one way: measured in herdr, <c>অসমীয়া</c> came out two columns wider than
    ///     counted and <c>हिन्दी</c> one, its <c>ि</c> counted as nothing and its <c>ी</c> as one. Every cell after such
    ///     text lands that many columns right of where it was laid out, which is how a list's border came apart beside
    ///     the languages written in those scripts. There is no count to correct to, so a character carrying one is
    ///     held in <see cref="HeldColumns" />, the second left blank, and drawn after the rest of its row at a place
    ///     of its own (ADR-0031): whatever a terminal makes of it, nothing after it moves. Marks that take no room —
    ///     an accent, Arabic's vowels, Tibetan's — are counted alike, and so is text with no mark at all.
    /// </remarks>
    public static bool MeasuredAlike(string text) =>
        !text.AsSpan().ContainsAnyExceptInRange(FirstPlainAscii, LastPlainAscii) || !CarriesASpacingMark(text);

    /// <summary>
    ///     Whether every terminal draws <paramref name="text" /> a character to a cell: written in Latin, Greek,
    ///     Cyrillic, Armenian, Georgian, Han, kana or Hangul, and nothing else but marks, spaces and punctuation.
    /// </summary>
    /// <remarks>
    ///     A script whose letters join, reorder or stack is shaped, and a terminal may lay a shaped word out off its
    ///     own cells and everything after it on the row with it: probed, Terminal.app drew a border placed by an
    ///     explicit cursor move a column or two out of place after <c>हिन्दी</c>, <c>తెలుగు</c>, <c>ᠮᠣᠩᠭᠣᠯ ᠬᠡᠯᠡ</c> and
    ///     <c>بهاس ملايو</c>, and in place after <c>অসমীয়া</c> and <c>العربية</c> — which words it does that to is a
    ///     matter of its fonts, not of anything the text says (ADR-0031). So this answers for the scripts that are
    ///     never shaped, and is false for every other: text the client writes itself, where it has the choice, is
    ///     written in one of them.
    /// </remarks>
    public static bool DrawnACharacterToACell(string text)
    {
        foreach (var rune in text.EnumerateRunes())
        {
            if (!Unshaped(rune))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>A character of a script that is never shaped, or a mark, space or punctuation beside one.</summary>
    private static bool Unshaped(Rune rune) =>
        rune.Value switch
        {
            < 0x0370 => true, // Latin, its extensions, IPA, spacing modifiers and combining diacritics
            < 0x0530 => true, // Greek and Coptic, Cyrillic and its supplement
            < 0x0590 => true, // Armenian
            >= 0x10A0 and < 0x1100 => true, // Georgian
            >= 0x1100 and < 0x1200 => true, // Hangul jamo
            >= 0x1C80 and < 0x1CC0 => true, // Cyrillic extended, Georgian extended
            >= 0x1D00 and < 0x2000 => true, // phonetic extensions, Latin extended additional, Greek extended
            >= 0x2000 and < 0x2070 => true, // general punctuation
            >= 0x2C60 and < 0x2C80 => true, // Latin extended
            >= 0x2D00 and < 0x2D30 => true, // Georgian supplement
            >= 0x2DE0 and < 0x2E00 => true, // Cyrillic extended
            >= 0x3000 and < 0x3100 => true, // CJK punctuation, hiragana, katakana
            >= 0x3130 and < 0x3190 => true, // Hangul compatibility jamo
            >= 0x3400 and < 0x4DC0 => true, // Han
            >= 0x4E00 and < 0xA000 => true, // Han
            >= 0xA640 and < 0xA6A0 => true, // Cyrillic extended
            >= 0xA720 and < 0xA800 => true, // Latin extended
            >= 0xAB30 and < 0xAB70 => true, // Latin extended
            >= 0xAC00 and < 0xD7B0 => true, // Hangul syllables
            >= 0xF900 and < 0xFB00 => true, // Han compatibility
            >= 0xFF00 and < 0xFFF0 => true, // half- and full-width forms
            >= 0x20000 and < 0x32000 => true, // Han
            _ => false,
        };

    /// <summary>
    ///     Whether <paramref name="text" /> carries a spacing mark, read a character at a time rather than a half of one:
    ///     Chakma's and Grantha's are written as two.
    /// </summary>
    private static bool CarriesASpacingMark(string text)
    {
        foreach (var rune in text.EnumerateRunes())
        {
            if (Rune.GetUnicodeCategory(rune) == UnicodeCategory.SpacingCombiningMark)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///     How many columns a character carrying a spacing mark is held in: its own and one left blank after it, which
    ///     a terminal drawing it two wide covers and one drawing it one wide leaves (ADR-0031).
    /// </summary>
    public const int HeldColumns = 2;

    /// <summary>
    ///     <paramref name="text" /> measured a character at a time, each carrying a spacing mark held in
    ///     <see cref="HeldColumns" /> and every other as Terminal.Gui counts it.
    /// </summary>
    private static int ColumnsByCharacter(string text)
    {
        var columns = 0;

        for (var at = 0; at < text.Length;)
        {
            var end = Past(text, at, text.Length);
            var character = text[at..end];

            columns += MeasuredAlike(character) ? character.GetColumns() : HeldColumns;
            at = end;
        }

        return columns;
    }

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
    /// <param name="text">The text.</param>
    /// <param name="from">Where in it to start.</param>
    /// <param name="to">Where in it to stop at the latest.</param>
    /// <param name="columns">How many columns there are.</param>
    /// <param name="held">
    ///     The custom emoji written in <paramref name="text" /> that are drawn as their boxes, left to right: each is
    ///     stepped over whole, at <see cref="CustomEmojiBox.Columns" />, as a character of that width would be — never
    ///     cut, and stopped in front of where it does not fit (ADR-0035).
    /// </param>
    public static int Reach(string text, int from, int to, int columns, IReadOnlyList<Shortcode>? held = null)
    {
        var at = from;
        var next = 0;

        while (at < to)
        {
            if (Held(held, at, ref next) is { } shortcode)
            {
                if (CustomEmojiBox.Columns > columns)
                {
                    return at;
                }

                columns -= CustomEmojiBox.Columns;
                at = shortcode.End;

                continue;
            }

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
    /// <param name="text">The text.</param>
    /// <param name="at">Where the character starts.</param>
    /// <param name="to">Where the text stops at the latest.</param>
    /// <param name="held">
    ///     The custom emoji drawn as their boxes, as <see cref="Reach" /> takes them: one starting at
    ///     <paramref name="at" /> is stepped past whole.
    /// </param>
    public static int Past(string text, int at, int to, IReadOnlyList<Shortcode>? held = null)
    {
        var next = 0;

        return Held(held, at, ref next) is { } shortcode ? shortcode.End
            : text[at] is >= FirstPlainAscii and <= LastPlainAscii ? at + 1
            : at + StringInfo.GetNextTextElementLength(text.AsSpan(at, to - at));
    }

    /// <summary>
    ///     How wide <paramref name="text" /> is with each of <paramref name="held" /> drawn as its box, which is
    ///     <see cref="CustomEmojiBox.Columns" /> however long its shortcode.
    /// </summary>
    public static int Columns(string text, IReadOnlyList<Shortcode> held)
    {
        var columns = 0;
        var at = 0;

        foreach (var shortcode in held)
        {
            columns += Columns(text[at..shortcode.At]) + CustomEmojiBox.Columns;
            at = shortcode.End;
        }

        return columns + Columns(text[at..]);
    }

    /// <summary>
    ///     The one of <paramref name="held" /> written at <paramref name="at" />, if one is, looked for from
    ///     <paramref name="next" /> on and leaving it at the first that has not been passed — so that a walk along the
    ///     text looks at each once.
    /// </summary>
    private static Shortcode? Held(IReadOnlyList<Shortcode>? held, int at, ref int next)
    {
        if (held is null)
        {
            return null;
        }

        while (next < held.Count && held[next].At < at)
        {
            next++;
        }

        return next < held.Count && held[next].At == at ? held[next] : null;
    }
}
