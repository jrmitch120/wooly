using Terminal.Gui.Input;
using Wooly.Tui.Screens;
using Wooly.Tui.Theme;

namespace Wooly.Tests.Tui;

/// <summary>
///     <c>ctrl-u</c> as the terminal sends it, through the whole shell drawn headless (#310): it empties a prompt the
///     shell types into itself, ahead of the keymap — and where nothing is taking letters it does nothing at all. The
///     search field and compose's one-line fields empty themselves on it (<c>ShellSearchFieldTests</c>,
///     <c>ComposeWarningFieldTests</c>, <c>ComposeLangTests</c>).
/// </summary>
public class PromptClearingTests
{
    /// <summary>
    ///     The add screen's instance field, typed into by the shell rather than a widget, is emptied by the key pressed
    ///     in the window, and goes on taking letters.
    /// </summary>
    [Fact]
    public async Task CtrlUEmptiesAPromptTheShellTypesInto()
    {
        using var drawn = await DrawnShell.Of(80, 24, Themes.Dark);

        drawn.Press(Key.P.WithCtrl);
        drawn.Press(new Key('a'));
        drawn.Type("hachyderm.io");

        var adding = Assert.IsType<AddProfileScreen>(drawn.Shell.Screen);

        Assert.Equal("hachyderm.io", adding.Instance);

        drawn.Press(Key.U.WithCtrl);

        Assert.Same(adding, drawn.Shell.Screen);
        Assert.Equal(string.Empty, adding.Instance);

        drawn.Type("mastodon.social");

        Assert.Equal("mastodon.social", adding.Instance);
    }

    /// <summary>Where nothing is taking letters, the key does nothing: no screen moves, nothing is asked or said.</summary>
    [Fact]
    public async Task CtrlUDoesNothingWhereNoPromptIsTakingLetters()
    {
        using var drawn = await DrawnShell.Of(80, 24, Themes.Dark);

        var screen = drawn.Shell.Screen;
        var depth = drawn.Shell.Depth;
        var asked = drawn.Built.Requests;
        var rows = drawn.Rows();

        drawn.Press(Key.U.WithCtrl);
        drawn.Settle();

        Assert.Same(screen, drawn.Shell.Screen);
        Assert.Equal(depth, drawn.Shell.Depth);
        Assert.Equal(asked, drawn.Built.Requests);
        Assert.Null(drawn.Shell.Notice);
        Assert.Equal(rows, drawn.Rows());
    }
}
