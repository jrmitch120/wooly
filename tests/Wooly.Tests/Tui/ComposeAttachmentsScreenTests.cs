using Terminal.Gui.Input;
using Wooly.Core.Errors;
using Wooly.Tests.Fakes;
using Wooly.Tui.Rendering;
using Wooly.Tui.Screens;
using Wooly.Tui.Shell;
using Wooly.Tui.Theme;

namespace Wooly.Tests.Tui;

/// <summary>
///     The attachments screen (story 58) in the shell: describing from it and leaving it, and a click on the folded
///     Media line pushing it, with the mouse its rows take under the header. What its keys do to the draft is drafting's
///     (<see cref="DraftingAttachmentsScreenTests" />).
/// </summary>
public class ComposeAttachmentsScreenTests : IDisposable
{
    /// <summary>The content panel's inside at an 80-column terminal.</summary>
    private const int Width = 78;

    /// <summary>Tall enough for three rows under the header with three left to write in, as the fold test has it.</summary>
    private const int Roomy = 16;

    /// <summary>A row short of that, which folds them.</summary>
    private const int Short = 15;

    private readonly TemporaryDirectory _files = new();

    public void Dispose() => _files.Dispose();

    /// <summary>
    ///     <c>⏎</c> there opens the description editor on the row picked, and its done comes back to the list; esc off
    ///     the list goes back to the compose, its typing on the Media header.
    /// </summary>
    [Fact]
    public async Task EnterDescribesAndEscGoesBack()
    {
        var (shell, _, compose) = await Folded("one.png", "two.png", "three.png");

        shell.Press(ShellKey.Enter);
        shell.Move(1);
        shell.Press(ShellKey.Enter);

        var describing = Assert.IsType<DescriptionScreen>(shell.Screen);

        Assert.Equal("two.png", describing.Attachment.Name);

        shell.Drafting.Write("A dog");
        shell.Press(ShellKey.Escape);

        Assert.IsType<AttachmentsScreen>(shell.Screen);
        Assert.Equal("A dog", compose.Media.Attachments[1].Description);

        shell.Press(ShellKey.Escape);

        Assert.Same(compose, shell.Screen);
        Assert.Equal(ComposeField.Media, compose.Typing);
    }

    /// <summary>
    ///     In the window: a click on the folded line pushes the list; there a click on a row's <c>x</c> takes it off, a
    ///     click on its quiet mark opens the description editor, and a drag reorders it, live.
    /// </summary>
    [Fact]
    public async Task InTheWindowTheMouseWorksThere()
    {
        using var drawn = await DrawnShell.Of(80, Short + 3, Themes.Plain, new AShell { LaunchedFrom = _files.Path });

        drawn.Shell.Press(ShellKey.C);
        drawn.Redraw();

        foreach (var name in new[] { "one.png", "two.png", "three.png" })
        {
            Assert.True(drawn.Shell.Paste(_files.WriteFile(name)));
        }

        drawn.Redraw();

        var (column, row) = At(drawn, "Media", "3 of 4");

        drawn.Click(column + 1, row);

        var listed = Assert.IsType<AttachmentsScreen>(drawn.Shell.Screen);

        (column, row) = At(drawn, "two.png", " x ");
        drawn.Click(column + 1, row);

        Assert.Equal(["one.png", "three.png"], Names(listed.Compose));

        (column, row) = At(drawn, "three.png", "three.png");
        drawn.Point(column, row, MouseFlags.LeftButtonPressed);
        drawn.Point(column, row - 1, MouseFlags.LeftButtonPressed | MouseFlags.PositionReport);
        drawn.Point(column, row - 1, MouseFlags.LeftButtonReleased);
        drawn.Point(column, row - 1, MouseFlags.LeftButtonClicked);

        Assert.Same(listed, drawn.Shell.Screen);
        Assert.Equal(["three.png", "one.png"], Names(listed.Compose));

        drawn.Built.Author.Attaching[0].Ready();
        drawn.Built.Host.Drain();
        drawn.Redraw();

        (column, row) = At(drawn, "one.png", "no alt text");
        drawn.Click(column + 1, row);

        Assert.Equal("one.png", Assert.IsType<DescriptionScreen>(drawn.Shell.Screen).Attachment.Name);
    }

    /// <summary>
    ///     On the attachments screen as under the Media header: a click is not a drag, and nor is the pointer moving with
    ///     no press made on a row, however the terminal reports it.
    /// </summary>
    [Fact]
    public async Task InTheWindowOnlyAPressOnARowPicksItUp()
    {
        using var drawn = await DrawnShell.Of(80, Short + 3, Themes.Plain, new AShell { LaunchedFrom = _files.Path });

        drawn.Shell.Press(ShellKey.C);
        drawn.Redraw();

        foreach (var name in new[] { "one.png", "two.png", "three.png" })
        {
            Assert.True(drawn.Shell.Paste(_files.WriteFile(name)));
        }

        drawn.Redraw();

        var (column, row) = At(drawn, "Media", "3 of 4");

        drawn.Click(column + 1, row);

        var listed = Assert.IsType<AttachmentsScreen>(drawn.Shell.Screen);

        (column, row) = At(drawn, "three.png", "three.png");
        drawn.Point(column, row, MouseFlags.LeftButtonPressed);
        drawn.Point(column, row, MouseFlags.LeftButtonReleased);
        drawn.Point(column, row, MouseFlags.LeftButtonClicked);
        drawn.Point(column, row - 1, MouseFlags.LeftButtonPressed | MouseFlags.PositionReport);
        drawn.Point(column, row - 2, MouseFlags.LeftButtonPressed | MouseFlags.PositionReport);

        Assert.Equal(["one.png", "two.png", "three.png"], Names(listed.Compose));
    }

    /// <summary>
    ///     A compose with <paramref name="names" /> dropped onto it, drawn short enough that their rows fold, and the walk
    ///     on the Media header the rows folded into.
    /// </summary>
    private async Task<(Wooly.Tui.Shell.Shell Shell, AShell Built, ComposeScreen Compose)> Folded(params string[] names)
    {
        var composing = await Composing(names);

        Assert.DoesNotContain(Texts(composing.Compose, Short), row => row.Contains(names[0], StringComparison.Ordinal));

        composing.Shell.Press(ShellKey.Up);

        Assert.Equal(ComposeField.Media, composing.Compose.Typing);

        return composing;
    }

    private async Task<(Wooly.Tui.Shell.Shell Shell, AShell Built, ComposeScreen Compose)> Composing(params string[] names)
    {
        var built = new AShell { LaunchedFrom = _files.Path };
        var shell = await built.Opened();
        var compose = ComposeRows.Open(shell, ComposeFor.Post);

        built.Host.Drain();

        foreach (var name in names)
        {
            Assert.True(shell.Paste(_files.WriteFile(name)));
        }

        return (shell, built, compose);
    }

    /// <summary>Where <paramref name="text" /> starts on the drawn row with <paramref name="on" /> on it.</summary>
    private static (int Column, int Row) At(DrawnShell drawn, string on, string text)
    {
        var rows = drawn.Rows();
        var row = Array.FindIndex(rows, line => line.Contains(on, StringComparison.Ordinal));

        Assert.True(row >= 0, string.Join('\n', rows));

        return (rows[row].IndexOf(text, StringComparison.Ordinal), row);
    }

    private static IEnumerable<string> Names(ComposeScreen compose) =>
        compose.Media.Attachments.Select(attachment => attachment.Name);

    private static IReadOnlyList<string> Texts(Screen screen, int height = Roomy) =>
        [.. screen.Lines(new Drawing(Width, AShell.Now, Height: height)).Select(line => line.Text)];
}
