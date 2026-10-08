using Terminal.Gui.Input;
using Wooly.Core.Errors;
using Wooly.Tests.Fakes;
using Wooly.Tui.Rendering;
using Wooly.Tui.Screens;
using Wooly.Tui.Shell;
using Wooly.Tui.Theme;

namespace Wooly.Tests.Tui;

/// <summary>
///     The attachments screen (story 58): on a terminal too short for a row each, the rows fold into the Media header's
///     line, and <c>⏎</c> or a click on that line pushes a screen listing them — with the keys and the mouse their rows
///     take under the header, so that nothing attached is out of reach on a short terminal (review of #372).
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
    ///     <c>⏎</c> on the folded header pushes the attachments screen, crumbed under the compose, listing the header's
    ///     line unfolded and a row each — the same rows the header draws when there is room, the first one picked.
    /// </summary>
    [Fact]
    public async Task EnterOnTheFoldedHeaderListsTheRows()
    {
        var (shell, _, compose) = await Folded("one.png", "two.png", "three.png");

        shell.Press(ShellKey.Enter);

        var listed = Assert.IsType<AttachmentsScreen>(shell.Screen);
        var rows = Texts(listed);

        Assert.Equal([compose.Crumb, "Media"], shell.Crumbs.TakeLast(2));
        Assert.Contains("  Media  3 of 4 · ctrl-o to add · □ sensitive", rows);
        Assert.StartsWith("    ▌⠶     one.png", Row(rows, "one.png"), StringComparison.Ordinal);
        Assert.StartsWith("     ⠶     two.png", Row(rows, "two.png"), StringComparison.Ordinal);
        Assert.Equal(
            Row(Texts(compose, Roomy), "three.png").TrimEnd(),
            Row(rows, "three.png").TrimEnd());
    }

    /// <summary>With room for the rows, <c>⏎</c> on the header still opens the file browser.</summary>
    [Fact]
    public async Task EnterOnTheUnfoldedHeaderStillBrowses()
    {
        var (shell, _, compose) = await Composing("one.png");

        _ = Texts(compose, Roomy);
        shell.Press(ShellKey.Up);
        shell.Press(ShellKey.Up);
        shell.Press(ShellKey.Enter);

        Assert.IsType<FileBrowserScreen>(shell.Screen);
    }

    /// <summary>
    ///     The row keys work there as under the header: the arrows walk, <c>del</c> takes one off and <c>ctrl-z</c>
    ///     brings it back, <c>shift-↓</c> moves it later, and <c>s</c> toggles sensitive — each on the compose's own
    ///     draft, which is what is sent.
    /// </summary>
    [Fact]
    public async Task TheRowKeysWorkThere()
    {
        var (shell, _, compose) = await Folded("one.png", "two.png", "three.png");

        shell.Press(ShellKey.Enter);
        shell.Move(1);

        Assert.StartsWith("    ▌⠶     two.png", Row(Texts(shell.Screen), "two.png"), StringComparison.Ordinal);

        shell.Press(ShellKey.Delete);

        Assert.Equal(["one.png", "three.png"], Names(compose));

        shell.Press(ShellKey.CtrlZ);

        Assert.Equal(["one.png", "two.png", "three.png"], Names(compose));

        shell.Press(ShellKey.ShiftDown);

        Assert.Equal(["one.png", "three.png", "two.png"], Names(compose));
        Assert.Equal(["one.png", "three.png", "two.png"], Names(Texts(shell.Screen)));

        shell.Press(ShellKey.S);

        Assert.True(compose.Sensitive);
        Assert.Contains("  Media  3 of 4 · ctrl-o to add · ■ sensitive", Texts(shell.Screen));
    }

    /// <summary><c>r</c> there sends a dropped connection up again.</summary>
    [Fact]
    public async Task RRetriesThere()
    {
        var (shell, built, _) = await Folded("one.png", "two.png", "three.png");

        built.Author.Attaching[0].Refuse(Dropped());
        built.Host.Drain();
        shell.Press(ShellKey.Enter);
        shell.Press(ShellKey.R);

        Assert.Equal(4, built.Author.Attaching.Count);
        Assert.Equal(built.Author.Attaching[0].Path, built.Author.Attaching[3].Path);
    }

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

        shell.WriteDescription("A dog");
        shell.Press(ShellKey.Escape);

        Assert.IsType<AttachmentsScreen>(shell.Screen);
        Assert.Equal("A dog", compose.Attachments[1].Description);

        shell.Press(ShellKey.Escape);

        Assert.Same(compose, shell.Screen);
        Assert.Equal(ComposeField.Media, compose.Typing);
    }

    /// <summary>
    ///     <c>ctrl-o</c> there opens the file browser, and what it attaches lands on the compose under the list, listed
    ///     there as it comes back.
    /// </summary>
    [Fact]
    public async Task CtrlOAttachesMoreFromThere()
    {
        var (shell, _, compose) = await Folded("one.png", "two.png", "three.png");

        _files.WriteFile("four.png");
        shell.Press(ShellKey.Enter);
        shell.Press(ShellKey.CtrlO);

        var browser = Assert.IsType<FileBrowserScreen>(shell.Screen);

        browser.Pick(1);
        shell.Press(ShellKey.Enter);

        Assert.IsType<AttachmentsScreen>(shell.Screen);
        Assert.Equal(["one.png", "two.png", "three.png", "four.png"], Names(compose));
        Assert.Contains(Texts(shell.Screen), row => row.Contains("four.png", StringComparison.Ordinal));
    }

    /// <summary>
    ///     In the window: a click on the folded line pushes the list; there a click on a row's <c>x</c> takes it off, a
    ///     click on its quiet mark opens the description editor, and a drag reorders it, live.
    /// </summary>
    [Fact]
    public async Task InTheWindowTheMouseWorksThere()
    {
        using var drawn = await DrawnShell.Of(80, Short + 3, Themes.Plain, new AShell { LaunchedFrom = _files.Path });

        drawn.Shell.Compose();
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

    /// <summary>A connection that dropped while a file was going up, the one failure a retry can mend.</summary>
    private static TransientNetworkException Dropped() =>
        new(new Uri("https://mastodon.social/api/v2/media"), 3, new HttpRequestException());

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
        compose.Attachments.Select(attachment => attachment.Name);

    /// <summary>The names on the listed rows, top to bottom.</summary>
    private static IEnumerable<string> Names(IReadOnlyList<string> rows) =>
        rows.Where(row => row.Contains('⠶', StringComparison.Ordinal))
            .Select(row => row.Split(' ', StringSplitOptions.RemoveEmptyEntries)[1]);

    private static string Row(IReadOnlyList<string> rows, string name) =>
        rows.Single(row => row.Contains(name, StringComparison.Ordinal));

    private static IReadOnlyList<string> Texts(Screen screen, int height = Roomy) =>
        [.. screen.Lines(new Drawing(Width, AShell.Now, Height: height)).Select(line => line.Text)];
}
