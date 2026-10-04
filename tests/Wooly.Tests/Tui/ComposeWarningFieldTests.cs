using Wooly.Core.Posts;
using Wooly.Tests.Fakes;
using Wooly.Tui.Screens;
using Wooly.Tui.Theme;
using Wooly.Tui.Views;
using Key = Terminal.Gui.Input.Key;
using MouseFlags = Terminal.Gui.Input.MouseFlags;

namespace Wooly.Tests.Tui;

/// <summary>
///     The content warning as a field of its own, laid over its header's value column the way the editor is laid over
///     the body (#320): it selects, moves by word, takes a paste and takes the mouse, and the keys the shell takes off
///     the editor it takes off the field too.
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
        using var drawn = await Replying();
        var compose = Compose(drawn);

        drawn.Press(Key.W.WithCtrl);

        Assert.True(compose.WritingTheWarning);
        Assert.True(Field(drawn).HasFocus);

        Type(drawn, "cw");

        Assert.Equal("cw", compose.Warning);
        Assert.Equal(Mention, compose.Text);

        drawn.Press(Key.W.WithCtrl);

        Assert.False(compose.WritingTheWarning);
        Assert.True(Editor(drawn).HasFocus);

        Type(drawn, "hi");

        Assert.Equal("cw", compose.Warning);
        Assert.Equal($"{Mention}hi", compose.Text);
    }

    /// <summary><c>enter</c> finishes a one-line field the way it finishes any: the typing goes back to the post.</summary>
    [Fact]
    public async Task Enter_HandsTheTypingBackToThePost()
    {
        using var drawn = await Replying();
        var compose = Compose(drawn);

        drawn.Press(Key.W.WithCtrl);
        Type(drawn, "cw");
        drawn.Press(Key.Enter);

        Assert.False(compose.WritingTheWarning);
        Assert.True(Editor(drawn).HasFocus);
        Assert.Equal("cw", compose.Warning);
        Assert.Equal(Mention, compose.Text);
    }

    /// <summary>The status row says which way <c>ctrl-w</c> goes next, wherever the typing is.</summary>
    [Fact]
    public async Task TheStatusRowSaysWhichWayCtrlWGoesNext()
    {
        using var drawn = await Replying();

        Assert.Contains("Content warning: ctrl-w", drawn.Rows()[^1], StringComparison.Ordinal);

        drawn.Press(Key.W.WithCtrl);

        Assert.Contains("Back to the post: ctrl-w", drawn.Rows()[^1], StringComparison.Ordinal);
    }

    /// <summary>
    ///     Moving by word and selecting with shift and the arrows work as they do in the post: a word back, and a
    ///     selection typed over.
    /// </summary>
    [Fact]
    public async Task TheFieldMovesByWordAndSelects()
    {
        using var drawn = await Replying();
        var compose = Compose(drawn);

        drawn.Press(Key.W.WithCtrl);
        Type(drawn, "one two");

        drawn.Press(Key.CursorLeft.WithCtrl);
        drawn.Press(Key.Backspace);

        Assert.Equal("onetwo", compose.Warning);

        drawn.Press(Key.End);
        drawn.Press(Key.CursorLeft.WithShift);
        drawn.Press(Key.CursorLeft.WithShift);
        drawn.Press(Key.CursorLeft.WithShift);
        Type(drawn, "six");

        Assert.Equal("onesix", compose.Warning);
    }

    /// <summary>A paste goes into the field whole, where the typing is.</summary>
    [Fact]
    public async Task APasteGoesIntoTheField()
    {
        using var drawn = await Replying();
        var compose = Compose(drawn);

        drawn.Press(Key.W.WithCtrl);
        drawn.Application.RaisePasteEvent("spoilers");
        drawn.Redraw();

        Assert.Equal("spoilers", compose.Warning);
        Assert.Equal(Mention, compose.Text);
    }

    /// <summary>
    ///     <c>?</c> is a letter in the field rather than the keymap: a warning is entitled to ask a question. And so is
    ///     every other key the shell would otherwise act on — <c>c</c> does not open a second compose.
    /// </summary>
    [Fact]
    public async Task KeysTheShellActsOnAreLettersInTheField()
    {
        using var drawn = await Replying();
        var compose = Compose(drawn);

        drawn.Press(Key.W.WithCtrl);
        Type(drawn, "c?");

        Assert.Same(compose, drawn.Shell.Screen);
        Assert.Equal("c?", compose.Warning);
    }

    /// <summary>
    ///     And <c>?</c> is a letter in the post too, which is why compose's status row offers no keymap key.
    /// </summary>
    [Fact]
    public async Task AQuestionMarkInThePostIsALetter()
    {
        using var drawn = await Replying();
        var compose = Compose(drawn);

        Type(drawn, "?");

        Assert.Same(compose, drawn.Shell.Screen);
        Assert.Equal($"{Mention}?", compose.Text);
        Assert.DoesNotContain("?", drawn.Rows()[^1], StringComparison.Ordinal);
    }

    /// <summary><c>esc</c> from the field throws the draft away, as it does from the post.</summary>
    [Fact]
    public async Task Esc_FromTheFieldThrowsTheDraftAway()
    {
        using var drawn = await Replying();

        drawn.Press(Key.W.WithCtrl);
        Type(drawn, "cw");
        drawn.Press(Key.Esc);

        Assert.IsNotType<ComposeScreen>(drawn.Shell.Screen);
    }

    /// <summary><c>ctrl-s</c> from the field sends the post with the warning over it, as it does from the post.</summary>
    [Fact]
    public async Task CtrlS_FromTheFieldSendsThePostBehindTheWarning()
    {
        using var drawn = await Replying();

        Type(drawn, "hi");
        drawn.Press(Key.W.WithCtrl);
        Type(drawn, "cw");
        drawn.Press(Key.S.WithCtrl);
        drawn.Settle();

        var draft = Assert.Single(drawn.Built.Author.Published).Draft;

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
        using var drawn = await Replying();
        var compose = Compose(drawn);
        var field = Field(drawn).FrameToScreen();
        var editor = Editor(drawn).FrameToScreen();

        drawn.Point(field.X, field.Y, MouseFlags.LeftButtonPressed);
        drawn.Point(field.X, field.Y, MouseFlags.LeftButtonReleased);
        drawn.Click(field.X, field.Y);

        Assert.True(compose.WritingTheWarning);
        Assert.True(Field(drawn).HasFocus);

        Type(drawn, "cw");

        Assert.Equal("cw", compose.Warning);

        drawn.Point(editor.X, editor.Y, MouseFlags.LeftButtonPressed);
        drawn.Point(editor.X, editor.Y, MouseFlags.LeftButtonReleased);
        drawn.Click(editor.X, editor.Y);

        Assert.False(compose.WritingTheWarning);
        Assert.True(Editor(drawn).HasFocus);

        drawn.Press(Key.W.WithCtrl);

        Assert.True(compose.WritingTheWarning);
        Assert.True(Field(drawn).HasFocus);
    }

    /// <summary>A right click on the field does nothing, as on the editor: on a draft it would be <c>esc</c> (#307).</summary>
    [Fact]
    public async Task ARightClickOnTheFieldDoesNothing()
    {
        using var drawn = await Replying();
        var compose = Compose(drawn);
        var field = Field(drawn).FrameToScreen();

        drawn.RightClick(field.X, field.Y);

        Assert.Same(compose, drawn.Shell.Screen);
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

        using var drawn = await DrawnShell.Of(80, 24, Themes.Dark, built);

        ComposeRows.Open(drawn.Shell, opening);
        drawn.Redraw();

        Assert.Equal("spoilers", Field(drawn).Text);
        Assert.Contains(drawn.Rows(), row => row.Contains("⚠  spoilers", StringComparison.Ordinal));
    }

    /// <summary>The field sits on the warning header's row, in its value column, wherever the headers put it.</summary>
    [Fact]
    public async Task TheFieldSitsInTheWarningHeadersValueColumn()
    {
        using var drawn = await Replying();
        var at = Field(drawn).FrameToScreen();
        var row = drawn.Rows()[at.Y];

        Assert.Equal("⚠  ", row.Substring(at.X - 3, 3));
        Assert.Equal(Editor(drawn).FrameToScreen().Right, at.Right);
    }

    /// <summary>
    ///     Empty, the field says what it is for in the muted role — how to reach it while it is not being written, what
    ///     goes there while it is — and its text, once written, is the warning's colour.
    /// </summary>
    [Fact]
    public async Task TheFieldHintsWhileEmptyAndIsTheWarningsColourOnceWritten()
    {
        using var drawn = await Replying();
        var at = Field(drawn).FrameToScreen();

        Assert.StartsWith("none · ctrl-w to add", drawn.Rows()[at.Y][at.X..], StringComparison.Ordinal);
        Assert.Equal(Themes.Dark.For(Role.Muted), drawn.Cell(at.Y, at.X));

        drawn.Press(Key.W.WithCtrl);

        Assert.StartsWith("say what it's about", drawn.Rows()[at.Y][at.X..], StringComparison.Ordinal);

        Type(drawn, "cw");

        Assert.Equal("cw", drawn.Rows()[at.Y].Substring(at.X, at.Width).TrimEnd());
        Assert.Equal(Themes.Dark.For(Role.ContentWarning), drawn.Cell(at.Y, at.X));
    }

    /// <summary>Selected text in the field is drawn in the selection role, as it is in the post (#316).</summary>
    [Fact]
    public async Task SelectedTextInTheFieldIsDrawnInTheSelectionRole()
    {
        using var drawn = await Replying();

        drawn.Press(Key.W.WithCtrl);
        Type(drawn, "cw!");
        Field(drawn).SelectAll();
        drawn.Redraw();

        var at = Field(drawn).FrameToScreen();

        Assert.All(
            Enumerable.Range(at.X, 3),
            column => Assert.Equal(Themes.Dark.For(Role.SelectedText), drawn.Cell(at.Y, column)));
    }

    /// <summary>
    ///     A selection made with shift and the arrows is drawn in the selection role too, and what is left unselected
    ///     stays the warning's colour.
    /// </summary>
    [Fact]
    public async Task ASelectionMadeWithShiftIsDrawnInTheSelectionRole()
    {
        using var drawn = await Replying();

        drawn.Press(Key.W.WithCtrl);
        Type(drawn, "one two");
        drawn.Press(Key.CursorLeft.WithShift);
        drawn.Press(Key.CursorLeft.WithShift);
        drawn.Press(Key.CursorLeft.WithShift);

        var at = Field(drawn).FrameToScreen();

        Assert.Equal(Themes.Dark.For(Role.ContentWarning), drawn.Cell(at.Y, at.X));
        Assert.All(
            Enumerable.Range(at.X + 4, 3),
            column => Assert.Equal(Themes.Dark.For(Role.SelectedText), drawn.Cell(at.Y, column)));
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

        using var drawn = await DrawnShell.Of(80, 24, theme, built);

        drawn.Shell.Reply();
        drawn.Redraw();
        drawn.Press(Key.W.WithCtrl);
        Type(drawn, "cw");
        drawn.Press(Key.CursorLeft.WithShift);

        var answered = Enum.GetValues<Role>()
                           .SelectMany(role => new[] { theme.For(role), theme.Banded(role) })
                           .ToHashSet();

        Assert.All(drawn.Cells(), cell => Assert.Contains(cell, answered));
    }

    /// <summary>
    ///     A compose thrown away while the field had the typing gives it back: the next one opens with the editor
    ///     focused.
    /// </summary>
    [Fact]
    public async Task TheNextComposeOpensOnItsEditor()
    {
        using var drawn = await Replying();

        drawn.Press(Key.W.WithCtrl);
        drawn.Press(Key.Esc);

        drawn.Shell.Reply();
        drawn.Redraw();

        Assert.False(Compose(drawn).WritingTheWarning);
        Assert.True(Editor(drawn).HasFocus);
        Assert.Equal(string.Empty, Field(drawn).Text);
    }

    /// <summary>A reply to a post with no warning, drawn on an 80×24 terminal.</summary>
    private static async Task<DrawnShell> Replying()
    {
        var built = new AShell
        {
            Timelines = FakeTimelineReader.Holding(APost.With(id: "220", account: "ben@hachyderm.io")),
        };

        var drawn = await DrawnShell.Of(80, 24, Themes.Dark, built);

        drawn.Shell.Reply();
        drawn.Redraw();

        return drawn;
    }

    private static ComposeScreen Compose(DrawnShell drawn) => Assert.IsType<ComposeScreen>(drawn.Shell.Screen);

    private static void Type(DrawnShell drawn, string text)
    {
        foreach (var letter in text)
        {
            drawn.Window.NewKeyDownEvent(new Key(letter));
        }

        drawn.Redraw();
    }

    private static ComposeEditor Editor(DrawnShell drawn) =>
        drawn.Window.SubViews.OfType<ComposeEditor>().Single();

    private static ComposeWarningField Field(DrawnShell drawn) =>
        drawn.Window.SubViews.OfType<ComposeWarningField>().Single();
}
