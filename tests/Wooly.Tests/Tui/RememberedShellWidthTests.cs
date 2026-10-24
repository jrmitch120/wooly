using Terminal.Gui.Input;
using Wooly.Tests.Fakes;
using static Wooly.Tests.Tui.ShellWidthTests;

namespace Wooly.Tests.Tui;

/// <summary>
///     The <b>Shell width</b> remembered (#415): each step that lands saves it, a refused one saves nothing, and a
///     launch opens at what was saved — written by hand or not. Pressed and read back off a headless terminal, with
///     what was saved read off the port the shell saves it through.
/// </summary>
public class RememberedShellWidthTests
{
    [Fact]
    public async Task EachStepThatLandsSavesTheWidthItLandedOn()
    {
        var built = new AShell();
        using var drawn = await DrawnShell.Of(200, 24, Midnight(), built);

        drawn.Press(new Key('-'));

        Assert.Equal([90], built.ShellPreferences.Widths);

        drawn.Press(new Key('-'));
        drawn.Press(new Key('+'));
        drawn.Press(new Key('='));

        Assert.Equal([90, 80, 90, 100], built.ShellPreferences.Widths);
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
        Assert.Empty(built.ShellPreferences.Widths);
    }

    [Fact]
    public async Task ALaunchOpensAtTheSavedWidth()
    {
        using var drawn = await DrawnShell.Of(200, 24, Midnight(), new AShell { ShellWidth = 70 });

        // 70% of 200 is 140, which leaves 60 to share: 30 either side.
        Assert.Equal((30, 140), Shell(drawn));
    }

    /// <summary>
    ///     The first-run screen too: with nobody to act as, a launch opens on adding a profile with the rail hidden, and
    ///     draws it centred at the saved width, the add screen's panel taking all of the shell — and saves nothing, as
    ///     opening at a width is not a step.
    /// </summary>
    [Fact]
    public async Task AFirstRunLaunchOpensAtTheSavedWidth()
    {
        var built = new AShell { ShellWidth = 70, Profiles = FakeProfileRegistry.Holding(current: null) };
        using var drawn = await DrawnShell.Of(200, 24, Midnight(), built, launch: true);

        Assert.False(drawn.Shell.ShowsRail);
        Assert.Equal((30, 140), Shell(drawn));
        Assert.StartsWith("╭ Add a profile ─", drawn.Rows()[0][30..], StringComparison.Ordinal);
        Assert.Equal(new string(' ', 30), drawn.Rows()[0][..30]);
        Assert.Equal(new string(' ', 30), drawn.Rows()[0][170..]);
        Assert.Empty(built.ShellPreferences.Widths);
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
        Assert.Equal([percent], built.ShellPreferences.Widths);
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
        Assert.Empty(built.ShellPreferences.Widths);
    }

    /// <summary>
    ///     A config file edited by hand, while the TUI runs, into something that no longer reads cannot be saved to. The
    ///     status row says why, as <c>D</c> does, and the step still holds for this session, since it was seen.
    /// </summary>
    [Fact]
    public async Task AStepThatCannotBeSavedSaysWhy_AndStillHoldsForTheSession()
    {
        var built = new AShell();
        built.ShellPreferences.Unreadable = "'bell' must be true or false.";
        using var drawn = await DrawnShell.Of(200, 24, Midnight(), built);

        drawn.Press(new Key('-'));

        Assert.Equal((10, 180), Shell(drawn));
        Assert.Contains("'bell' must be true or false.", drawn.Shell.Notice, StringComparison.Ordinal);
        Assert.True(drawn.Shell.NoticeIsError);
    }

    /// <summary>
    ///     Why a step could not be saved is a failure, and stays until something else is said — not cleared by the wait
    ///     an earlier step's width was said for.
    /// </summary>
    [Fact]
    public async Task WhyAStepCouldNotBeSaved_OutlastsTheWaitAnEarlierWidthWasSaidFor()
    {
        var built = new AShell();
        using var drawn = await DrawnShell.Of(200, 24, Midnight(), built);

        drawn.Press(new Key('-'));
        built.ShellPreferences.Unreadable = "'bell' must be true or false.";
        drawn.Press(new Key('-'));

        built.Host.SettleAll();
        drawn.Redraw();

        Assert.Contains("'bell' must be true or false.", drawn.Shell.Notice, StringComparison.Ordinal);
        Assert.True(drawn.Shell.NoticeIsError);
    }
}
