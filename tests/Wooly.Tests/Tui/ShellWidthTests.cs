using Terminal.Gui.Input;
using Wooly.Core.Configuration;
using Wooly.Tui.Theme;
using Attribute = Terminal.Gui.Drawing.Attribute;

namespace Wooly.Tests.Tui;

/// <summary>
///     The **Shell width** (#414): <c>-</c> narrows the shell a step, <c>+</c> and <c>=</c> widen it, and the shell is
///     drawn centred at that share of the terminal, never under 80 columns, with the terminal's own cells either side.
///     Pressed and read back off a headless terminal, since the promise is about where the cells land.
/// </summary>
public class ShellWidthTests
{
    [Fact]
    public async Task MinusNarrowsTheShellAStepCentredInTheTerminal()
    {
        using var drawn = await DrawnShell.Of(200, 24, Midnight());

        drawn.Press(new Key('-'));

        // 90% of 200 is 180, which leaves 20 to share: 10 either side.
        Assert.Equal((10, 180), Shell(drawn));
        Assert.Contains("Width 90%", drawn.Rows()[^1], StringComparison.Ordinal);
    }

    /// <summary><c>+</c> and <c>=</c> each widen a step back, saying where they landed.</summary>
    [Theory]
    [InlineData('+')]
    [InlineData('=')]
    public async Task PlusAndEqualsWidenTheShellAStep(char wider)
    {
        using var drawn = await DrawnShell.Of(200, 24, Midnight());

        drawn.Press(new Key('-'));
        drawn.Press(new Key('-'));
        drawn.Press(new Key(wider));

        Assert.Equal((10, 180), Shell(drawn));
        Assert.Contains("Width 90%", drawn.Rows()[^1], StringComparison.Ordinal);

        drawn.Press(new Key(wider));

        Assert.Equal((0, 200), Shell(drawn));
        Assert.Contains("Width 100%", drawn.Rows()[^1], StringComparison.Ordinal);
    }

    /// <summary>At the whole terminal there is no wider, and <c>+</c> says so rather than doing nothing silently.</summary>
    [Fact]
    public async Task PlusAtTheWholeTerminalSaysFullWidth()
    {
        using var drawn = await DrawnShell.Of(200, 24, Midnight());

        drawn.Press(new Key('+'));

        Assert.Equal((0, 200), Shell(drawn));
        Assert.Contains("Full width", drawn.Rows()[^1], StringComparison.Ordinal);
    }

    /// <summary>Half the terminal is the narrowest step, and <c>-</c> there says so and leaves it.</summary>
    [Fact]
    public async Task MinusAtHalfTheTerminalSaysNarrowest()
    {
        using var drawn = await DrawnShell.Of(200, 24, Midnight());

        foreach (var percent in new[] { 90, 80, 70, 60, 50 })
        {
            drawn.Press(new Key('-'));

            Assert.Contains($"Width {percent}%", drawn.Rows()[^1], StringComparison.Ordinal);
        }

        drawn.Press(new Key('-'));

        Assert.Equal((50, 100), Shell(drawn));
        Assert.Contains("Narrowest", drawn.Rows()[^1], StringComparison.Ordinal);
    }

    /// <summary>
    ///     Nor is the shell ever narrowed under the 80 columns it is laid out for: on a 120-column terminal 70% is 84
    ///     columns and 60% would be 72, so <c>-</c> stops at 70% and says so.
    /// </summary>
    [Fact]
    public async Task MinusThatWouldGoUnderEightyColumnsSaysNarrowest()
    {
        using var drawn = await DrawnShell.Of(120, 24, Midnight());

        drawn.Press(new Key('-'));
        drawn.Press(new Key('-'));
        drawn.Press(new Key('-'));

        Assert.Equal((18, 84), Shell(drawn));

        drawn.Press(new Key('-'));

        Assert.Equal((18, 84), Shell(drawn));
        Assert.Contains("Narrowest", drawn.Rows()[^1], StringComparison.Ordinal);
    }

    /// <summary>On a terminal 80 columns wide there is no room for less, so the shell is the whole of it.</summary>
    [Fact]
    public async Task OnAnEightyColumnTerminalMinusSaysNarrowest()
    {
        using var drawn = await DrawnShell.Of(80, 24, Midnight());

        drawn.Press(new Key('-'));

        Assert.Equal((0, 80), Shell(drawn));
        Assert.Contains("Narrowest", drawn.Rows()[^1], StringComparison.Ordinal);
    }

    /// <summary>
    ///     The margins are the terminal's: blank, at its default attribute, under a theme with a page of its own — no
    ///     role, so no theme entry, painted them. An odd column left over falls on the right, so the left edge moves only
    ///     when the width does.
    /// </summary>
    [Fact]
    public async Task TheMarginsAreLeftAtTheTerminalsDefaultWithTheSpareColumnOnTheRight()
    {
        using var drawn = await DrawnShell.Of(201, 24, Midnight());

        drawn.Press(new Key('-'));

        // 90% of 201 is 180.9, so 180 columns, and 21 to share: 10 on the left, 11 on the right.
        Assert.Equal((10, 180), Shell(drawn));
        AssertMargins(drawn, left: 10, right: 11);
    }

    /// <summary>
    ///     And as the running loop draws a press, onto the frame before rather than a cleared screen: what the shell drew
    ///     where the margins now are is gone, not left behind beside it.
    /// </summary>
    [Fact]
    public async Task NarrowingAsTheLoopDrawsLeavesNothingOfTheWiderShellInTheMargins()
    {
        using var drawn = await DrawnShell.Of(200, 24, Midnight());

        drawn.PressAsTheLoopDraws(new Key('-'));

        Assert.Equal((10, 180), Shell(drawn));
        AssertMargins(drawn, left: 10, right: 10);
    }

    /// <summary>
    ///     Where the shell is drawn, as its first column and how many columns it takes: from the rail's top-left corner to
    ///     the content panel's top-right one.
    /// </summary>
    private static (int Left, int Columns) Shell(DrawnShell drawn)
    {
        var top = drawn.Rows()[0];
        var left = top.IndexOf('╭', StringComparison.Ordinal);
        var right = top.LastIndexOf('╮');

        return (left, right - left + 1);
    }

    /// <summary>
    ///     Every cell of every row in the <paramref name="left" /> columns at the start and the <paramref name="right" /> at
    ///     the end is blank and at the terminal's default attribute — and the columns just inside them are the shell's.
    /// </summary>
    private static void AssertMargins(DrawnShell drawn, int left, int right)
    {
        var rows = drawn.Rows();
        var width = rows[0].Length;
        var page = Midnight().For(Role.Body);

        for (var row = 0; row < rows.Length; row++)
        {
            foreach (var column in Enumerable.Range(0, left).Concat(Enumerable.Range(width - right, right)))
            {
                Assert.Equal(' ', rows[row][column]);
                Assert.Equal(Attribute.Default, drawn.Cell(row, column));
            }

            Assert.Equal(page.Background, drawn.Cell(row, left).Background);
            Assert.Equal(page.Background, drawn.Cell(row, width - right - 1).Background);
        }
    }

    /// <summary>A theme with a page of its own, so that a cell the shell drew is told from one the terminal kept.</summary>
    private static ITheme Midnight() => Themes.Chosen(
        new WoolyConfig
        {
            Theme = "midnight",
            Themes = new Dictionary<string, ThemeConfig> { ["midnight"] = new() { Background = "#12111a" } },
        },
        "/somewhere/config.toml");
}
