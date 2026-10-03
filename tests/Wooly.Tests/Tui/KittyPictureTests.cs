using Wooly.Tests.Fakes;
using Wooly.Tui.Media;
using Wooly.Tui.Rendering;
using Wooly.Tui.Screens;
using Wooly.Tui.Theme;

namespace Wooly.Tests.Tui;

/// <summary>
///     Pictures on a Kitty terminal, drawn as placeholder cells in the content panel's own rows, read back off the
///     whole shell drawn headless (#292, ADR-0022). The pixels themselves are a manual smoke test (ADR-0016); what the
///     cells say and what the terminal is sent are here.
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
    ///     The point of it all: a scroll moves the picture with the text, in the same frame, and sends the terminal no
    ///     image data to do it.
    /// </summary>
    [Fact]
    public async Task AScrollMovesThePictureWithTheTextAndTransmitsNothing()
    {
        var terminal = new FakeTerminalImages();

        using var drawn = await Drawn(terminal);

        var before = Boxed(drawn);
        var caption = Row(drawn, "A cartoon sheep");

        drawn.Wheel(RailLines.Width + 4, 3);

        var after = Boxed(drawn);

        Assert.Equal(1, drawn.Content.Top);
        Assert.Equal(caption - 1, Row(drawn, "A cartoon sheep"));
        Assert.Single(terminal.Transmitted);

        // The whole box, a row higher, and nothing else about any cell of it changed.
        Assert.Equal([.. before.Select(cell => cell with { Row = cell.Row - 1 })], after);
    }

    /// <summary>
    ///     A box half off the top of the page still draws its lower rows, numbered as they were, so the terminal
    ///     crops the picture rather than it blinking out.
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
        Assert.Single(terminal.Transmitted);
    }

    /// <summary>
    ///     A picture below the page is encoded before it is scrolled to, and only sent once it is on the page — so that
    ///     it comes into view with the text around it rather than a frame or two behind (#292).
    /// </summary>
    [Fact]
    public async Task APictureJustBelowThePageIsEncodedAheadAndSentOnlyOnceItIsOnThePage()
    {
        var terminal = new FakeTerminalImages();
        var encoding = new List<Action>();

        using var drawn = await DrawnShell.Of(
            80,
            24,
            Themes.Plain,
            new AShell
            {
                Timelines = FakeTimelineReader.Holding(
                    APost.With(id: "110"),
                    APost.With(id: "220"),
                    APost.With(id: "330"),
                    APost.With(id: "440"),
                    APost.With(id: "550", media: [APost.APicture("m1")])),
            },
            pictures: FakePictures.With().Holding("m1", 800, 200),
            kittyImages: terminal,
            drawsPlaceholders: true,
            encoding: encoding.Add);

        Assert.DoesNotContain(drawn.Rows(), row => row.Contains("A cartoon sheep"));

        // Encoded already, though nothing is sent while the box is off the page.
        Assert.Single(encoding)();
        drawn.Redraw();

        Assert.Empty(terminal.Transmitted);

        for (var notch = 0; notch < 100 && Boxed(drawn).Count == 0; notch++)
        {
            drawn.Wheel(RailLines.Width + 4, 3);
        }

        // Sent on the very frame its first row came onto the page, with nothing more to encode.
        Assert.Single(terminal.Transmitted);
        Assert.Single(encoding);
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
            pictures: FakePictures.With().Holding("m1", 800, 200),
            kittyImages: terminal,
            answersKitty: true);

        Assert.Empty(terminal.Transmitted);
        Assert.Empty(Boxed(drawn));
        Assert.Single(drawn.Content.SubViews.OfType<PictureView>(), view => view.Visible);
    }

    /// <summary>
    ///     Where placeholders are drawn Terminal.Gui draws no pixels of its own: the picture is the placeholders, and a
    ///     <see cref="PictureView" /> drawing as well would be the 11 MB a scroll this replaced.
    /// </summary>
    [Fact]
    public async Task NoPictureViewDrawsOnAKittyTerminal()
    {
        using var drawn = await Drawn(new FakeTerminalImages());

        Assert.DoesNotContain(drawn.Content.SubViews.OfType<PictureView>(), view => view.Visible);
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
            pictures: FakePictures.With().Holding("m1", 800, 600),
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
        pictures: FakePictures.With().Holding("m1", 800, 200),
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
