using Wooly.Core.Posts;
using Wooly.Tests.Fakes;
using Wooly.Tui.Rendering;
using Wooly.Tui.Screens;
using Wooly.Tui.Shell;
using Wooly.Tui.Theme;
using Wooly.Tui.Views;

namespace Wooly.Tests.Tui;

/// <summary>
///     Descriptions on pending attachments (#377) in the shell: the description editor in the window, and <c>esc</c>
///     and <c>ctrl-s</c> being done with it. Opening it, what is written into it and what goes to the instance, and
///     when, are drafting's (<see cref="DraftingDescriptionTests" />).
/// </summary>
public class ComposeDescriptionTests : IDisposable
{
    /// <summary>The content panel's inside at an 80 × 24 terminal.</summary>
    private const int Width = 78;

    private const int Height = 21;

    private readonly TemporaryDirectory _files = new();

    /// <summary>
    ///     <c>esc</c> is done, not cancel: it goes back to the draft keeping what was typed, which the row says in quotes
    ///     once the attachment is ready.
    /// </summary>
    [Theory]
    [InlineData(ShellKey.Escape)]
    [InlineData(ShellKey.CtrlS)]
    public async Task DoneKeepsTheDescriptionAndTheRowQuotesIt(ShellKey done)
    {
        var (shell, built, compose) = await Composing();

        shell.Paste(_files.WriteFile("cat.png"));
        built.Author.Attaching.Single().Ready();
        built.Host.Drain();

        Walk(shell, ShellKey.Up);
        shell.Press(ShellKey.Enter);
        shell.Drafting.Write("A cat asleep on a mat");
        shell.Press(done);

        Assert.Same(compose, shell.Screen);
        Assert.Null(shell.Asking);
        Assert.EndsWith("x  “A cat asleep on a mat”", Row(compose, "cat.png", width: 120), StringComparison.Ordinal);
    }

    /// <summary>
    ///     A description longer than the status column is cut inside its closing quote, so the row still reads as one
    ///     quotation.
    /// </summary>
    [Fact]
    public async Task ALongDescriptionIsCutInsideItsClosingQuote()
    {
        var (shell, built, compose) = await Composing();

        shell.Paste(_files.WriteFile("cat.png"));
        built.Author.Attaching.Single().Ready();
        built.Host.Drain();
        Walk(shell, ShellKey.Up);
        shell.Press(ShellKey.Enter);
        shell.Drafting.Write("A dog in a red knitted coat sitting on the steps of a house in the rain");
        shell.Press(ShellKey.Escape);

        Assert.EndsWith("x  “A dog in a red knitted coat sitting o…”", Row(compose, "cat.png", width: 120), StringComparison.Ordinal);
    }

    /// <summary>
    ///     In the window: a click on a row picks it out, a click on its description opens the editor, and what is typed
    ///     there is the description.
    /// </summary>
    [Fact]
    public async Task InTheWindowAClickOnTheMarkOpensTheEditor()
    {
        using var drawn = await DrawnShell.Of(100, 30, Themes.Plain, new AShell());

        drawn.Shell.Press(ShellKey.C);
        drawn.Redraw();
        drawn.Shell.Paste(_files.WriteFile("cat.png"));
        drawn.Built.Author.Attaching.Single().Ready();
        drawn.Settle();

        var (row, name) = Find(drawn, "cat.png");

        drawn.Click(name, row);

        var compose = Assert.IsType<ComposeScreen>(drawn.Shell.Screen);

        Assert.Equal("cat.png", compose.Media.Picked?.Name);

        drawn.Click(Find(drawn, "no alt text").Column, row);

        Assert.IsType<DescriptionScreen>(drawn.Shell.Screen);

        foreach (var letter in "A cat")
        {
            drawn.Press(letter);
        }

        drawn.Press(Terminal.Gui.Input.Key.Esc);
        drawn.Settle();

        Assert.Same(compose, drawn.Shell.Screen);
        Assert.Contains(drawn.Rows(), text => text.Contains("“A cat”", StringComparison.Ordinal));
        Assert.Equal("A cat", Assert.Single(drawn.Built.Author.Descriptions).Description);
    }

    /// <summary>
    ///     A right click over the description editor's field is its <c>esc</c>, as it is over compose's fields and the
    ///     form's (#373, #430): it leaves the editor, back to the draft, and sends the description as leaving does.
    /// </summary>
    [Fact]
    public async Task InTheWindowARightClickOverTheFieldIsDone()
    {
        using var drawn = await Describing();
        var field = drawn.Window.DescriptionField<ComposeEditor>().FrameToScreen();

        foreach (var letter in "A cat")
        {
            drawn.Press(letter);
        }

        drawn.RightClick(field.X, field.Y);
        drawn.Settle();

        Assert.IsType<ComposeScreen>(drawn.Shell.Screen);
        Assert.Null(drawn.Shell.Asking);
        Assert.Equal("A cat", Assert.Single(drawn.Built.Author.Descriptions).Description);
    }

    /// <summary>
    ///     A right click elsewhere on the description editor's screen — the breadcrumb, the status row — is its
    ///     <c>esc</c> too, as it was before the field took one (#307, #430): it leaves the editor, back to the draft.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(29)]
    public async Task InTheWindowARightClickOffTheFieldIsDoneToo(int row)
    {
        using var drawn = await Describing();
        var field = drawn.Window.DescriptionField<ComposeEditor>().FrameToScreen();

        Assert.False(field.Contains(field.X, row));

        foreach (var letter in "A cat")
        {
            drawn.Press(letter);
        }

        drawn.RightClick(field.X, row);
        drawn.Settle();

        Assert.IsType<ComposeScreen>(drawn.Shell.Screen);
        Assert.Null(drawn.Shell.Asking);
        Assert.Equal("A cat", Assert.Single(drawn.Built.Author.Descriptions).Description);
    }

    /// <summary>
    ///     A press over the description editor's field while a question is open — <c>tab</c> off a touched draft asks
    ///     before it is thrown away — goes to the question, as over compose's fields and the form's (#373, #430): the
    ///     click keeps the draft, and the field neither moves its caret nor takes the typing.
    /// </summary>
    [Fact]
    public async Task InTheWindowAPressOverTheFieldDuringAQuestionGoesToTheQuestion()
    {
        using var drawn = await Describing();
        var editor = drawn.Window.DescriptionField<ComposeEditor>();
        var field = editor.FrameToScreen();

        foreach (var letter in "A cat")
        {
            drawn.Press(letter);
        }

        drawn.Press(Terminal.Gui.Input.Key.Tab);

        Assert.NotNull(drawn.Shell.Asking);

        var caret = editor.CurrentColumn;

        drawn.Point(field.X, field.Y, Terminal.Gui.Input.MouseFlags.LeftButtonPressed);
        drawn.Point(field.X, field.Y, Terminal.Gui.Input.MouseFlags.LeftButtonReleased);
        drawn.Click(field.X, field.Y);
        drawn.Settle();

        Assert.Null(drawn.Shell.Asking);
        Assert.IsType<DescriptionScreen>(drawn.Shell.Screen);
        Assert.Equal("A cat", editor.Text);
        Assert.Equal(caret, editor.CurrentColumn);
    }

    /// <summary>A double click anywhere on a row opens the editor.</summary>
    [Fact]
    public async Task InTheWindowADoubleClickOnARowOpensTheEditor()
    {
        using var drawn = await DrawnShell.Of(100, 30, Themes.Plain, new AShell());

        drawn.Shell.Press(ShellKey.C);
        drawn.Redraw();
        drawn.Shell.Paste(_files.WriteFile("cat.png"));
        drawn.Settle();

        var (row, name) = Find(drawn, "cat.png");

        drawn.DoubleClick(name, row);

        Assert.IsType<DescriptionScreen>(drawn.Shell.Screen);
    }

    /// <summary>In the window, <c>↑</c> from the post walks onto the last row and <c>enter</c> there opens the editor.</summary>
    [Fact]
    public async Task InTheWindowTheKeysWalkOntoARowAndOpenTheEditor()
    {
        using var drawn = await DrawnShell.Of(100, 30, Themes.Plain, new AShell());

        drawn.Shell.Press(ShellKey.C);
        drawn.Redraw();
        drawn.Shell.Paste(_files.WriteFile("cat.png"));
        drawn.Settle();

        drawn.PressThroughTheApplication(Terminal.Gui.Input.Key.CursorUp);

        Assert.Contains(drawn.Rows(), text => text.Contains("▌⠶     cat.png", StringComparison.Ordinal));

        drawn.PressThroughTheApplication(Terminal.Gui.Input.Key.Enter);

        Assert.IsType<DescriptionScreen>(drawn.Shell.Screen);
    }

    /// <summary>
    ///     Done with a description — <c>esc</c>, <c>ctrl-s</c> or a click on the crumb back — goes back to the row it was
    ///     written for, picked, rather than to the post: so the next row's description is a walk and an <c>enter</c> away.
    /// </summary>
    [Theory]
    [InlineData(ShellKey.Escape)]
    [InlineData(ShellKey.CtrlS)]
    public async Task DoneGoesBackToTheRowDescribed(ShellKey done)
    {
        var (shell, built, compose) = await Composing();

        shell.Paste(_files.WriteFile("cat.png"));
        shell.Paste(_files.WriteFile("dog.png"));
        built.Host.Drain();

        Walk(shell, ShellKey.Up);
        Walk(shell, ShellKey.Up);
        shell.Press(ShellKey.Enter);
        shell.Drafting.Write("A cat asleep on a mat");
        shell.Press(done);

        Assert.Same(compose, shell.Screen);
        Assert.Equal(ComposeField.Attachment, compose.Typing);
        Assert.Equal("cat.png", compose.Media.Picked?.Name);
    }

    /// <summary>
    ///     In the window, done with a description leaves the walk on the row described, its bar drawn, and the next
    ///     <c>↓</c> walks to the row under it rather than starting from the post.
    /// </summary>
    [Fact]
    public async Task InTheWindowDoneGoesBackToTheRowDescribed()
    {
        using var drawn = await DrawnShell.Of(100, 30, Themes.Plain, new AShell());

        drawn.Shell.Press(ShellKey.C);
        drawn.Redraw();
        drawn.Shell.Paste(_files.WriteFile("cat.png"));
        drawn.Shell.Paste(_files.WriteFile("dog.png"));
        drawn.Settle();

        drawn.PressThroughTheApplication(Terminal.Gui.Input.Key.CursorUp);
        drawn.PressThroughTheApplication(Terminal.Gui.Input.Key.CursorUp);
        drawn.PressThroughTheApplication(Terminal.Gui.Input.Key.Enter);

        Assert.IsType<DescriptionScreen>(drawn.Shell.Screen);

        drawn.PressThroughTheApplication(Terminal.Gui.Input.Key.Esc);

        var compose = Assert.IsType<ComposeScreen>(drawn.Shell.Screen);

        Assert.Contains(drawn.Rows(), text => text.Contains("▌⠶     cat.png", StringComparison.Ordinal));
        Assert.Equal(ComposeField.Attachment, compose.Typing);

        drawn.PressThroughTheApplication(Terminal.Gui.Input.Key.CursorDown);

        Assert.Equal("dog.png", compose.Media.Picked?.Name);
    }

    public void Dispose() => _files.Dispose();

    /// <summary>
    ///     <paramref name="key" /> pressed on the screen in front once it has been drawn, as a terminal would have drawn it
    ///     before anything is pressed: the walk keeps to the rows that were drawn (#378).
    /// </summary>
    private static void Walk(Wooly.Tui.Shell.Shell shell, ShellKey key)
    {
        _ = Lines(shell.Screen);

        shell.Press(key);
    }

    /// <summary>The row and column <paramref name="text" /> is first drawn at in the window.</summary>
    private static (int Row, int Column) Find(DrawnShell drawn, string text)
    {
        var rows = drawn.Rows();
        var row = Array.FindIndex(rows, drawnRow => drawnRow.Contains(text, StringComparison.Ordinal));

        return (row, rows[row].IndexOf(text, StringComparison.Ordinal));
    }

    /// <summary>The window, a picture attached to a fresh compose and ready, and its description editor open over it.</summary>
    private async Task<DrawnShell> Describing()
    {
        var drawn = await DrawnShell.Of(100, 30, Themes.Plain, new AShell());

        drawn.Shell.Press(ShellKey.C);
        drawn.Redraw();
        drawn.Shell.Paste(_files.WriteFile("cat.png"));
        drawn.Built.Author.Attaching.Single().Ready();
        drawn.Settle();

        drawn.PressThroughTheApplication(Terminal.Gui.Input.Key.CursorUp);
        drawn.PressThroughTheApplication(Terminal.Gui.Input.Key.Enter);
        drawn.Settle();

        Assert.IsType<DescriptionScreen>(drawn.Shell.Screen);

        return drawn;
    }

    private async Task<(Wooly.Tui.Shell.Shell Shell, AShell Built, ComposeScreen Compose)> Composing()
    {
        var built = new AShell();
        var shell = await built.Opened();
        var compose = ComposeRows.Open(shell, ComposeFor.Post);

        built.Host.Drain();

        return (shell, built, compose);
    }

    private static string Row(ComposeScreen compose, string name, int width = Width) =>
        Texts(compose, width).Single(row => row.Contains(name, StringComparison.Ordinal));

    private static IReadOnlyList<Line> Lines(Screen screen, int width = Width, int height = Height) =>
        screen.Lines(new Drawing(width, AShell.Now, Height: height));

    private static IReadOnlyList<string> Texts(Screen screen, int width = Width, int height = Height) =>
        [.. Lines(screen, width, height).Select(line => line.Text)];
}
