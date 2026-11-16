using Terminal.Gui.Input;

namespace Wooly.Tests.Tui;

/// <summary>
///     An edit made with something selected in the post, or just after, leaves it whole. <see cref="Terminal.Gui.Views.TextView" />
///     reads its selection as the text changes, part way through a paste over it or an undo, while its wrapped rows are
///     still laid out as they were before the edit — and where the text left was shorter, read past the end of a line
///     and threw, taking Wooly down. Deleting a selection leaves the rows to lay out again later, and a paste before
///     that put the caret on the wrong line or off the text, from which selecting, or the paste itself, threw too.
/// </summary>
public class ComposeEditorPasteTests
{
    [Fact]
    public async Task APasteOfLinesOverEverythingSelected_TakesItsPlace()
    {
        using var composed = await ComposedView.Of();

        composed.Type("a meadow full of sheep");
        composed.Press(Key.A.WithCtrl);
        composed.Paste("baa\nbaa");

        Assert.Equal("baa\nbaa", composed.Editor.Text.ReplaceLineEndings("\n"));
    }

    [Fact]
    public async Task AnUndoWithSomethingSelected_TakesBackTheLastLetterTyped()
    {
        using var composed = await ComposedView.Of();

        composed.Type("sheep");
        composed.Press(Key.Home.WithShift);
        composed.Press(Key.Z.WithCtrl);

        Assert.Equal("shee", composed.Editor.Text);
    }

    [Fact]
    public async Task ALineBreakPastedAfterDeletingASelection_LeavesTheCaretBeforeWhatFollows()
    {
        using var composed = await ComposedView.Of();

        DeletedASelectionThenPastedALineBreak(composed);
        composed.Type("x");

        Assert.Equal("\nxt", composed.Editor.Text.ReplaceLineEndings("\n"));
    }

    [Fact]
    public async Task ALineBreakPastedAfterDeletingASelection_IsWhatSelectingBackTakes()
    {
        using var composed = await ComposedView.Of();

        DeletedASelectionThenPastedALineBreak(composed);
        composed.Press(Key.CursorLeft.WithShift);
        composed.Press(Key.Delete);

        Assert.Equal("t", composed.Editor.Text);
    }

    [Fact]
    public async Task APasteOfLinesAfterDeletingTheLastLetterOfALineThatWraps_GoesInWhereItWas()
    {
        using var composed = await ComposedView.Of();

        composed.Paste(Wrapping);
        composed.Press(Key.CursorLeft.WithShift);
        composed.Press(Key.Backspace);
        composed.Paste("x\ny");

        Assert.Equal(Wrapping[..^1] + "x\ny", composed.Editor.Text.ReplaceLineEndings("\n"));
    }

    /// <summary>A line wider than the post, which wraps onto rows of its own below.</summary>
    private const string Wrapping =
        "a meadow full of sheep, every one of them grazing, and all of them asking the same thing of the one beside it";

    /// <summary><c>abt</c> written, <c>ab</c> selected and deleted, and a line break pasted in front of the <c>t</c> left.</summary>
    private static void DeletedASelectionThenPastedALineBreak(ComposedView composed)
    {
        composed.Type("abt");
        composed.Press(Key.CursorLeft);
        composed.Press(Key.Home.WithShift);
        composed.Press(Key.Delete);
        composed.Paste("\n");
    }
}
