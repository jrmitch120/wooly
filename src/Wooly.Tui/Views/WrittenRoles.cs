using System.Globalization;
using Terminal.Gui.Drawing;
using Wooly.Tui.Rendering;
using Wooly.Tui.Theme;

namespace Wooly.Tui.Views;

/// <summary>
///     The role each letter an editor draws is painted in where it is written in a reference (#571): found on the whole
///     text, by the rule a post's text is read by (<see cref="BodyText.References" />), and laid on the rows the editor
///     wrapped it to.
/// </summary>
/// <remarks>
///     The references are found on the whole text, as a post's are (#83), not a row at a time, which is what keeps an
///     address wrapped across rows one address.
/// </remarks>
internal sealed class WrittenRoles
{
    private readonly Role?[][] _rows;

    private WrittenRoles(Role?[][] rows) => _rows = rows;

    /// <summary>Nothing painted: every letter is drawn in the role the editor would draw it in.</summary>
    public static WrittenRoles None { get; } = new([]);

    /// <summary>
    ///     The role of the letter drawn at <paramref name="column" /> of <paramref name="row" /> — rows and columns of
    ///     the wrapped text, from its first — where it is in a reference, and <see langword="null" /> where it is not,
    ///     or where nothing is drawn.
    /// </summary>
    public Role? At(int row, int column) =>
        row >= 0 && row < _rows.Length && column >= 0 && column < _rows[row].Length ? _rows[row][column] : null;

    /// <summary>
    ///     The roles of the letters on each of <paramref name="rows" /> — the editor's rows, wrapped where it wraps,
    ///     a grapheme to a cell — from the references found in the whole of <paramref name="text" />.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         A row's letters are found in the text in turn, stepping over whatever whitespace the wrap took up
    ///         between them, since the rows are all the editor says about where it broke the text. Whitespace is in
    ///         no reference, so a space the wrap kept or dropped can never put a letter of one out of place.
    ///     </para>
    ///     <para>
    ///         A letter drawn otherwise than it is written — a letter typed with a combining accent, which the editor
    ///         draws composed — is still the next letter of the text, and is taken as that. So every letter drawn moves
    ///         along the text, and one the text spells differently never leaves the rest of the post unpainted.
    ///     </para>
    /// </remarks>
    public static WrittenRoles Of(string text, List<List<Cell>> rows)
    {
        var written = new Role?[text.Length];

        foreach (var reference in BodyText.References(text))
        {
            Array.Fill(written, reference.Role, reference.At, reference.Text.Length);
        }

        var drawn = new Role?[rows.Count][];
        var at = 0;

        for (var row = 0; row < rows.Count; row++)
        {
            drawn[row] = new Role?[rows[row].Count];

            for (var column = 0; column < rows[row].Count; column++)
            {
                var letter = rows[row][column].Grapheme;

                if (letter.Length == 0)
                {
                    continue;
                }

                while (at < text.Length && char.IsWhiteSpace(text[at]) && !SpelledAt(text, at, letter))
                {
                    at++;
                }

                if (at >= text.Length)
                {
                    return new WrittenRoles(drawn);
                }

                if (SpelledAt(text, at, letter))
                {
                    drawn[row][column] = written[at];
                    at += letter.Length;
                }
                else if (!string.IsNullOrWhiteSpace(letter))
                {
                    drawn[row][column] = written[at];
                    at += StringInfo.GetNextTextElementLength(text, at);
                }
            }
        }

        return new WrittenRoles(drawn);
    }

    /// <summary>Whether <paramref name="text" />, from <paramref name="at" />, spells <paramref name="letter" /> exactly.</summary>
    private static bool SpelledAt(string text, int at, string letter) =>
        at + letter.Length <= text.Length && string.CompareOrdinal(text, at, letter, 0, letter.Length) == 0;
}
