using Wooly.Tui.Media;

namespace Wooly.Tests.Tui;

/// <summary>
///     A cell's real size in pixels, from what the kernel says of the terminal window (#292). Terminal.Gui's guess of
///     10×20 against Ghostty's real 16×34 stretched every photograph.
/// </summary>
public class WindowSizeTests
{
    /// <summary>The window's pixels over its cells, as Ghostty reported them for the prototype.</summary>
    [Fact]
    public void Divided_IsTheWindowsPixelsOverItsCells() =>
        Assert.Equal(new CellSize(16, 34), WindowSize.Divided(columns: 182, rows: 51, width: 2924, height: 1754));

    /// <summary>A terminal that fills in no pixels — most do not — says nothing, rather than a cell of no size.</summary>
    [Theory]
    [InlineData(80, 24, 0, 0)]
    [InlineData(0, 0, 800, 480)]
    [InlineData(80, 24, 40, 480)]
    public void Divided_IsNothingWhereTheWindowGivesNoPixels(int columns, int rows, int width, int height) =>
        Assert.Null(WindowSize.Divided(columns, rows, width, height));

    /// <summary>
    ///     Asked for real, of whatever the tests' output is — usually not a terminal at all. What matters is that it
    ///     answers rather than taking the process down: the plain declaration of <c>ioctl</c> crashed on Apple Silicon,
    ///     where a variadic argument goes on the stack.
    /// </summary>
    [Fact]
    public void Measure_AnswersWithoutCrashingWhateverTheOutputIs()
    {
        var cell = WindowSize.Measure();

        if (cell is { } measured)
        {
            Assert.True(measured.Width > 0 && measured.Height > 0);
        }
    }

    /// <summary>Measured once for a size of screen rather than once a post, and again when the screen changes size.</summary>
    [Fact]
    public void Cell_IsMeasuredAgainOnlyWhenTheScreenChangesSize()
    {
        var screen = (Columns: 80, Rows: 24);
        var measured = 0;
        var size = new WindowSize(() => screen, () => new CellSize(10, 20 + measured++));

        Assert.Equal(new CellSize(10, 20), size.Cell);
        Assert.Equal(new CellSize(10, 20), size.Cell);

        screen = (100, 30);

        Assert.Equal(new CellSize(10, 21), size.Cell);
        Assert.Equal(2, measured);
    }
}
