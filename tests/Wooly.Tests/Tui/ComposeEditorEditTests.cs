using Terminal.Gui.Input;
using Wooly.Core.Accounts;
using Wooly.Tests.Fakes;
using Wooly.Tui.Screens;
using Wooly.Tui.Shell;
using Wooly.Tui.Theme;
using Wooly.Tui.Views;

namespace Wooly.Tests.Tui;

/// <summary>
///     An edit made with something selected in the post, or just after, leaves it whole. <see cref="Terminal.Gui.Views.TextView" />
///     reads its selection as the text changes, part way through a paste over it or an undo, while its wrapped rows are
///     still laid out as they were before the edit — and where the text left was shorter, read past the end of a line
///     and threw, taking Wooly down. Deleting a selection leaves the rows to lay out again later, and a paste before
///     that put the caret on the wrong line or off the text, from which selecting threw too; a click or a <c>↓</c>
///     before then was counted against the wrong rows.
/// </summary>
public class ComposeEditorEditTests
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

    [Fact]
    public async Task APasteOverASelectionInAPostScrolledDown_LeavesItWhereItWas()
    {
        using var composed = await ComposedView.Of();
        var editor = composed.Editor;

        composed.Paste(string.Join(" ", Enumerable.Repeat(Wrapping, 8)));
        composed.Press(Key.CursorLeft);
        composed.Press(Key.CursorRight);

        var scrolled = editor.Viewport.Y;

        composed.Press(Key.CursorLeft.WithShift);
        composed.Paste("z");

        Assert.NotEqual(0, scrolled);
        Assert.Equal(scrolled, editor.Viewport.Y);
    }

    [Fact]
    public async Task AClickAfterDeletingASelection_PutsTheCaretWhereItLands()
    {
        using var composed = await ComposedView.Of();
        var editor = composed.Editor;

        composed.Paste(Wrapping);
        composed.Press(Key.CursorLeft.WithShift);
        composed.Press(Key.Backspace);

        var at = editor.FrameToScreen();

        composed.Click(at.X + 3, at.Y + 1);

        Assert.Equal((1, 3), (editor.CurrentRow, editor.CurrentColumn));
    }

    [Fact]
    public async Task DownAfterDeletingASelectionOnTheBiosFirstRow_GoesToItsNextRow()
    {
        var me = AnAccount.With(address: "jeff@mastodon.social", author: "Jeff", id: "7");
        using var drawn = await DrawnShell.Of(80, 30, Themes.Plain, new AShell
        {
            Accounts = FakeAccountRelationships.Holding(me),
            PublicProfile = FakePublicProfileEditor.Holding(APublicProfile.With(bio: Wrapping, account: me)),
        });

        drawn.Shell.Rail.GoTo(drawn.Shell.Rail.Of(DestinationKind.Profile));
        drawn.Settle();
        drawn.Press(new Key('E'));
        drawn.Settle();

        var form = Assert.IsType<PublicProfileScreen>(drawn.Shell.Screen);

        while (form.Typing.Part != PublicProfilePart.Bio)
        {
            drawn.Press(Key.CursorDown);
        }

        var bio = drawn.Window.ProfileField<ComposeEditor>();

        drawn.Press(Key.Home.WithCtrl);
        drawn.Press(Key.CursorRight.WithShift);
        drawn.Press(Key.Delete);
        drawn.Press(Key.CursorDown);

        Assert.Equal(new PublicProfileStop(PublicProfilePart.Bio), form.Typing);
        Assert.Equal(1, bio.CurrentRow);
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
