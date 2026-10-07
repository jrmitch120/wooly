using Wooly.Tests.Fakes;
using Wooly.Tui.Media;
using Wooly.Tui.Rendering;
using Wooly.Tui.Screens;
using Wooly.Tui.Theme;

namespace Wooly.Tests.Tui;

/// <summary>
///     Pictures on a Kitty terminal, drawn as placeholder cells in the content panel's own rows, read back off the
///     whole shell drawn headless (#292, ADR-0022). Wiring only: what the terminal is sent, prepared and told to forget
///     is <see cref="Placing" />'s, and tested there (<see cref="PlacingTests" />, #362). The pixels themselves are a
///     manual smoke test (ADR-0016).
/// </summary>
public class KittyPictureTests
{
    private const string Placeholder = "\U0010EEEE";

    /// <summary>Every cell of a box names its picture, its row and its column.</summary>
    [Fact]
    public async Task ABoxIsDrawnAsPlaceholderCellsNamingThePictureAndEachCellsPlace()
    {
        var terminal = new FakeTerminalImages();

        using var drawn = await Drawn(terminal);

        var sent = Assert.Single(terminal.Transmitted);
        var cells = Boxed(drawn);

        Assert.Equal(sent.Rows * sent.Columns, cells.Count);

        var top = cells.Min(cell => cell.Row);
        var left = cells.Min(cell => cell.Column);

        Assert.All(cells, cell =>
        {
            Assert.Equal(sent.Id, cell.Id);
            Assert.Equal(KittyPlaceholder.Of(cell.Row - top, cell.Column - left), cell.Grapheme);
        });

        // And the id reaches the terminal as a 24-bit foreground, which is the only way it can tell whose cell it is.
        Assert.Contains($"38;2;{(sent.Id >> 16) & 0xFF};{(sent.Id >> 8) & 0xFF};{sent.Id & 0xFF}m", drawn.Ansi());
    }

    /// <summary>
    ///     A box half off the top of the page still has its lower rows painted, numbered as they were, so the terminal
    ///     crops the picture rather than it blinking out. The painting is the view's; what is sent and placed is
    ///     <see cref="Placing" />'s (<see cref="PlacingTests" />).
    /// </summary>
    [Fact]
    public async Task ABoxHalfOffTheTopStillDrawsItsLowerRows()
    {
        var terminal = new FakeTerminalImages();

        using var drawn = await Drawn(terminal);

        var sent = Assert.Single(terminal.Transmitted);
        var firstBoxRow = Boxed(drawn).Min(cell => cell.Row);

        // Scrolled until the box's top two rows are above the page: the content's first row is the screen's second.
        for (var notch = 0; notch < 100 && drawn.Content.Top < firstBoxRow - 1 + 2; notch++)
        {
            drawn.Wheel(RailLines.Width + 4, 3);
        }

        Assert.Equal(firstBoxRow - 1 + 2, drawn.Content.Top);

        var cells = Boxed(drawn);

        Assert.Equal((sent.Rows - 2) * sent.Columns, cells.Count);
        Assert.Equal(KittyPlaceholder.Of(2, 0), cells.MinBy(cell => (cell.Row, cell.Column))!.Grapheme);
    }

    /// <summary>
    ///     A terminal that answers that it speaks Kitty graphics, but is not known by name to draw its placeholders,
    ///     draws through a box: WezTerm answers yes and prints the placeholders as boxes (#292, ADR-0022).
    /// </summary>
    [Fact]
    public async Task ATerminalAnsweringKittyButNotKnownByNameDrawsThroughABox()
    {
        var terminal = new FakeTerminalImages();

        using var drawn = await DrawnShell.Of(
            80,
            24,
            Themes.Plain,
            AShellWithAPicture(),
            pictures: new FakePictures().Holding("m1", 800, 200),
            kittyImages: terminal,
            answersKitty: true);

        Assert.Empty(terminal.Transmitted);
        Assert.Empty(Boxed(drawn));
        Assert.Single(drawn.Content.SubViews.OfType<PictureView>(), view => view.Visible);
    }

    /// <summary>
    ///     A window shrunk too small to draw the page in at all lets go of every box the content panel holds, so that
    ///     no picture is left drawn over whatever replaces the page — and draws it again once it grows back.
    /// </summary>
    [Fact]
    public async Task AWindowShrunkToNothingLetsGoOfEveryBoxAndPlacesThemAgainOnceItGrows()
    {
        using var drawn = await DrawnShell.Of(
            80,
            24,
            Themes.Plain,
            AShellWithAPicture(),
            pictures: new FakePictures().Holding("m1", 800, 200),
            drawsPictures: true);

        Assert.Single(drawn.Content.SubViews.OfType<PictureView>(), view => view.Visible);

        drawn.Application.Driver!.SetScreenSize(80, 1);
        drawn.Redraw();

        Assert.All(drawn.Content.SubViews.OfType<PictureView>(), view => Assert.Null(view.PictureId));

        drawn.Application.Driver!.SetScreenSize(80, 24);
        drawn.Redraw();

        Assert.Single(drawn.Content.SubViews.OfType<PictureView>(), view => view.Visible);
    }

    /// <summary>A sixel terminal still draws through the boxes, and sends nothing to a Kitty image store.</summary>
    [Fact]
    public async Task ASixelTerminalStillDrawsThroughTheBoxes()
    {
        var terminal = new FakeTerminalImages();

        using var drawn = await DrawnShell.Of(
            80,
            24,
            Themes.Plain,
            AShellWithAPicture(),
            pictures: new FakePictures().Holding("m1", 800, 600),
            drawsPictures: true,
            kittyImages: terminal);

        Assert.Single(drawn.Content.SubViews.OfType<PictureView>(), view => view.Visible);
        Assert.Empty(terminal.Transmitted);
        Assert.Empty(Boxed(drawn));
    }

    private static Task<DrawnShell> Drawn(FakeTerminalImages terminal) => DrawnShell.Of(
        80,
        24,
        Themes.Plain,
        AShellWithAPicture(),
        // A wide picture, so that its box is a few rows and fits on the page whole.
        pictures: new FakePictures().Holding("m1", 800, 200),
        kittyImages: terminal,
        drawsPlaceholders: true);

    private static AShell AShellWithAPicture() => new()
    {
        Timelines = FakeTimelineReader.Holding(
            APost.With(id: "110", media: [APost.APicture("m1")]),
            APost.With(id: "220"),
            APost.With(id: "330"),
            APost.With(id: "440"),
            APost.With(id: "550")),
    };

    /// <summary>Every placeholder cell on screen, with where it is and the image id its colour carries.</summary>
    private static List<BoxCell> Boxed(DrawnShell drawn)
    {
        var contents = drawn.Application.Driver!.Contents!;
        var cells = new List<BoxCell>();

        for (var row = 0; row < contents.GetLength(0); row++)
        {
            for (var column = 0; column < contents.GetLength(1); column++)
            {
                var grapheme = contents[row, column].Grapheme;

                if (grapheme.StartsWith(Placeholder, StringComparison.Ordinal))
                {
                    var colour = drawn.Cell(row, column).Foreground;

                    cells.Add(new BoxCell(row, column, grapheme, (colour.R << 16) | (colour.G << 8) | colour.B));
                }
            }
        }

        return cells;
    }

    private static int Row(DrawnShell drawn, string text) => Array.FindIndex(drawn.Rows(), row => row.Contains(text));

    private sealed record BoxCell(int Row, int Column, string Grapheme, int Id);
}
