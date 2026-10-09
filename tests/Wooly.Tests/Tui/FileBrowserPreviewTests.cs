using Terminal.Gui.Input;
using Wooly.Tests.Fakes;
using Wooly.Tui.Media;
using Wooly.Tui.Rendering;
using Wooly.Tui.Screens;
using Wooly.Tui.Shell;
using Wooly.Tui.Theme;

namespace Wooly.Tests.Tui;

/// <summary>
///     The file browser's preview (#382): on a terminal that draws and is wide enough for the pane, the picture under
///     the cursor sits top left in the pane, level with the list's first row on the page, read off the disk through
///     the picture path the feed's pictures take — and nothing is drawn, and nothing moves, anywhere else.
/// </summary>
public class FileBrowserPreviewTests : IDisposable
{
    /// <summary>A panel wide enough for the pane: the list takes eleven twentieths of it.</summary>
    private const int Width = 120;

    private const int Height = 30;

    /// <summary>Where the pane's picture starts: past the list, its rule and a column of air.</summary>
    private const int PaneAt = (Width * 11 / 20) + 2;

    /// <summary>The row the list's first entry, <c>..</c>, is on: under the folder, the filter and the rule.</summary>
    private const int FirstRow = 3;

    private readonly TemporaryDirectory _files = new();

    public void Dispose() => _files.Dispose();

    /// <summary>
    ///     The picture under the cursor, its pixels here, is set top left in the pane on the list's first row, as wide
    ///     as the pane allows at its own proportions, with its name and size under it.
    /// </summary>
    [Fact]
    public async Task ThePictureUnderTheCursorIsSetTopLeftInThePane()
    {
        var cat = _files.WriteFile("a-cat.png", "twenty-five bytes of cat.");
        _files.WriteFile("b-dog.png");
        var shell = await Browsing();
        var pictures = new FakePictures().Holding(Drawn.OnDisk(cat).Id, 400, 200);

        var lines = Lines(shell, pictures, ARaster.Sixel());
        var inset = Assert.Single(lines.SelectMany(line => line.Insets));

        Assert.Same(lines[FirstRow], lines.Single(line => line.Insets.Count > 0));
        Assert.Equal(Drawn.OnDisk(cat), inset.Drawn);
        Assert.Equal(PaneAt, inset.Column);
        Assert.Equal(Width - PaneAt - 2, inset.Columns);
        Assert.Equal(Drawn.OnDisk(cat), lines[FirstRow].Wants);
        Assert.EndsWith("a-cat.png · 25 B", lines[FirstRow + inset.Rows + 1].Text.TrimEnd(), StringComparison.Ordinal);
    }

    /// <summary>Moving the cursor — here by filtering, which lands it on the closest match — moves the preview with it.</summary>
    [Fact]
    public async Task MovingTheCursorChangesThePreview()
    {
        var cat = _files.WriteFile("a-cat.png");
        var dog = _files.WriteFile("b-dog.png");
        var shell = await Browsing();
        var pictures = new FakePictures().Holding(Drawn.OnDisk(cat).Id, 400, 200).Holding(Drawn.OnDisk(dog).Id, 300, 300);

        foreach (var letter in "dog")
        {
            shell.Type(letter);
        }

        var lines = Lines(shell, pictures, ARaster.Sixel());

        Assert.Equal(Drawn.OnDisk(dog), Assert.Single(lines.SelectMany(line => line.Insets)).Drawn);
        Assert.Equal(Drawn.OnDisk(dog), lines[FirstRow].Wants);
    }

    /// <summary>
    ///     While the picture is on its way the row asks for it and nothing is set into the pane; once it is here the
    ///     rows are the same rows, as many of them, so the list beside it does not move.
    /// </summary>
    [Fact]
    public async Task APictureArrivingChangesNoRowsHeight()
    {
        var cat = _files.WriteFile("a-cat.png");
        _files.WriteFile("b-dog.png");
        var shell = await Browsing();

        var before = Lines(shell, new FakePictures(), ARaster.Sixel());
        var after = Lines(shell, new FakePictures().Holding(Drawn.OnDisk(cat).Id, 400, 200), ARaster.Sixel());

        Assert.Empty(before.SelectMany(line => line.Insets));
        Assert.Equal(Drawn.OnDisk(cat), before[FirstRow].Wants);
        Assert.Equal(before.Count, after.Count);
        Assert.Equal(
            before.Select(line => line.Text.PadRight(PaneAt)[..PaneAt]),
            after.Select(line => line.Text.PadRight(PaneAt)[..PaneAt]));
    }

    /// <summary>
    ///     While the picture is on its way, a muted "loading preview" holds its place top left in the pane, on the row the
    ///     picture will be set on; once it is here, the picture takes the place and the words are gone.
    /// </summary>
    [Fact]
    public async Task WhileThePictureIsOnItsWayThePaneSaysSo()
    {
        var cat = _files.WriteFile("a-cat.png");
        _files.WriteFile("b-dog.png");
        var shell = await Browsing();

        var loading = Lines(shell, new FakePictures(), ARaster.Sixel());
        var loaded = Lines(shell, new FakePictures().Holding(Drawn.OnDisk(cat).Id, 400, 200), ARaster.Sixel());

        var said = Assert.Single(loading, line => line.Text.Contains("loading preview", StringComparison.Ordinal));

        Assert.Same(loading[FirstRow], said);
        Assert.Equal(PaneAt, said.Text.IndexOf("loading preview", StringComparison.Ordinal));
        Assert.Equal(Role.Muted, Assert.Single(said.Spans, span => span.Text == "loading preview").Role);
        Assert.DoesNotContain(loaded, line => line.Text.Contains("loading preview", StringComparison.Ordinal));
    }

    /// <summary>
    ///     The cursor's row is picked out by the band alone, its name in its usual role, and the band stops at the list's
    ///     edge rather than running on under the pane.
    /// </summary>
    [Fact]
    public async Task TheCursorsRowIsBandedOnlyAcrossTheList()
    {
        _files.WriteFile("a-cat.png");
        var shell = await Browsing();

        var row = Lines(shell, new FakePictures(), ARaster.Sixel())[FirstRow + 1];

        Assert.True(row.Picked);
        Assert.Equal(Role.Body, Assert.Single(row.Spans, span => span.Text.StartsWith("a-cat.png", StringComparison.Ordinal)).Role);
        Assert.Equal(PaneAt - 2, row.BandsTo);
        Assert.Equal('│', row.Text[PaneAt - 2]);
    }

    /// <summary>Wiring: in the window, the band is painted across the list and not across the pane.</summary>
    [Fact]
    public async Task InTheWindowTheBandStopsAtTheList()
    {
        _files.WriteFile("a-cat.png");
        var theme = Themes.Dark;

        using var drawn = await DrawnShell.Of(120, 24, theme, new AShell { LaunchedFrom = _files.Path });

        drawn.Shell.Compose();
        drawn.Redraw();
        drawn.Press(Key.O.WithCtrl);

        var row = Find(drawn, "a-cat.png");
        var name = drawn.Rows()[row].IndexOf("a-cat.png", StringComparison.Ordinal);
        var rule = drawn.Rows()[row].IndexOf('│', name);

        Assert.Equal(theme.Banded(Role.Body), drawn.Cell(row, name));
        Assert.Equal(theme.Banded(Role.Body), drawn.Cell(row, rule - 1));
        Assert.Equal(theme.For(Role.Body), drawn.Cell(row, rule + 2));
    }

    /// <summary>
    ///     A folder, a video, or a terminal that cannot draw: nothing set into the pane and nothing asked for, and the
    ///     list drawn exactly as where a picture is.
    /// </summary>
    [Fact]
    public async Task NothingIsDrawnOrWantedOnATerminalThatCannotDraw()
    {
        var cat = _files.WriteFile("a-cat.png");
        var shell = await Browsing();
        var pictures = new FakePictures().Holding(Drawn.OnDisk(cat).Id, 400, 200);

        var undrawn = Lines(shell, pictures, Raster.None);

        Assert.Empty(undrawn.SelectMany(line => line.Insets));
        Assert.All(undrawn, line => Assert.Null(line.Wants));
        Assert.Equal(
            Lines(shell, pictures, ARaster.Sixel()).Select(line => line.Text.PadRight(PaneAt)[..PaneAt]),
            undrawn.Select(line => line.Text.PadRight(PaneAt)[..PaneAt]));
    }

    /// <summary>A folder or a video under the cursor has no picture to show.</summary>
    [Theory]
    [InlineData("a-folder", true)]
    [InlineData("a-clip.mp4", false)]
    public async Task WhatHasNoPictureShowsNone(string name, bool folder)
    {
        if (folder)
        {
            Directory.CreateDirectory(Path.Combine(_files.Path, name));
        }
        else
        {
            _files.WriteFile(name);
        }

        _files.WriteFile("b-dog.png");

        var shell = await Browsing();
        var lines = Lines(shell, new FakePictures(), ARaster.Sixel());

        Assert.Empty(lines.SelectMany(line => line.Insets));
        Assert.All(lines, line => Assert.Null(line.Wants));
    }

    /// <summary>A terminal too narrow for the pane has no preview.</summary>
    [Fact]
    public async Task ANarrowTerminalHasNoPreview()
    {
        var cat = _files.WriteFile("a-cat.png");
        var shell = await Browsing();

        var lines = shell.Screen.Lines(new Drawing(
            78,
            AShell.Now,
            new FakePictures().Holding(Drawn.OnDisk(cat).Id, 400, 200),
            ARaster.Sixel(),
            Height: Height));

        Assert.Empty(lines.SelectMany(line => line.Insets));
        Assert.All(lines, line => Assert.Null(line.Wants));
    }

    /// <summary>
    ///     Scrolled down a long folder, the preview sits level with the list's first row on the page — the first under the
    ///     folder, the filter and the rule, which stay pinned over the top of it — rather than of the list, so that the
    ///     picture under the cursor is always on the page with it.
    /// </summary>
    [Fact]
    public async Task ScrolledDownALongFolderThePreviewSitsAtTheTopOfThePage()
    {
        for (var at = 0; at < 60; at++)
        {
            _files.WriteFile($"picture {at:00}.png");
        }

        var shell = await Browsing();
        var lines = shell.Screen.Lines(new Drawing(Width, AShell.Now, new FakePictures(), ARaster.Sixel(), Height: Height)
        {
            Top = 40,
        });

        Assert.Equal(40 + FirstRow, lines.ToList().FindIndex(line => line.Wants is not null));
    }

    /// <summary>
    ///     Wiring: in the window, moved far enough down a long folder that the list has scrolled, the picture under the
    ///     cursor is still drawn through one of the content panel's boxes, on the page's first row of the list, under the
    ///     pinned folder, filter and rule.
    /// </summary>
    [Fact]
    public async Task InTheWindowThePictureUnderTheCursorIsDrawnOnThePage()
    {
        for (var at = 0; at < 40; at++)
        {
            _files.WriteFile($"picture {at:00}.png");
        }

        var last = Path.Combine(_files.Path, "picture 30.png");

        using var drawn = await DrawnShell.Of(
            120,
            24,
            Themes.Plain,
            new AShell { LaunchedFrom = _files.Path },
            pictures: new FakePictures().HoldingPhotograph(Drawn.OnDisk(last).Id, 60, 40),
            drawsPictures: true);

        drawn.Shell.Compose();
        drawn.Redraw();
        drawn.Press(Key.O.WithCtrl);

        for (var at = 0; at < 30; at++)
        {
            drawn.Press(Key.CursorDown);
        }

        var box = Assert.Single(drawn.Content.SubViews.OfType<PictureView>(), view => view.Visible);

        Assert.Equal(Drawn.OnDisk(last).Id, box.PictureId);
        Assert.Equal(drawn.Content.FrameToScreen().Y + 1 + FirstRow, box.FrameToScreen().Y);
        Assert.Contains("type to filter", drawn.Rows()[drawn.Content.FrameToScreen().Y + 2], StringComparison.Ordinal);
        Assert.DoesNotContain(drawn.Rows(), row => row.Contains("picture 00", StringComparison.Ordinal));

        // A filter that leaves only the one brings the page back to the top on the frame it is typed, and the picture
        // with it, level with the first row of the list.
        drawn.Press(Key.D3);
        drawn.Press(Key.D0);

        box = Assert.Single(drawn.Content.SubViews.OfType<PictureView>(), view => view.Visible);

        Assert.Equal(Find(drawn, "◂ .."), box.FrameToScreen().Y);
    }

    private static int Find(DrawnShell drawn, string text) =>
        Array.FindIndex(drawn.Rows(), row => row.Contains(text, StringComparison.Ordinal));

    /// <summary>The browser opened over a fresh post, launched from the test's own folder.</summary>
    private async Task<Wooly.Tui.Shell.Shell> Browsing()
    {
        var built = new AShell { LaunchedFrom = _files.Path };
        var shell = await built.Opened();

        ComposeRows.Open(shell, ComposeFor.Post);
        built.Host.Drain();
        shell.Press(ShellKey.CtrlO);

        Assert.IsType<FileBrowserScreen>(shell.Screen);

        return shell;
    }

    private static IReadOnlyList<Line> Lines(Wooly.Tui.Shell.Shell shell, IPictures pictures, Raster raster) =>
        shell.Screen.Lines(new Drawing(Width, AShell.Now, pictures, raster, Height: Height));
}
