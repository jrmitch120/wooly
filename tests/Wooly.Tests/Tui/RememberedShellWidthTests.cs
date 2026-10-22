using Terminal.Gui.Input;
using static Wooly.Tests.Tui.ShellWidthTests;

namespace Wooly.Tests.Tui;

/// <summary>
///     The **Shell width** remembered (#415): each step that lands saves it, a refused one saves nothing, and a launch
///     opens at what was saved — written by hand or not. Pressed and read back off a headless terminal, with what was
///     saved read off the port the shell saves it through.
/// </summary>
public class RememberedShellWidthTests
{
    [Fact]
    public async Task EachStepThatLandsSavesTheWidthItLandedOn()
    {
        var built = new AShell();
        using var drawn = await DrawnShell.Of(200, 24, Midnight(), built);

        drawn.Press(new Key('-'));

        Assert.Equal([90], built.ShellWidths.Saved);

        drawn.Press(new Key('-'));
        drawn.Press(new Key('+'));
        drawn.Press(new Key('='));

        Assert.Equal([90, 80, 90, 100], built.ShellWidths.Saved);
    }

    /// <summary>
    ///     <c>+</c> at the whole terminal and <c>-</c> where the next step would go under 80 columns are refused, and save
    ///     nothing: what is saved is always a width that was seen.
    /// </summary>
    [Fact]
    public async Task ARefusedStepSavesNothing()
    {
        var built = new AShell();
        using var drawn = await DrawnShell.Of(80, 24, Midnight(), built);

        drawn.Press(new Key('+'));

        Assert.Contains("Full width", drawn.Rows()[^1], StringComparison.Ordinal);

        drawn.Press(new Key('-'));

        Assert.Contains("Narrowest", drawn.Rows()[^1], StringComparison.Ordinal);
        Assert.Empty(built.ShellWidths.Saved);
    }

    [Fact]
    public async Task ALaunchOpensAtTheSavedWidth()
    {
        using var drawn = await DrawnShell.Of(200, 24, Midnight(), new AShell { ShellWidth = 70 });

        // 70% of 200 is 140, which leaves 60 to share: 30 either side.
        Assert.Equal((30, 140), Shell(drawn));
    }
}
