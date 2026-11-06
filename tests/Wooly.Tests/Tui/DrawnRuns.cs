using System.Globalization;
using System.Text;
using Terminal.Gui.Drawing;
using Terminal.Gui.Text;
using Terminal.Gui.ViewBase;
using Wooly.Tui.Theme;

namespace Wooly.Tests.Tui;

/// <summary>
///     What a drawn editor's cells say, read a run of text in a role at a time — shared by the tests of an editor that
///     paints the references written in it (#571).
/// </summary>
internal static class DrawnRuns
{
    /// <summary>
    ///     The cells of <paramref name="field" />'s <paramref name="row" />, from its first column, are each run of text
    ///     in its role in <paramref name="theme" />, in turn.
    /// </summary>
    /// <remarks>
    ///     A run is read a grapheme at a time, not a <see langword="char" /> at a time: a wide one, an emoji or a joined
    ///     sequence, takes the two columns it is drawn across, and a letter typed with a combining accent is the one the
    ///     terminal draws, composed or not.
    /// </remarks>
    public static void AssertDrawn(
        Cell[,] contents,
        View field,
        int row,
        ITheme theme,
        params (string Text, Role Role)[] runs)
    {
        var at = field.FrameToScreen();
        var y = at.Y + row;
        var column = at.X;

        foreach (var (text, role) in runs)
        {
            var letters = StringInfo.GetTextElementEnumerator(text);

            while (letters.MoveNext())
            {
                var letter = letters.GetTextElement();
                var cell = contents[y, column];

                Assert.True(
                    Composed(letter) == Composed(cell.Grapheme),
                    $"column {column - at.X} of row {row} reads '{cell.Grapheme}', not '{letter}' of \"{text}\"");
                Assert.True(
                    theme.For(role) == cell.Attribute,
                    $"'{letter}' at column {column - at.X} of row {row} is not {role}");

                column += Math.Max(1, letter.GetColumns());
            }
        }
    }

    private static string Composed(string letter) => letter.Normalize(NormalizationForm.FormC);
}
