using Terminal.Gui.Drivers;
using Terminal.Gui.Input;
using Wooly.Tui.Screens;
using Wooly.Tui.Shell;
using Wooly.Tui.Theme;

namespace Wooly.Tests.Tui;

/// <summary>
///     The search field as a real one-line text field (#608), typed into through the whole shell drawn headless: a caret
///     that moves, a selection by the keys and by the mouse, copy, cut and paste — as compose's content warning has had
///     since #320. What has been typed is the field's, so it is read off the field, as compose's tests read theirs.
/// </summary>
public class ShellSearchFieldTests
{
    /// <summary>
    ///     <c>←</c> moves the caret back into what is typed, and the next letter lands there rather than at the end —
    ///     and <c>⏎</c> asks for the whole of it, wherever the caret was left.
    /// </summary>
    [Fact]
    public async Task ALetterLandsAtTheCaretAndEnterSearchesTheWholeField()
    {
        using var drawn = await Searching();

        drawn.Type("cts");
        drawn.Press(Key.CursorLeft);
        drawn.Press(Key.CursorLeft);
        drawn.Type("a");

        Assert.Equal("cats", drawn.SearchField.Text);

        drawn.Press(Key.Enter);
        drawn.Settle();

        Assert.Equal("cats", Assert.Single(drawn.Built.Search.Searches).Query.Text);
        Assert.IsType<SearchResultsScreen>(drawn.Shell.Screen);
    }

    /// <summary><c>home</c> and <c>end</c> take the caret to either end of what is typed.</summary>
    [Fact]
    public async Task HomeAndEndTakeTheCaretToEitherEnd()
    {
        using var drawn = await Searching();

        drawn.Type("at");
        drawn.Press(Key.Home);
        drawn.Type("c");
        drawn.Press(Key.End);
        drawn.Type("s");

        Assert.Equal("cats", drawn.SearchField.Text);
    }

    /// <summary>
    ///     The shift-arrows select, and what is typed next replaces the selection — and a backspace takes a selection out
    ///     whole, rather than the one character before the caret.
    /// </summary>
    [Fact]
    public async Task TypingReplacesASelectionAndBackspaceRemovesIt()
    {
        using var drawn = await Searching();

        drawn.Type("cats");
        drawn.Press(Key.CursorLeft.WithShift);
        drawn.Press(Key.CursorLeft.WithShift);
        drawn.Press(Key.CursorLeft.WithShift);

        Assert.Equal("ats", drawn.SearchField.SelectedText);

        drawn.Type("ow");

        Assert.Equal("cow", drawn.SearchField.Text);

        drawn.Press(Key.Home);
        drawn.Press(Key.CursorRight.WithShift);
        drawn.Press(Key.CursorRight.WithShift);
        drawn.Press(Key.Backspace);

        Assert.Equal("w", drawn.SearchField.Text);
    }

    /// <summary>
    ///     A drag of the mouse across what is typed selects it from the cell it began on — a terminal reporting the
    ///     pointer moving only once it has reached the next one — and typing replaces what was dragged over.
    /// </summary>
    [Fact]
    public async Task ADragSelectsAndTypingReplacesIt()
    {
        using var drawn = await Searching();

        drawn.Type("big cats");

        var at = drawn.SearchField.FrameToScreen();

        drawn.Point(at.X, at.Y, MouseFlags.LeftButtonPressed);

        for (var column = at.X + 1; column <= at.X + 3; column++)
        {
            drawn.Point(column, at.Y, MouseFlags.LeftButtonPressed | MouseFlags.PositionReport);
        }

        drawn.Point(at.X + 3, at.Y, MouseFlags.LeftButtonReleased);

        Assert.Equal("big", drawn.SearchField.SelectedText);

        drawn.Type("fat");

        Assert.Equal("fat cats", drawn.SearchField.Text);
    }

    /// <summary>
    ///     <c>/</c> and <c>?</c> are typed, a web address and a question being entitled to them; <c>\</c> still hides and
    ///     shows the rail, and <c>[</c> and <c>]</c> move between sections rather than being typed — none of the three is
    ///     ever part of a query.
    /// </summary>
    [Fact]
    public async Task TheFramesKeysAreTypedButForTheRailAndTheSections()
    {
        using var drawn = await Searching();

        drawn.Type("a/b?[c]");

        Assert.Equal("a/b?c", drawn.SearchField.Text);
        Assert.Equal(1, drawn.Shell.Depth);

        drawn.Type("\\");

        Assert.Equal("a/b?c", drawn.SearchField.Text);
        Assert.Equal(RailState.Hidden, drawn.Shell.RailState);

        drawn.Type("\\");

        Assert.Equal(RailState.Shown, drawn.Shell.RailState);
        Assert.Equal("a/b?c", drawn.SearchField.Text);
        Assert.True(drawn.SearchField.HasFocus);
    }

    /// <summary>
    ///     <c>ctrl-c</c> copies a selection and <c>ctrl-x</c> cuts it; <c>ctrl-v</c> pastes it at the caret, and so does
    ///     <c>alt-v</c>, the paste of the terminals that keep <c>ctrl-v</c> — the keys compose's fields use.
    /// </summary>
    [Fact]
    public async Task CopyCutAndPasteWorkByTheKeysComposesFieldsUse()
    {
        using var drawn = await Searching();

        // A clipboard of the headless terminal's own, so that nothing here reads or writes the machine's.
        drawn.Application.Driver!.Clipboard = new FakeClipboard();

        drawn.Type("cats");
        drawn.Press(Key.Home);
        drawn.Press(Key.CursorRight.WithShift);
        drawn.Press(Key.CursorRight.WithShift);
        drawn.Press(Key.C.WithCtrl);

        Assert.True(drawn.Application.Clipboard!.TryGetClipboardData(out var copied));
        Assert.Equal("ca", copied);
        Assert.Equal("cats", drawn.SearchField.Text);

        drawn.Press(Key.End);
        drawn.Press(Key.V.WithCtrl);

        Assert.Equal("catsca", drawn.SearchField.Text);

        drawn.Press(Key.V.WithAlt);

        Assert.Equal("catscaca", drawn.SearchField.Text);

        drawn.Press(Key.Home);
        drawn.Press(Key.CursorRight.WithShift);
        drawn.Press(Key.X.WithCtrl);

        Assert.Equal("atscaca", drawn.SearchField.Text);

        drawn.Press(Key.V.WithCtrl);

        Assert.Equal("catscaca", drawn.SearchField.Text);
    }

    /// <summary>
    ///     <c>ctrl-u</c> empties the field, wherever the caret was, and leaves it taking letters: an edit like a
    ///     backspace, which asks nothing of the instance and leaves the prompt where it is (#310).
    /// </summary>
    [Fact]
    public async Task CtrlUEmptiesTheFieldAndLeavesItTakingLetters()
    {
        using var drawn = await Searching();

        drawn.Type("big cats");
        drawn.Press(Key.CursorLeft);
        drawn.Press(Key.U.WithCtrl);

        Assert.Equal(string.Empty, drawn.SearchField.Text);
        Assert.True(drawn.SearchField.HasFocus);
        Assert.IsType<SearchScreen>(drawn.Shell.Screen);
        Assert.Equal(1, drawn.Shell.Depth);
        Assert.Empty(drawn.Built.Search.Searches);

        // The field's own emptying, so taken back as any edit of it is.
        drawn.Press(Key.Z.WithCtrl);

        Assert.Equal("big cats", drawn.SearchField.Text);

        drawn.Press(Key.U.WithCtrl);
        drawn.Type("dogs");

        Assert.Equal("dogs", drawn.SearchField.Text);
    }

    /// <summary>
    ///     The status row offers <c>Clear: ctrl-u</c> while the field holds something, and not while it is empty — a key
    ///     announced where it does nothing reads as a shell that missed the press (#310).
    /// </summary>
    [Fact]
    public async Task CtrlUIsOfferedOnlyWhileTheFieldHoldsSomething()
    {
        using var drawn = await Searching();

        Assert.DoesNotContain(drawn.Shell.Screen.Keys, key => key.Key == "ctrl-u");

        drawn.Type("c");
        drawn.Redraw();

        Assert.Contains(drawn.Shell.Screen.Keys, key => key is { Key: "ctrl-u", Does: "clear" });
        Assert.Contains("Clear: ctrl-u", drawn.Rows()[^1]);

        drawn.Press(Key.Backspace);
        drawn.Redraw();

        Assert.DoesNotContain(drawn.Shell.Screen.Keys, key => key.Key == "ctrl-u");
        Assert.DoesNotContain("ctrl-u", drawn.Rows()[^1]);

        drawn.Type("cats");
        drawn.Press(Key.U.WithCtrl);

        Assert.DoesNotContain(drawn.Shell.Screen.Keys, key => key.Key == "ctrl-u");
    }

    /// <summary>The whole shell, gone to search with <c>/</c>, the field taking letters.</summary>
    private static async Task<DrawnShell> Searching()
    {
        var drawn = await DrawnShell.Of(80, 24, Themes.Dark);

        drawn.Press(new Key('/'));
        drawn.Settle();

        return drawn;
    }
}
