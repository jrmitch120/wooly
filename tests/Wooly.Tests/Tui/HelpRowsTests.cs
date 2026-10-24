using Terminal.Gui.Input;
using Wooly.Tui.Theme;

namespace Wooly.Tests.Tui;

/// <summary>
///     The <c>?</c> screen's rows as the shell draws them (#446): the mouse an action a row, its input in the key
///     column as every key's is, and a meaning too long for the panel carried on under its own column rather than cut.
/// </summary>
public class HelpRowsTests
{
    /// <summary>Tall enough that the whole of help is on the screen at once.</summary>
    private const int Tall = 80;

    /// <summary>
    ///     At 80 columns with the rail shown, the mouse is six rows of the <i>Everywhere</i> block, one action each,
    ///     read from the key column to the panel's right edge.
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

    /// <summary>
    ///     At 80 columns with the rail shown, the panel leaves the meaning column 42 wide, so <c>tab / shift-tab</c>'s
    ///     meaning carries on to the row under it, starting in the meaning column, rather than ending in <c>…</c>.
    /// </summary>
    [Fact]
    public async Task AMeaningTooLongForThePanelWrapsUnderItsColumn()
    {
        using var drawn = await DrawnShell.Of(80, Tall, Themes.Plain);

        drawn.Press(new Key('?'));

        var rows = drawn.Rows();
        var tab = Array.FindIndex(rows, row => row.Contains("│tab / shift-tab ", StringComparison.Ordinal));

        Assert.NotEqual(-1, tab);

        var column = rows[tab].IndexOf("tab / shift-tab", StringComparison.Ordinal);

        Assert.Equal(
            [
                "tab / shift-tab move the rail's cursor; it settles onto a",
                "                destination",
                "` / ~           move it to the next or previous rail",
                "                group's first destination",
            ],
            rows[tab..(tab + 4)].Select(row => row[column..].TrimEnd().TrimEnd('│').TrimEnd()));
    }

    /// <summary>
    ///     Nowhere on the content panel is anything of help's cut short, at the narrowest terminal the shell takes and
    ///     wider, with the rail shown or hidden.
    /// </summary>
    [Theory]
    [InlineData(80, false)]
    [InlineData(80, true)]
    [InlineData(100, false)]
    [InlineData(100, true)]
    [InlineData(160, false)]
    [InlineData(160, true)]
    public async Task NothingOnHelpIsClipped(int width, bool hidden)
    {
        using var drawn = await DrawnShell.Of(width, Tall, Themes.Plain);

        if (hidden)
        {
            drawn.Press(new Key('\\'));
        }

        drawn.Press(new Key('?'));

        var rows = drawn.Rows();
        var panel = drawn.Window.Frame.X + drawn.Content.Frame.X;

        // The status row is the shell's rather than help's, and the rail's entries are cut as the rail cuts them.
        Assert.Contains(rows, row => row.Contains("Everywhere", StringComparison.Ordinal));
        Assert.DoesNotContain(rows[..^1], row => row[panel..].Contains('…'));
    }

    /// <summary>
    ///     Nor inside a narrowed shell, whose content panel is narrower than the terminal: 160 narrowed to its
    ///     narrowest, 80 columns, and 200 narrowed to half, with the rail shown or hidden.
    /// </summary>
    [Theory]
    [InlineData(160, 5, false)]
    [InlineData(160, 5, true)]
    [InlineData(200, 5, false)]
    [InlineData(200, 5, true)]
    public async Task NothingOnHelpIsClippedInANarrowedShell(int width, int narrowed, bool hidden)
    {
        using var drawn = await DrawnShell.Of(width, Tall, Themes.Plain);

        for (var step = 0; step < narrowed; step++)
        {
            drawn.Press(new Key('-'));
        }

        if (hidden)
        {
            drawn.Press(new Key('\\'));
        }

        drawn.Press(new Key('?'));

        var rows = drawn.Rows();
        var panel = drawn.Window.Frame.X + drawn.Content.Frame.X;

        Assert.Equal(width / 2, drawn.Window.Frame.Width);
        Assert.Contains(rows, row => row.Contains("Everywhere", StringComparison.Ordinal));
        Assert.DoesNotContain(rows[..^1], row => row[panel..].Contains('…'));
    }

    /// <summary>
    ///     Help is taller than an 80 by 16 terminal, and <c>↓</c> reads down it to its last row, the mouse's last
    ///     action, rather than stopping short of it.
    /// </summary>
    [Fact]
    public async Task HelpScrollsToItsLastRow()
    {
        using var drawn = await DrawnShell.Of(80, 16, Themes.Plain);

        drawn.Press(new Key('?'));

        Assert.DoesNotContain(drawn.Rows(), row => row.Contains("click «/»", StringComparison.Ordinal));

        for (var row = 0; row < Tall; row++)
        {
            drawn.Press(Key.CursorDown);
        }

        Assert.Contains(
            drawn.Rows(),
            row => row.Contains("click «/»       hide or show the rail", StringComparison.Ordinal));
    }
}
