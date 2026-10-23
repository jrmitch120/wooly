using System.Drawing;
using Terminal.Gui.Input;
using Wooly.Core.Posts;
using Wooly.Tests.Fakes;
using Wooly.Tui.Media;
using Wooly.Tui.Rendering;
using Wooly.Tui.Screens;
using Wooly.Tui.Shell;
using Wooly.Tui.Theme;

namespace Wooly.Tests.Tui;

/// <summary>
///     Pictures in a narrowed shell (#417): a sixel, Kitty's placeholder cells and Kitty drawn through a box all land
///     inside the content panel of a shell narrowed and centred by its <b>Shell width</b>, never in the margin and
///     never over a frame — and are drawn again to fit once the width changes. Read back off the whole shell drawn
///     headless, as the picture tests at full width are (<see cref="KittyPictureTests" />,
///     <see cref="PictureResendTests" />).
/// </summary>
public class NarrowedShellPictureTests
{
    private const string Placeholder = "\U0010EEEE";

    /// <summary>
    ///     The first column inside the content panel of a shell narrowed to 180 columns on a 200-column terminal: the
    ///     margin's 10, the rail's 20, and the panel's left edge.
    /// </summary>
    private const int FirstInside = 10 + RailLines.Width + 1;

    /// <summary>The last column inside that panel: the one before its right edge, which is the shell's last column.</summary>
    private const int LastInside = 188;

    [Fact]
    public async Task ASixelsCellsFallInsideTheNarrowedContentPanel()
    {
        using var drawn = await Narrowed(drawsPictures: true);

        var cells = BoxCells(drawn);

        Assert.NotEmpty(cells);
        Assert.All(cells, cell => Assert.InRange(cell.X, FirstInside, LastInside));

        // And it takes the panel's width: the picture is wider than any panel, so the panel is what bounds it.
        Assert.Equal(LastInside, cells.Max(cell => cell.X));
    }

    /// <summary>
    ///     What the terminal is let draw a sixel in is cut to the narrowed panel, not to the whole screen: where the
    ///     driver cuts a picture at the edge of what it may draw, the edge is the panel's.
    /// </summary>
    [Fact]
    public async Task ASixelIsClippedToTheNarrowedContentPanelNotTheScreen()
    {
        using var drawn = await Narrowed(drawsPictures: true);

        var images = drawn.Application.Driver!.GetOutputBuffer().GetRasterImages();

        Assert.NotEmpty(images);
        Assert.All(images, image =>
        {
            var clip = Assert.IsType<Terminal.Gui.Drawing.Region>(image.Clip).GetBounds();

            Assert.InRange(clip.Left, FirstInside, LastInside);
            Assert.InRange(clip.Right - 1, FirstInside, LastInside);
        });
    }

    /// <summary>
    ///     A step either way, drawn as the running loop draws it, sends the sixel again in the frame it lands — at the new
    ///     panel's origin and width, never left where the old shell had it.
    /// </summary>
    [Fact]
    public async Task AStepSendsTheSixelAgainAtTheNewWidthAndPlace()
    {
        using var drawn = await AtFullWidth(drawsPictures: true);

        drawn.DrawAsTheLoopDraws();
        drawn.PressAsTheLoopDraws(new Key('-'));

        AssertSentInTheFrameSaying(drawn, "Width 90%");
        Assert.Equal((FirstInside + 1, LastInside), Across(BoxCells(drawn)));

        drawn.PressAsTheLoopDraws(new Key('+'));

        // The whole terminal again: the panel's inside from just past the rail's 20 columns and the panel's left edge,
        // to the one before its right edge on the terminal's last column.
        AssertSentInTheFrameSaying(drawn, "Width 100%");
        Assert.Equal((RailLines.Width + 2, 198), Across(BoxCells(drawn)));
    }

    /// <summary>
    ///     The last frame written sent the sixel once — and is the frame that said <paramref name="said" />, not one
    ///     before it: a pass that writes nothing leaves the last frame's bytes standing.
    /// </summary>
    private static void AssertSentInTheFrameSaying(DrawnShell drawn, string said)
    {
        Assert.Contains(said, drawn.Application.Driver!.GetOutput().GetLastOutput(), StringComparison.Ordinal);
        Assert.Equal(1, drawn.SixelsSent());
    }

    /// <summary>
    ///     On a terminal drawing Kitty's placeholders, every placeholder cell is inside the narrowed panel, and the box
    ///     takes the panel's width — numbered from its own first column, wherever the shell has put it.
    /// </summary>
    [Fact]
    public async Task KittyPlaceholderCellsFallInsideTheNarrowedContentPanel()
    {
        var terminal = new FakeTerminalImages();

        using var drawn = await Narrowed(kittyImages: terminal, drawsPlaceholders: true);

        var sent = terminal.Transmitted[^1];
        var cells = PlaceholderCells(drawn);

        Assert.Equal(sent.Rows * sent.Columns, cells.Count);
        Assert.All(cells, cell => Assert.InRange(cell.Column, FirstInside, LastInside));
        Assert.Equal(LastInside, cells.Max(cell => cell.Column));

        var top = cells.Min(cell => cell.Row);
        var left = cells.Min(cell => cell.Column);

        Assert.All(cells, cell =>
        {
            Assert.Equal(sent.Id, cell.Id);
            Assert.Equal(KittyPlaceholder.Of(cell.Row - top, cell.Column - left), cell.Grapheme);
        });
    }

    /// <summary>
    ///     As placeholders, a step either way paints the picture's cells in the new panel, naming an image sent sized to
    ///     it: one sent for the narrower panel when narrowed, and the one the terminal already holds for the whole width
    ///     when widened back.
    /// </summary>
    [Fact]
    public async Task AStepPaintsKittyPlaceholdersAgainAtTheNewWidthAndPlace()
    {
        var terminal = new FakeTerminalImages();

        using var drawn = await AtFullWidth(kittyImages: terminal, drawsPlaceholders: true);

        drawn.DrawAsTheLoopDraws();
        drawn.PressAsTheLoopDraws(new Key('-'));

        // From just past the panel's left edge to just before its right one.
        Assert.Equal((FirstInside + 1, LastInside), Across(PlaceholderCells(drawn)));
        Assert.Equal(157, Named(PlaceholderCells(drawn)).Columns);

        drawn.PressAsTheLoopDraws(new Key('+'));

        Assert.Equal((RailLines.Width + 2, 198), Across(PlaceholderCells(drawn)));
        Assert.Equal(177, Named(PlaceholderCells(drawn)).Columns);

        // The image the cells name, of those the terminal was sent.
        FakeTerminalImages.Transmission Named(List<PlaceholderCell> cells) =>
            Assert.Single(terminal.Transmitted, sent => sent.Id == cells[0].Id);
    }

    /// <summary>
    ///     On a terminal answering Kitty but not known to draw its placeholders, the picture is drawn through a box —
    ///     and the box, and what the terminal is let draw it in, are inside the narrowed panel.
    /// </summary>
    [Fact]
    public async Task AKittyPictureDrawnThroughABoxFallsInsideTheNarrowedContentPanel()
    {
        var terminal = new FakeTerminalImages();

        using var drawn = await Narrowed(kittyImages: terminal, answersKitty: true);

        var cells = BoxCells(drawn);

        Assert.Empty(terminal.Transmitted);
        Assert.Empty(PlaceholderCells(drawn));
        Assert.NotEmpty(cells);
        Assert.All(cells, cell => Assert.InRange(cell.X, FirstInside, LastInside));
        Assert.Equal(LastInside, cells.Max(cell => cell.X));
    }

    /// <summary>
    ///     Through a Kitty box, a step either way places the picture again in the frame it lands, at the new panel's
    ///     origin and width.
    /// </summary>
    [Fact]
    public async Task AStepPlacesAKittyBoxAgainAtTheNewWidthAndPlace()
    {
        using var drawn = await AtFullWidth(answersKitty: true);

        drawn.DrawAsTheLoopDraws();
        drawn.PressAsTheLoopDraws(new Key('-'));

        AssertPlacedInTheFrameSaying(drawn, "Width 90%");
        Assert.Equal((FirstInside + 1, LastInside), Across(BoxCells(drawn)));

        drawn.PressAsTheLoopDraws(new Key('+'));

        AssertPlacedInTheFrameSaying(drawn, "Width 100%");
        Assert.Equal((RailLines.Width + 2, 198), Across(BoxCells(drawn)));
    }

    /// <summary>The last frame written placed a Kitty image, and is the frame that said <paramref name="said" />.</summary>
    private static void AssertPlacedInTheFrameSaying(DrawnShell drawn, string said)
    {
        var frame = drawn.Application.Driver!.GetOutput().GetLastOutput();

        Assert.Contains(said, frame, StringComparison.Ordinal);
        Assert.Contains("\u001b_G", frame, StringComparison.Ordinal);
    }

    /// <summary>
    ///     An overlay that follows the content panel keeps its picture inside the narrowed panel too: the description
    ///     editor, opened on a picture dropped onto a fresh post.
    /// </summary>
    [Fact]
    public async Task TheDescriptionEditorsSixelFallsInsideTheNarrowedContentPanel()
    {
        using var files = new TemporaryDirectory();
        var cat = files.WriteFile("cat.png");

        using var drawn = await DrawnShell.Of(
            200,
            30,
            Themes.Plain,
            pictures: new FakePictures().HoldingPhotograph(Drawn.OnDisk(cat).Id, 600, 400),
            drawsPictures: true);

        drawn.Press(new Key('-'));
        drawn.Shell.Press(ShellKey.C);
        drawn.Shell.Paste(cat);
        drawn.Settle();
        drawn.Press(Key.CursorUp);
        drawn.Press(Key.Enter);

        Assert.IsType<DescriptionScreen>(drawn.Shell.Screen);

        var cells = BoxCells(drawn);

        Assert.NotEmpty(cells);
        Assert.All(cells, cell => Assert.InRange(cell.X, FirstInside, LastInside));
    }

    /// <summary>
    ///     Narrowing the shell narrows the box, not the cell: a picture is sent in the cell as measured — the whole
    ///     terminal's pixels over its whole cells — whatever share of the terminal the shell takes.
    /// </summary>
    [Fact]
    public async Task ANarrowedShellSendsAPictureInTheCellAsMeasured()
    {
        var terminal = new FakeTerminalImages();

        using var drawn = await Narrowed(
            kittyImages: terminal,
            drawsPlaceholders: true,
            raster: new Raster(PictureWay.Placeholders, new CellSize(16, 34), 0));

        var sent = terminal.Transmitted[^1];

        Assert.Equal(157, sent.Columns);
        Assert.Equal((157 * 16, sent.Rows * 34), PngSize(sent.Png));
    }

    /// <summary>A PNG's width and height in pixels, off its header.</summary>
    private static (int Width, int Height) PngSize(byte[] png) =>
        (System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(16)),
            System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(20)));

    /// <summary>The first and last column <paramref name="cells" /> take.</summary>
    private static (int First, int Last) Across(IEnumerable<PlaceholderCell> cells) =>
        Across([.. cells.Select(cell => new Point(cell.Column, cell.Row))]);

    /// <summary>The first and last column <paramref name="cells" /> take.</summary>
    private static (int First, int Last) Across(List<Point> cells) =>
        (cells.Min(cell => cell.X), cells.Max(cell => cell.X));

    /// <summary>The shell on a 200-column terminal holding a wide picture, narrowed a step to 180 columns from the eleventh.</summary>
    private static async Task<DrawnShell> Narrowed(
        bool drawsPictures = false,
        FakeTerminalImages? kittyImages = null,
        bool drawsPlaceholders = false,
        bool answersKitty = false,
        Raster? raster = null)
    {
        var drawn = await AtFullWidth(drawsPictures, kittyImages, drawsPlaceholders, answersKitty, raster);

        drawn.Press(new Key('-'));

        Assert.Equal('╭', drawn.Rows()[0][10]);
        Assert.Equal('╮', drawn.Rows()[0][189]);

        return drawn;
    }

    /// <summary>The shell on a 200-column terminal holding a wide picture, at the whole of its width.</summary>
    private static Task<DrawnShell> AtFullWidth(
        bool drawsPictures = false,
        FakeTerminalImages? kittyImages = null,
        bool drawsPlaceholders = false,
        bool answersKitty = false,
        Raster? raster = null) => DrawnShell.Of(
        200,
        30,
        Themes.Plain,
        AShellWithAPicture(),
        // Wider than any content panel, so that the panel's width is what bounds its box.
        pictures: new FakePictures().Holding("m1", 4000, 400),
        drawsPictures: drawsPictures,
        kittyImages: kittyImages,
        drawsPlaceholders: drawsPlaceholders,
        answersKitty: answersKitty,
        raster: raster);

    private static AShell AShellWithAPicture() => new()
    {
        Timelines = FakeTimelineReader.Holding(
            APost.With(id: "110", media: [APost.APicture("m1", shape: new PictureShape(4000, 400))]),
            APost.With(id: "220"),
            APost.With(id: "330")),
    };

    /// <summary>Every placeholder cell on screen, with where it is and the image id its colour carries.</summary>
    private static List<PlaceholderCell> PlaceholderCells(DrawnShell drawn)
    {
        var contents = drawn.Application.Driver!.Contents!;
        var cells = new List<PlaceholderCell>();

        for (var row = 0; row < contents.GetLength(0); row++)
        {
            for (var column = 0; column < contents.GetLength(1); column++)
            {
                var grapheme = contents[row, column].Grapheme;

                if (grapheme.StartsWith(Placeholder, StringComparison.Ordinal))
                {
                    var colour = drawn.Cell(row, column).Foreground;

                    cells.Add(new PlaceholderCell(row, column, grapheme, (colour.R << 16) | (colour.G << 8) | colour.B));
                }
            }
        }

        return cells;
    }

    private sealed record PlaceholderCell(int Row, int Column, string Grapheme, int Id);

    /// <summary>Every cell a picture was drawn in through a box: where it was placed, cut to where the terminal was let draw it.</summary>
    private static List<Point> BoxCells(DrawnShell drawn)
    {
        var cells = new List<Point>();

        foreach (var image in drawn.Application.Driver!.GetOutputBuffer().GetRasterImages())
        {
            var clip = image.Clip?.GetRectangles() ?? [image.DestinationCells];

            foreach (var visible in clip.Select(rectangle => Rectangle.Intersect(rectangle, image.DestinationCells)))
            {
                for (var y = visible.Top; y < visible.Bottom; y++)
                {
                    for (var x = visible.Left; x < visible.Right; x++)
                    {
                        cells.Add(new Point(x, y));
                    }
                }
            }
        }

        return cells;
    }
}
