using Terminal.Gui.Input;
using Wooly.Tui.Theme;

namespace Wooly.Tests.Tui;

/// <summary>
///     The <c>?</c> screen's rows as the shell draws them (#446): the mouse an action a row, its input in the key column
///     as every key's is, and a meaning too long for the panel carried on under its own column rather than cut.
/// </summary>
public class HelpRowsTests
{
    /// <summary>Tall enough that the whole of help is on the screen at once.</summary>
    private const int Tall = 80;

    /// <summary>
    ///     At 80 columns with the rail shown, the mouse is six rows of the <i>Everywhere</i> block, one action each, read
    ///     from the key column to the panel's right edge.
    /// </summary>
    [Fact]
    public async Task TheMouseIsOneActionARowWithItsInputInTheKeyColumn()
    {
        using var drawn = await DrawnShell.Of(80, Tall, Themes.Plain);

        drawn.Press(new Key('?'));

        var rows = drawn.Rows();
        var wheel = Array.FindIndex(rows, row => row.Contains("│wheel ", StringComparison.Ordinal));
        Assert.NotEqual(-1, wheel);

        var column = rows[wheel].IndexOf("wheel", StringComparison.Ordinal);

        Assert.Equal(
            [
                "wheel           scroll the content",
                "click           arrive on the rail; pick a row",
                "double click    read (⏎)",
                "right click     up one level, as esc",
                "click a crumb   walk back to it",
                "click «/»       hide or show the rail",
            ],
            rows[wheel..(wheel + 6)].Select(row => row[column..].TrimEnd().TrimEnd('│').TrimEnd()));
        Assert.DoesNotContain(rows, row => row.Contains("│mouse ", StringComparison.Ordinal));
    }
}
