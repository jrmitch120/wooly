using Terminal.Gui.Input;
using Wooly.Tests.Fakes;
using Wooly.Tui.Media;
using Wooly.Tui.Screens;
using Wooly.Tui.Theme;

namespace Wooly.Tests.Tui;

/// <summary>
///     The mouse as a terminal reports it: SGR reports read in through Terminal.Gui's own parsing and click-making, the
///     loop deciding what to draw (<see cref="DrawnShell.Reports" />) — on compose's Media rows, the attachments screen
///     and the file browser, where a report made ready for the view under the pointer had hidden what a real terminal
///     does (review of #372).
/// </summary>
/// <remarks>
///     Some terminals report the pointer moving with no button held in the words they report it moving with the left
///     button held (<see cref="Sgr.Drag" />); macOS's was found to (#378). Terminal.Gui then takes the button for held,
///     and makes a click of the next report that says it is not — a wheel notch among them.
/// </remarks>
public class TerminalMouseReportsTests : IDisposable
{
    private readonly TemporaryDirectory _files = new();

    public void Dispose() => _files.Dispose();

    /// <summary>
    ///     In the file browser the wheel only scrolls the list, wherever the pointer is — over the list, the pinned rows
    ///     or the preview — and however the terminal words the pointer drifting between notches: it never moves the
    ///     cursor, chooses, attaches or leaves.
    /// </summary>
    [Theory]
    [InlineData("picture 05")]
    [InlineData("type to filter")]
    [InlineData("preview")]
    public async Task InTheFileBrowserTheWheelOnlyScrolls(string over)
    {
        for (var at = 0; at < 40; at++)
        {
            _files.WriteFile($"picture {at:00}.png");
        }

        var first = Drawn.OnDisk(Path.Combine(_files.Path, "picture 00.png")).Id;

        using var drawn = await DrawnShell.Of(
            120,
            24,
            Themes.Plain,
            new AShell { LaunchedFrom = _files.Path },
            pictures: new FakePictures().HoldingPhotograph(first, 60, 40),
            drawsPictures: true);

        drawn.Shell.Compose();
        drawn.Iterate();
        drawn.Window.NewKeyDownEvent(Key.O.WithCtrl);
        drawn.Iterate();

        var (column, row) = over == "preview" ? (100, 4) : Find(drawn, over);

        for (var notch = 0; notch < 6; notch++)
        {
            drawn.Reports(Sgr.Drag(column, row));
            drawn.Reports(Sgr.Wheel(column, row));
        }

        var browser = Assert.IsType<FileBrowserScreen>(drawn.Shell.Screen);

        // Still on the first file, and nothing chosen: what ⏎ would attach is the file the cursor opened on.
        Assert.Equal(6, drawn.Content.Top);
        Assert.Equal([Path.Combine(_files.Path, "picture 00.png")], browser.Take());
    }

    /// <summary>
    ///     A wheel notch over a row's <c>x</c>, with the pointer reported moving there as some terminals word it, scrolls
    ///     nothing and takes nothing off: it is no click.
    /// </summary>
    [Fact]
    public async Task AWheelNotchOverARowIsNoClick()
    {
        using var drawn = await Composing("one.png", "two.png", "three.png");
        var (column, row) = At(drawn, "two.png", " x ");

        drawn.Reports(Sgr.Drag(column + 1, row));
        drawn.Reports(Sgr.Wheel(column + 1, row));
        drawn.Reports(Sgr.Drag(column + 1, row));
        drawn.Reports(Sgr.Wheel(column + 1, row));

        var compose = Assert.IsType<ComposeScreen>(drawn.Shell.Screen);

        Assert.Equal(["one.png", "two.png", "three.png"], Names(compose));
        Assert.Equal(ComposeField.Post, compose.Typing);
    }

    /// <summary>
    ///     Two clicks on <c>x</c> in quick succession take two rows off — the second the row that took the first one's
    ///     place — though Terminal.Gui reports the second as a double click.
    /// </summary>
    [Fact]
    public async Task TwoQuickClicksOnXTakeTwoRowsOff()
    {
        using var drawn = await Composing("one.png", "two.png", "three.png");
        var (column, row) = At(drawn, "one.png", " x ");

        drawn.Reports(Sgr.Press(column + 1, row), Sgr.Release(column + 1, row));
        drawn.Reports(Sgr.Press(column + 1, row), Sgr.Release(column + 1, row));

        var compose = Assert.IsType<ComposeScreen>(drawn.Shell.Screen);

        Assert.Equal(["three.png"], Names(compose));
        Assert.DoesNotContain(drawn.Rows(), text => text.Contains("two.png", StringComparison.Ordinal));
    }

    /// <summary>The same on the attachments screen, whose rows the window takes the clicks for.</summary>
    [Fact]
    public async Task OnTheAttachmentsScreenTwoQuickClicksOnXTakeTwoRowsOff()
    {
        using var drawn = await Listing("one.png", "two.png", "three.png");
        var (column, row) = At(drawn, "one.png", " x ");

        drawn.Reports(Sgr.Press(column + 1, row), Sgr.Release(column + 1, row));
        drawn.Reports(Sgr.Press(column + 1, row), Sgr.Release(column + 1, row));

        var listed = Assert.IsType<AttachmentsScreen>(drawn.Shell.Screen);

        Assert.Equal(["three.png"], Names(listed.Compose));
    }

    /// <summary>
    ///     A press on a row of the attachments screen picks it, as a press on a row under the Media header does, so the
    ///     bar is on the row being dragged from the moment it is picked up.
    /// </summary>
    [Fact]
    public async Task OnTheAttachmentsScreenAPressPicksTheRow()
    {
        using var drawn = await Listing("one.png", "two.png", "three.png");
        var (column, row) = At(drawn, "three.png", "three.png");

        drawn.Reports(Sgr.Press(column, row));

        Assert.Contains("▌⠶     three.png", drawn.Rows()[row], StringComparison.Ordinal);

        drawn.Reports(Sgr.Drag(column, row - 1));

        Assert.Contains("▌⠶     three.png", drawn.Rows()[row - 1], StringComparison.Ordinal);
    }

    /// <summary>
    ///     The pointer passing over compose with the list of people to mention open leaves it open, however the terminal
    ///     words the pointer moving: only a press outside it closes it (#335).
    /// </summary>
    [Fact]
    public async Task ThePointerPassingOverComposeLeavesTheListOfPeopleOpen()
    {
        using var drawn = await DrawnShell.Of(80, 24, Themes.Plain, new AShell
        {
            Timelines = FakeTimelineReader.Holding(
                APost.With(id: "1", account: "maria@fosstodon.org", author: "Maria Gonzalez")),
        });

        drawn.Shell.Compose();
        drawn.Iterate();

        foreach (var letter in "hi @ma")
        {
            drawn.Window.NewKeyDownEvent(new Key(letter));
        }

        drawn.Iterate();

        Assert.Contains(drawn.Rows(), text => text.Contains("@maria@fosstodon.org", StringComparison.Ordinal));

        drawn.Reports(Sgr.Drag(40, 4), Sgr.Drag(41, 5), Sgr.Drag(42, 6));

        Assert.Contains(drawn.Rows(), text => text.Contains("@maria@fosstodon.org", StringComparison.Ordinal));
    }

    /// <summary>
    ///     A row picked by the pointer carries the selection bar against its grip and nothing else: its name is drawn in
    ///     its usual role, under the header and on the attachments screen alike.
    /// </summary>
    [Fact]
    public async Task APickedRowsNameKeepsItsRole()
    {
        var theme = Themes.Dark;

        using var drawn = await Composing(theme, "one.png", "two.png");
        var (column, row) = At(drawn, "two.png", "two.png");

        drawn.Reports(Sgr.Press(column, row), Sgr.Release(column, row));

        Assert.Equal(theme.For(Role.Selection), drawn.Cell(row, column - 7));
        Assert.Equal(theme.For(Role.Body), drawn.Cell(row, column));
        Assert.Equal(theme.For(Role.Body), drawn.Cell(row, column + 3));
    }

    /// <summary>The same on the attachments screen, where the picked row is not banded either.</summary>
    [Fact]
    public async Task OnTheAttachmentsScreenAPickedRowsNameKeepsItsRole()
    {
        var theme = Themes.Dark;

        using var drawn = await Listing(theme, "one.png", "two.png", "three.png");
        var (column, row) = At(drawn, "one.png", "one.png");

        Assert.Contains("▌⠶     one.png", drawn.Rows()[row], StringComparison.Ordinal);
        Assert.Equal(theme.For(Role.Selection), drawn.Cell(row, column - 7));
        Assert.Equal(theme.For(Role.Body), drawn.Cell(row, column));
        Assert.Equal(theme.For(Role.Body), drawn.Cell(row, column + 3));
    }

    /// <summary>
    ///     Dragging a row through the terminal moves it a row for every row the pointer crosses, drawn as it goes — over
    ///     rows whose pictures a sixel terminal draws through boxes laid over them, and dragged either way.
    /// </summary>
    [Theory]
    [InlineData("four.png", -1)]
    [InlineData("one.png", 1)]
    public async Task DraggingARowMovesItARowForEveryRowCrossed(string name, int way)
    {
        using var drawn = await Composing(Themes.Plain, drawsPictures: true, "one.png", "two.png", "three.png", "four.png");
        var (column, row) = At(drawn, name, name);

        // On the picture rather than the name, which a box is laid over.
        column -= 3;

        Assert.Contains(
            drawn.Content.SubViews.OfType<PictureView>(),
            box => box.Visible && box.FrameToScreen().Contains(column, row));

        drawn.Reports(Sgr.Move(column + 1, row), Sgr.Move(column, row));
        drawn.Reports(Sgr.Press(column, row));

        for (var crossed = 1; crossed <= 3; crossed++)
        {
            drawn.Reports(Sgr.Drag(column, row + (way * crossed)));

            Assert.Contains(name, drawn.Rows()[row + (way * crossed)], StringComparison.Ordinal);
        }

        drawn.Reports(Sgr.Release(column, row + (way * 3)));

        Assert.Contains(name, drawn.Rows()[row + (way * 3)], StringComparison.Ordinal);
        Assert.Equal(way < 0 ? 0 : 3, Names(Assert.IsType<ComposeScreen>(drawn.Shell.Screen)).ToList().IndexOf(name));
    }

    /// <summary>A click on a row's <c>x</c> through the terminal takes it off on the very pass the click is read in.</summary>
    [Fact]
    public async Task AClickOnXTakesTheRowOffAtOnce()
    {
        using var drawn = await Composing(Themes.Plain, drawsPictures: true, "one.png", "two.png", "three.png");
        var (column, row) = At(drawn, "two.png", " x ");

        drawn.Reports(Sgr.Move(column + 1, row));
        drawn.Reports(Sgr.Press(column + 1, row));
        drawn.Reports(Sgr.Release(column + 1, row));

        Assert.DoesNotContain(drawn.Rows(), text => text.Contains("two.png", StringComparison.Ordinal));
        Assert.Contains("three.png", drawn.Rows()[row], StringComparison.Ordinal);
    }

    private Task<DrawnShell> Composing(params string[] names) => Composing(Themes.Plain, false, names);

    private Task<DrawnShell> Composing(ITheme theme, params string[] names) => Composing(theme, false, names);

    /// <summary>
    ///     An 80 × 24 window on a fresh compose with <paramref name="names" /> dropped onto it, each a picture the
    ///     terminal draws where <paramref name="drawsPictures" /> says it draws sixel — and nothing drawn but by the loop.
    /// </summary>
    private async Task<DrawnShell> Composing(ITheme theme, bool drawsPictures, params string[] names)
    {
        var paths = names.Select(name => _files.WriteFile(name)).ToArray();
        var pictures = paths.Aggregate(
            new FakePictures(),
            (held, path) => held.HoldingPhotograph(Drawn.Attaching(path).Id, 40, 30));

        var drawn = await DrawnShell.Of(80, 24, theme, pictures: pictures, drawsPictures: drawsPictures);

        drawn.Shell.Compose();
        drawn.Iterate();

        foreach (var path in paths)
        {
            Assert.True(drawn.Shell.Paste(path));
        }

        drawn.Built.Host.Drain();
        drawn.Iterate();

        return drawn;
    }

    private Task<DrawnShell> Listing(params string[] names) => Listing(Themes.Plain, names);

    /// <summary>
    ///     The attachments screen, pushed by a click through the terminal on the line a short window folded
    ///     <paramref name="names" /> into.
    /// </summary>
    private async Task<DrawnShell> Listing(ITheme theme, params string[] names)
    {
        var drawn = await DrawnShell.Of(80, 18, theme);

        drawn.Shell.Compose();
        drawn.Iterate();

        foreach (var name in names)
        {
            Assert.True(drawn.Shell.Paste(_files.WriteFile(name)));
        }

        drawn.Iterate();

        var (column, row) = Find(drawn, $"{names.Length} of 4");

        drawn.Reports(Sgr.Press(column, row), Sgr.Release(column, row));

        Assert.IsType<AttachmentsScreen>(drawn.Shell.Screen);

        return drawn;
    }

    private static IEnumerable<string> Names(ComposeScreen compose) =>
        compose.Attachments.Select(attachment => attachment.Name);

    /// <summary>Where <paramref name="text" /> starts on the drawn row with <paramref name="on" /> on it.</summary>
    private static (int Column, int Row) At(DrawnShell drawn, string on, string text)
    {
        var rows = drawn.Rows();
        var row = Array.FindIndex(rows, line => line.Contains(on, StringComparison.Ordinal));

        Assert.True(row >= 0, string.Join('\n', rows));

        return (rows[row].IndexOf(text, StringComparison.Ordinal), row);
    }

    /// <summary>Where <paramref name="text" /> is drawn, on the first row with it.</summary>
    private static (int Column, int Row) Find(DrawnShell drawn, string text) => At(drawn, text, text);
}
