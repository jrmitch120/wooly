using Wooly.Tui.Media;

namespace Wooly.Tests.Fakes;

/// <summary>
///     How a terminal paints pixels, said outright for a <see cref="Wooly.Tui.Rendering.Drawing" /> laid out with no
///     terminal in the room — at 10×20 unless a test says otherwise, which is what both protocols fall back to reporting.
/// </summary>
internal static class ARaster
{
    /// <summary>A terminal drawing sixel through a box, in all 256 colours.</summary>
    public static Raster Sixel(CellSize? cell = null) => new(PictureWay.Sixel, cell ?? new CellSize(10, 20), 256);

    /// <summary>A terminal drawing the Kitty graphics protocol through a box.</summary>
    public static Raster Kitty(CellSize? cell = null) => new(PictureWay.Kitty, cell ?? new CellSize(10, 20), 0);

    /// <summary>A terminal known by name to draw Kitty's placeholders.</summary>
    public static Raster Placeholders(CellSize? cell = null) =>
        new(PictureWay.Placeholders, cell ?? new CellSize(10, 20), 0);
}
