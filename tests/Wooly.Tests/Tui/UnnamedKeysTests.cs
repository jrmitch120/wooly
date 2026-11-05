using Terminal.Gui.Input;
using Wooly.Tui.Theme;
using Wooly.Tui.Views;

namespace Wooly.Tests.Tui;

/// <summary>
///     A key the terminal sends in words Terminal.Gui takes for a key but cannot name, read in as the running TUI reads
///     it: the main loop carries on past what Terminal.Gui throws for it, and reads the next key as it would have, while a
///     fault of wooly's own reached the same way still ends the session.
/// </summary>
public class UnnamedKeysTests
{
    /// <summary>
    ///     The Menu key as Windows Terminal sends it, F13, rxvt's Home and End, a paste's end with no start, <c>ESC O O</c>
    ///     and a kitty report of a code that is no character — each of which ended the session.
    /// </summary>
    [Theory]
    [InlineData("\u001b[29~")]
    [InlineData("\u001b[25~")]
    [InlineData("\u001b[7~")]
    [InlineData("\u001b[8~")]
    [InlineData("\u001b[201~")]
    [InlineData("\u001bOO")]
    [InlineData("\u001b[55296u")]
    public async Task TheLoopCarriesOnPastAKeyTerminalGuiCannotNameAndReadsTheNextOne(string sequence)
    {
        using var drawn = await DrawnShell.Of(80, 24, Themes.Plain, new AShell());

        var read = new List<Key>();
        drawn.Application.Keyboard.KeyDown += (_, key) => read.Add(key);

        var thrown = Record.Exception(() => drawn.Reports(sequence, "x"));

        Assert.NotNull(thrown);
        Assert.True(UnnamedKeys.CarryOn(thrown));

        // The loop's next pass, as Terminal.Gui's own makes one after its error handler says to carry on.
        drawn.Iterate();

        Assert.Equal([Key.X], read);
    }

    /// <summary>
    ///     A key Terminal.Gui did name, raised through the same place, whose handler throws: that is wooly's fault, and
    ///     ends the session where it is seen.
    /// </summary>
    [Fact]
    public async Task AFaultOfWoolysOwnHandlingANamedKeyIsNotCarriedOnPast()
    {
        using var drawn = await DrawnShell.Of(80, 24, Themes.Plain, new AShell());

        drawn.Application.Keyboard.KeyDown += (_, _) => throw new NullReferenceException();

        var thrown = Record.Exception(() => drawn.Reports("\u001b[2~"));

        Assert.IsType<NullReferenceException>(thrown);
        Assert.False(UnnamedKeys.CarryOn(thrown));
    }

    [Fact]
    public void ANullReferenceThrownAnywhereElseIsNotCarriedOnPast()
    {
        var thrown = Record.Exception(() => ((string?)null)!.Trim());

        Assert.False(UnnamedKeys.CarryOn(thrown!));
    }
}
