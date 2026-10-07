using Wooly.Core.Posts;
using Wooly.Tests.Fakes;
using Wooly.Tui.Screens;
using Wooly.Tui.Theme;
using Wooly.Tui.Views;
using Key = Terminal.Gui.Input.Key;

namespace Wooly.Tests.Tui;

/// <summary>
///     The content warning as a field of its own, laid over its header's value column the way the editor is laid over
///     the body (#320): it selects, moves by word, takes a paste and takes the mouse, and the keys the shell takes off
///     the editor it takes off the field too. Typed into in <see cref="ComposeView" /> over a shell (#365).
/// </summary>
public class ComposeWarningFieldTests
{
    private const string Mention = "@ben@hachyderm.io ";

    /// <summary>
    ///     <c>ctrl-w</c> moves the typing into the field and what is typed lands there, the post untouched — and
    ///     <c>ctrl-w</c> again hands it back to the post.
    /// </summary>
    [Fact]
    public async Task CtrlW_MovesTheTypingIntoTheFieldAndBack()
    {
        using var view = await Replying();
        var compose = view.Compose;

        view.Press(Key.W.WithCtrl);

        Assert.True(compose.WritingTheWarning);
        Assert.True(view.Warning.HasFocus);

        view.Type("cw");

        Assert.Equal("cw", compose.Warning);
        Assert.Equal(Mention, compose.Text);

        view.Press(Key.W.WithCtrl);

        Assert.False(compose.WritingTheWarning);
        Assert.True(view.Editor.HasFocus);

        view.Type("hi");

        Assert.Equal("cw", compose.Warning);
        Assert.Equal($"{Mention}hi", compose.Text);
    }

    /// <summary><c>enter</c> finishes a one-line field the way it finishes any: the typing goes back to the post.</summary>
    [Fact]
    public async Task Enter_HandsTheTypingBackToThePost()
    {
        using var view = await Replying();
        var compose = view.Compose;

        view.Press(Key.W.WithCtrl);
        view.Type("cw");
        view.Press(Key.Enter);

        Assert.False(compose.WritingTheWarning);
        Assert.True(view.Editor.HasFocus);
        Assert.Equal("cw", compose.Warning);
        Assert.Equal(Mention, compose.Text);
    }

    /// <summary>The status row says which way <c>ctrl-w</c> goes next, wherever the typing is.</summary>
    [Fact]
    public async Task TheStatusRowSaysWhichWayCtrlWGoesNext()
    {
        using var view = await Replying();

        Assert.Contains("Content warning: ctrl-w", view.Status(), StringComparison.Ordinal);

        view.Press(Key.W.WithCtrl);

        Assert.Contains("Back to the post: ctrl-w", view.Status(), StringComparison.Ordinal);
    }

    /// <summary>
    ///     Moving by word and selecting with shift and the arrows work as they do in the post: a word back, and a
    ///     selection typed over.
    /// </summary>
    [Fact]
    public async Task TheFieldMovesByWordAndSelects()
    {
        using var view = await Replying();
        var compose = view.Compose;

        view.Press(Key.W.WithCtrl);
        view.Type("one two");

        view.Press(Key.CursorLeft.WithCtrl);
        view.Press(Key.Backspace);

        Assert.Equal("onetwo", compose.Warning);

        view.Press(Key.End);
        view.Press(Key.CursorLeft.WithShift);
        view.Press(Key.CursorLeft.WithShift);
        view.Press(Key.CursorLeft.WithShift);
        view.Type("six");

        Assert.Equal("onesix", compose.Warning);
    }

    /// <summary>A paste goes into the field whole, where the typing is.</summary>
    [Fact]
    public async Task APasteGoesIntoTheField()
    {
        using var view = await Replying();
        var compose = view.Compose;

        view.Press(Key.W.WithCtrl);
        view.Paste("spoilers");

        Assert.Equal("spoilers", compose.Warning);
        Assert.Equal(Mention, compose.Text);
    }

    /// <summary>
    ///     <c>?</c> is a letter in the field rather than the keymap: a warning is entitled to ask a question. And so is
    ///     every other key the shell would otherwise act on — <c>c</c> never reaches the window to open a second compose.
    /// </summary>
    [Fact]
    public async Task KeysTheShellActsOnAreLettersInTheField()
    {
        using var view = await Replying();
        var compose = view.Compose;

        view.Press(Key.W.WithCtrl);
        view.Window.Reached.Clear();
        view.Type("c?");

        Assert.Empty(view.Window.Reached);
        Assert.Same(compose, view.Shell.Screen);
        Assert.Equal("c?", compose.Warning);
    }

    /// <summary>
    ///     And <c>?</c> is a letter in the post too, which is why compose's status row offers no keymap key.
    /// </summary>
    [Fact]
    public async Task AQuestionMarkInThePostIsALetter()
    {
        using var view = await Replying();
        var compose = view.Compose;

        view.Type("?");

        Assert.Empty(view.Window.Reached);
        Assert.Same(compose, view.Shell.Screen);
        Assert.Equal($"{Mention}?", compose.Text);
        Assert.DoesNotContain("?", view.Status(), StringComparison.Ordinal);
    }

    /// <summary><c>esc</c> from the field throws the draft away, as it does from the post.</summary>
    [Fact]
    public async Task Esc_FromTheFieldThrowsTheDraftAway()
    {
        using var view = await Replying();

        view.Press(Key.W.WithCtrl);
        view.Type("cw");
        view.Press(Key.Esc);

        Assert.IsNotType<ComposeScreen>(view.Shell.Screen);
    }

    /// <summary><c>ctrl-s</c> from the field sends the post with the warning over it, as it does from the post.</summary>
    [Fact]
    public async Task CtrlS_FromTheFieldSendsThePostBehindTheWarning()
    {
        using var view = await Replying();

        view.Type("hi");
        view.Press(Key.W.WithCtrl);
        view.Type("cw");
        view.Press(Key.S.WithCtrl);
        view.Settle();

        var draft = Assert.Single(view.Built.Author.Published).Draft;

        Assert.Equal($"{Mention}hi", draft.Text);
        Assert.Equal("cw", draft.ContentWarning);
    }

    /// <summary>
    ///     A click into the field moves the typing there, and a click into the post moves it back — the two are one
    ///     form, and <c>ctrl-w</c> keeps saying the right way afterwards.
    /// </summary>
    [Fact]
    public async Task AClickMovesTheTypingBetweenTheFields()
    {
        using var view = await Replying();
        var compose = view.Compose;
        var field = view.Warning.FrameToScreen();
        var editor = view.Editor.FrameToScreen();

        view.Click(field.X, field.Y);

        Assert.True(compose.WritingTheWarning);
        Assert.True(view.Warning.HasFocus);

        view.Type("cw");

        Assert.Equal("cw", compose.Warning);

        view.Click(editor.X, editor.Y);

        Assert.False(compose.WritingTheWarning);
        Assert.True(view.Editor.HasFocus);

        view.Press(Key.W.WithCtrl);

        Assert.True(compose.WritingTheWarning);
        Assert.True(view.Warning.HasFocus);
    }

    /// <summary>A right click on the field does nothing, as on the editor: on a draft it would be <c>esc</c> (#307).</summary>
    [Fact]
    public async Task ARightClickOnTheFieldDoesNothing()
    {
        using var view = await Replying();
        var compose = view.Compose;
        var field = view.Warning.FrameToScreen();

        view.RightClick(field.X, field.Y);

        Assert.Same(compose, view.Shell.Screen);
        Assert.False(compose.WritingTheWarning);
    }

    /// <summary>A reply opens the field on the answered post's warning (#123), and an edit on the post's own (#140).</summary>
    [Theory]
    [InlineData(ComposeFor.Reply)]
    [InlineData(ComposeFor.Edit)]
    public async Task TheFieldOpensOnThePostsWarning(ComposeFor opening)
    {
        var built = new AShell
        {
            Timelines = FakeTimelineReader.Holding(
                APost.With(id: "220", account: "jeff@mastodon.social", contentWarning: "spoilers")),
        };

        using var view = await ComposedView.Of(built, opening);

        Assert.Equal("spoilers", view.Warning.Text);
        Assert.Contains(view.Rows(), row => row.Contains("⚠  spoilers", StringComparison.Ordinal));
    }

    /// <summary>The field sits on the warning header's row, in its value column, wherever the headers put it.</summary>
    [Fact]
    public async Task TheFieldSitsInTheWarningHeadersValueColumn()
    {
        using var view = await Replying();
        var at = view.Warning.FrameToScreen();
        var row = view.Rows()[at.Y];

        Assert.Equal("⚠  ", row.Substring(at.X - 3, 3));
        Assert.Equal(view.Editor.FrameToScreen().Right, at.Right);
    }

    /// <summary>
    ///     Empty, the field says what it is for in the muted role — how to reach it while it is not being written, what
    ///     goes there while it is — and its text, once written, is the warning's colour.
    /// </summary>
    [Fact]
    public async Task TheFieldHintsWhileEmptyAndIsTheWarningsColourOnceWritten()
    {
        using var view = await Replying();
        var at = view.Warning.FrameToScreen();

        Assert.StartsWith("none · ctrl-w to add", view.Rows()[at.Y][at.X..], StringComparison.Ordinal);
        Assert.Equal(Themes.Dark.For(Role.Muted), view.Cell(at.Y, at.X));

        view.Press(Key.W.WithCtrl);

        Assert.StartsWith("say what it's about", view.Rows()[at.Y][at.X..], StringComparison.Ordinal);

        view.Type("cw");

        Assert.Equal("cw", view.Rows()[at.Y].Substring(at.X, at.Width).TrimEnd());
        Assert.Equal(Themes.Dark.For(Role.ContentWarning), view.Cell(at.Y, at.X));
    }

    /// <summary>Selected text in the field is drawn in the selection role, as it is in the post (#316).</summary>
    [Fact]
    public async Task SelectedTextInTheFieldIsDrawnInTheSelectionRole()
    {
        using var view = await Replying();

        view.Press(Key.W.WithCtrl);
        view.Type("cw!");
        view.Warning.SelectAll();
        view.Redraw();

        var at = view.Warning.FrameToScreen();

        Assert.All(
            Enumerable.Range(at.X, 3),
            column => Assert.Equal(Themes.Dark.For(Role.SelectedText), view.Cell(at.Y, column)));
    }

    /// <summary>
    ///     A selection made with shift and the arrows is drawn in the selection role too, and what is left unselected
    ///     stays the warning's colour.
    /// </summary>
    [Fact]
    public async Task ASelectionMadeWithShiftIsDrawnInTheSelectionRole()
    {
        using var view = await Replying();

        view.Press(Key.W.WithCtrl);
        view.Type("one two");
        view.Press(Key.CursorLeft.WithShift);
        view.Press(Key.CursorLeft.WithShift);
        view.Press(Key.CursorLeft.WithShift);

        var at = view.Warning.FrameToScreen();

        Assert.Equal(Themes.Dark.For(Role.ContentWarning), view.Cell(at.Y, at.X));
        Assert.All(
            Enumerable.Range(at.X + 4, 3),
            column => Assert.Equal(Themes.Dark.For(Role.SelectedText), view.Cell(at.Y, column)));
    }

    /// <summary>
    ///     Every cell of a compose with a warning being written and part of it selected is a role the theme answers,
    ///     none a colour Terminal.Gui chose for itself.
    /// </summary>
    [Theory]
    [InlineData("dark")]
    [InlineData("light")]
    public async Task EveryCellWithTheFieldInUseIsARoleTheThemeAnswers(string name)
    {
        var theme = name == "dark" ? Themes.Dark : Themes.Light;
        var built = new AShell
        {
            Timelines = FakeTimelineReader.Holding(APost.With(id: "220", account: "ben@hachyderm.io")),
        };

        using var view = await ComposedView.Of(built, ComposeFor.Reply, theme: theme);

        view.Press(Key.W.WithCtrl);
        view.Type("cw");
        view.Press(Key.CursorLeft.WithShift);

        var answered = Enum.GetValues<Role>()
                           .SelectMany(role => new[] { theme.For(role), theme.Banded(role) })
                           .ToHashSet();

        Assert.All(view.Cells(), cell => Assert.Contains(cell, answered));
    }

    /// <summary>
    ///     A compose thrown away while the field had the typing gives it back: the next one opens with the editor
    ///     focused.
    /// </summary>
    [Fact]
    public async Task TheNextComposeOpensOnItsEditor()
    {
        using var view = await Replying();

        view.Press(Key.W.WithCtrl);
        view.Press(Key.Esc);

        view.Shell.Reply();
        view.Redraw();

        Assert.False(view.Compose.WritingTheWarning);
        Assert.True(view.Editor.HasFocus);
        Assert.Equal(string.Empty, view.Warning.Text);
    }

    /// <summary>Compose's fields over a reply to a post with no warning.</summary>
    private static Task<ComposedView> Replying() =>
        ComposedView.Of(
            new AShell { Timelines = FakeTimelineReader.Holding(APost.With(id: "220", account: "ben@hachyderm.io")) },
            ComposeFor.Reply);
}
