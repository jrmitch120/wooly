using System.Drawing;
using Terminal.Gui.Drawing;
using Terminal.Gui.Drivers;
using Terminal.Gui.ViewBase;
using Wooly.Tui.Media;

namespace Wooly.Tests.Tui;

/// <summary>
///     Which cells of the terminal show a picture, frame by frame, worked out from what the driver hands the terminal
///     and by the rules each way of drawing a picture keeps once it is there (#577) — so that a test can say a picture is
///     hidden where something covers it and back whole once that goes, on the terminal rather than in the views.
/// </summary>
/// <remarks>
///     The headless terminal writes no cursor positions, so what reaches the terminal is read off the driver's buffer
///     either side of its write instead: the cells it is about to write and the pictures it is about to send, just
///     before it writes them (<see cref="View.DrawComplete" /> on the window), and the pictures it kept, just after
///     (<c>LayoutAndDrawComplete</c>). Each way of drawing keeps what it was sent by its own rule:
///     <list type="bullet">
///         <item>
///             Kitty's placeholders are text, so a cell shows the picture exactly while it holds a placeholder.
///         </item>
///         <item>
///             A sixel is pixels laid into cells, which stay until text is written over them: a cell shows the last
///             sixel sent over it until the driver writes anything else there.
///         </item>
///         <item>
///             A Kitty placement sits on the cells it was put over, whatever is written there, until it is put
///             somewhere else or deleted — the driver puts a picture again wherever it is sent, and deletes one it no
///             longer holds.
///         </item>
///     </list>
/// </remarks>
internal sealed class ShownPictures : IDisposable
{
    /// <summary>The character every Kitty placeholder cell starts with, its diacritics after it.</summary>
    private const string Placeholder = "\U0010EEEE";

    private readonly DrawnShell _drawn;
    private readonly PictureWay _way;

    /// <summary>Through a box, which picture each cell shows, by the id the driver keeps it under.</summary>
    private readonly Dictionary<Point, string> _boxed = [];

    /// <summary>Through Kitty, the cells each picture was last put over, by the id the driver keeps it under.</summary>
    private readonly Dictionary<string, Rectangle[]> _placed = [];

    /// <summary>What the frame being written sends, read just before it is written.</summary>
    private Writes? _writing;

    private ShownPictures(DrawnShell drawn, PictureWay way)
    {
        _drawn = drawn;
        _way = way;

        drawn.Window.DrawComplete += Writing;
        drawn.Application.LayoutAndDrawComplete += Written;
    }

    /// <summary>
    ///     Starts reading what <paramref name="drawn" />, a terminal drawing pictures <paramref name="way" />, shows of
    ///     them — the whole screen drawn again first, so that nothing the terminal was sent before goes unseen.
    /// </summary>
    public static ShownPictures Over(DrawnShell drawn, PictureWay way)
    {
        var shown = new ShownPictures(drawn, way);

        drawn.Redraw();

        return shown;
    }

    /// <summary>Every cell showing a picture, as of the last frame written.</summary>
    public HashSet<Point> Cells()
    {
        switch (_way)
        {
            case PictureWay.Placeholders:
                var cells = _drawn.Application.Driver!.Contents!;
                var holding = new HashSet<Point>();

                for (var row = 0; row < cells.GetLength(0); row++)
                {
                    for (var column = 0; column < cells.GetLength(1); column++)
                    {
                        if (cells[row, column].Grapheme?.StartsWith(Placeholder, StringComparison.Ordinal) == true)
                        {
                            holding.Add(new Point(column, row));
                        }
                    }
                }

                return holding;

            case PictureWay.Kitty:
                return [.. _placed.Values.SelectMany(rectangles => rectangles).SelectMany(Within)];

            default:
                return [.. _boxed.Keys];
        }
    }

    public void Dispose()
    {
        _drawn.Window.DrawComplete -= Writing;
        _drawn.Application.LayoutAndDrawComplete -= Written;
    }

    /// <summary>
    ///     The frame drawn and about to be written: the cells the driver will write, which are every dirty cell but the
    ///     blank ones a picture it holds owns, and the pictures it will send, which are those not yet sent as they are.
    /// </summary>
    private void Writing(object? sender, DrawEventArgs e)
    {
        var buffer = _drawn.Application.Driver!.GetOutputBuffer();
        var images = buffer.GetRasterImages();
        var held = images.SelectMany(Visible).ToArray();
        var cells = buffer.Contents!;
        var written = new HashSet<Point>();

        for (var row = 0; row < buffer.Rows; row++)
        {
            if (!buffer.DirtyLines[row])
            {
                continue;
            }

            for (var column = 0; column < buffer.Cols; column++)
            {
                if (cells[row, column].IsDirty && !OwnedByAPicture(cells[row, column], new Point(column, row), held))
                {
                    written.Add(new Point(column, row));
                }
            }
        }

        _writing = new Writes(
            written,
            [.. images.Where(image => image.IsDirty || image.AlwaysRender).Select(Image.Of)],
            [.. images.Select(Image.Of)]);
    }

    /// <summary>The frame written: what it sent laid onto what the terminal showed, by each way's rule.</summary>
    private void Written(object? sender, EventArgs e)
    {
        if (_writing is not var (written, sent, before))
        {
            return;
        }

        _writing = null;

        var buffer = _drawn.Application.Driver!.GetOutputBuffer();
        var images = buffer.GetRasterImages();
        var kept = images.Select(image => image.Id).ToHashSet();
        var held = images.SelectMany(Visible).ToArray();
        var cells = buffer.Contents!;

        // A picture the driver let go of after the frame was drawn has its cells marked to be written, and they are,
        // all but those another picture it kept owns.
        foreach (var (_, gone) in before.Where(image => !kept.Contains(image.Id)))
        {
            foreach (var cell in gone.SelectMany(Within))
            {
                if (!OwnedByAPicture(cells[cell.Y, cell.X], cell, held))
                {
                    written.Add(cell);
                }
            }
        }

        if (_way is PictureWay.Kitty)
        {
            foreach (var id in _placed.Keys.Where(id => !kept.Contains(id)).ToList())
            {
                _placed.Remove(id);
            }

            foreach (var (id, at) in sent.Where(image => kept.Contains(image.Id)))
            {
                _placed[id] = at;
            }

            return;
        }

        // Pictures go before text, so text written this frame lands over them.
        foreach (var (id, at) in sent.Where(image => kept.Contains(image.Id)))
        {
            foreach (var cell in at.SelectMany(Within))
            {
                _boxed[cell] = id;
            }
        }

        foreach (var cell in written)
        {
            _boxed.Remove(cell);
        }
    }

    /// <summary>
    ///     Where the driver lets <paramref name="image" /> draw: its cells inside the clip it was handed under.
    /// </summary>
    private static Rectangle[] Visible(RasterImageCommand image) =>
        image.Clip is not { } clip
            ? [image.DestinationCells]
            :
            [
                .. clip.GetRectangles()
                       .Select(part => Rectangle.Intersect(part, image.DestinationCells))
                       .Where(part => part is { Width: > 0, Height: > 0 }),
            ];

    /// <summary>
    ///     Whether the driver leaves <paramref name="cell" /> to a picture rather than writing it: blank, with no
    ///     background, inside a picture it holds.
    /// </summary>
    private static bool OwnedByAPicture(Cell cell, Point at, Rectangle[] held) =>
        (string.IsNullOrEmpty(cell.Grapheme) || cell.Grapheme == " ")
        && (cell.Attribute is not { } attribute || attribute.Background.A == 0)
        && held.Any(rectangle => rectangle.Contains(at));

    private static IEnumerable<Point> Within(Rectangle rectangle) =>
        from row in Enumerable.Range(rectangle.Top, rectangle.Height)
        from column in Enumerable.Range(rectangle.Left, rectangle.Width)
        select new Point(column, row);

    /// <summary>
    ///     What a frame writes, read just before it is written: the cells it writes text in, the pictures it sends, and
    ///     every picture the driver held as it was drawn.
    /// </summary>
    private sealed record Writes(HashSet<Point> Written, List<Image> Sent, List<Image> Held);

    /// <summary>A picture the driver holds, by the id it keeps it under, and the cells it lets it draw in.</summary>
    private sealed record Image(string Id, Rectangle[] Cells)
    {
        public static Image Of(RasterImageCommand image) => new(image.Id!, Visible(image));
    }
}
