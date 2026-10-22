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

    /// <summary>
    ///     A width written by hand between the steps is drawn as written, and <c>-</c> and <c>+</c> bring it back onto
    ///     them: 73 narrows to 70 and widens to 80.
    /// </summary>
    [Theory]
    [InlineData('-', 70, 30, 140)]
    [InlineData('+', 80, 20, 160)]
    public async Task AnInBetweenWidthIsDrawnAsWritten_AndAStepTakesItBackOntoTheSteps(
        char key,
        int percent,
        int left,
        int columns)
    {
        var built = new AShell { ShellWidth = 73 };
        using var drawn = await DrawnShell.Of(200, 24, Midnight(), built);

        // 73% of 200 is 146, which leaves 54 to share: 27 either side.
        Assert.Equal((27, 146), Shell(drawn));

        drawn.Press(new Key(key));

        Assert.Equal((left, columns), Shell(drawn));
        Assert.Contains($"Width {percent}%", drawn.Rows()[^1], StringComparison.Ordinal);
        Assert.Equal([percent], built.ShellWidths.Saved);
    }

    /// <summary>
    ///     Half of a 100-column terminal is under the 80 columns the shell is laid out for, so it is drawn at 80 — but
    ///     the saved half is left as it is, and is what a wider terminal draws.
    /// </summary>
    [Fact]
    public async Task ASavedWidthTheFloorOverridesIsLeftAsSaved()
    {
        var built = new AShell { ShellWidth = 50 };
        using var drawn = await DrawnShell.Of(100, 24, Midnight(), built);

        Assert.Equal((10, 80), Shell(drawn));

        drawn.Resize(200, 24);

        Assert.Equal((50, 100), Shell(drawn));
        Assert.Empty(built.ShellWidths.Saved);
    }

    /// <summary>
    ///     A config file edited by hand, while the TUI runs, into something that no longer reads cannot be saved to. The
    ///     status row says why, as <c>D</c> does, and the step still holds for this session, since it was seen.
    /// </summary>
    [Fact]
    public async Task AStepThatCannotBeSavedSaysWhy_AndStillHoldsForTheSession()
    {
        var built = new AShell();
        built.ShellWidths.Unreadable = "'bell' must be true or false.";
        using var drawn = await DrawnShell.Of(200, 24, Midnight(), built);

        drawn.Press(new Key('-'));

        Assert.Equal((10, 180), Shell(drawn));
        Assert.Contains("'bell' must be true or false.", drawn.Shell.Notice, StringComparison.Ordinal);
        Assert.True(drawn.Shell.NoticeIsError);
    }
}
