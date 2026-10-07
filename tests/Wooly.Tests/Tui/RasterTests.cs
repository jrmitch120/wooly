using Terminal.Gui.Drawing;
using Wooly.Tui.Media;

namespace Wooly.Tests.Tui;

/// <summary>
///     How this terminal paints pixels, given what it said it can do: the way, the cell and the sixel colours. #2's
///     testing decisions name exactly this — "the image-rendering fallback chain's selection logic (which mode gets
///     chosen for a given terminal capability — not the actual pixel rendering)" — and it is the one part of drawing
///     that is assertable with no terminal in the room.
/// </summary>
public class RasterTests
{
    /// <summary>
    ///     Where nothing is known of the terminal but its answers: Kitty through a box where it is there, sixel where
    ///     Kitty is not, and nothing everywhere else. Story 49 asked for sixel first; Kitty sends a picture once and
    ///     moves it, where sixel sends every picture on the page again on every step of a scroll (#292, ADR-0023).
    /// </summary>
    [Theory]
    [InlineData(true, true, PictureWay.Kitty)]
    [InlineData(true, false, PictureWay.Sixel)]
    [InlineData(false, true, PictureWay.Kitty)]
    [InlineData(false, false, PictureWay.None)]
    public void Way_PrefersKittyThenSixelThenNothing(bool sixel, bool kitty, PictureWay expected) =>
        Assert.Equal(
            expected,
            Raster.Of(
                    new SixelSupportResult { IsSupported = sixel },
                    new KittyGraphicsSupportResult { IsSupported = kitty },
                    placeholders: false,
                    measured: () => null)
                .Way);

    /// <summary>
    ///     A capability nobody has asked the terminal about yet is not one it has denied — but it is not one to draw
    ///     through either, so it falls to the rung below exactly as a denial would.
    /// </summary>
    [Fact]
    public void Way_TreatsACapabilityNotYetReportedAsOneNotThere()
    {
        Assert.Equal(PictureWay.None, Raster.Of(null, null, placeholders: false, measured: () => null).Way);
        Assert.Equal(
            PictureWay.Kitty,
            Raster.Of(null, new KittyGraphicsSupportResult { IsSupported = true }, false, () => null).Way);
        Assert.Equal(
            PictureWay.Sixel,
            Raster.Of(new SixelSupportResult { IsSupported = true }, null, false, () => null).Way);
    }

    /// <summary>
    ///     A terminal known by name to draw Kitty's placeholders draws them, at once and whatever it has answered —
    ///     including nothing yet, which in Ghostty was seconds of no pictures at all (#292, ADR-0022).
    /// </summary>
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void Way_IsPlaceholdersWhereTheTerminalIsKnownToDrawThem(bool sixel, bool kitty) =>
        Assert.Equal(
            PictureWay.Placeholders,
            Raster.Of(
                    new SixelSupportResult { IsSupported = sixel },
                    new KittyGraphicsSupportResult { IsSupported = kitty },
                    placeholders: true,
                    measured: () => null)
                .Way);

    /// <summary>With placeholders, the cell the kernel measured, where it measured one.</summary>
    [Fact]
    public void Cell_WithPlaceholdersIsWhatWasMeasured() =>
        Assert.Equal(
            new CellSize(16, 34),
            Raster.Of(null, KittyAt(10, 20), placeholders: true, measured: () => new CellSize(16, 34)).Cell);

    /// <summary>Then what the protocol reported, then the 10×20 both detectors fall back to.</summary>
    [Fact]
    public void Cell_WithPlaceholdersFallsBackToWhatWasReportedAndThenTenByTwenty()
    {
        Assert.Equal(
            new CellSize(9, 18),
            Raster.Of(null, KittyAt(9, 18), placeholders: true, measured: () => null).Cell);
        Assert.Equal(
            new CellSize(8, 16),
            Raster.Of(SixelAt(8, 16), null, placeholders: true, measured: () => null).Cell);
        Assert.Equal(new CellSize(10, 20), Raster.Of(null, null, placeholders: true, measured: () => null).Cell);
    }

    /// <summary>
    ///     Through a box the cell is the protocol's own answer: Terminal.Gui's image view sizes what it draws by that,
    ///     and a box shaped by another cell size would be a picture drawn at one shape into a box of another.
    /// </summary>
    [Fact]
    public void Cell_ThroughABoxIsWhatTheProtocolReported()
    {
        Assert.Equal(
            new CellSize(9, 18),
            Raster.Of(SixelAt(9, 18), null, placeholders: false, measured: () => new CellSize(16, 34)).Cell);
        Assert.Equal(
            new CellSize(9, 18),
            Raster.Of(null, KittyAt(9, 18), placeholders: false, measured: () => new CellSize(16, 34)).Cell);
    }

    /// <summary>A terminal that draws nothing has no cell, which is what links every picture on it.</summary>
    [Fact]
    public void Cell_IsNothingWhereNoPictureIsDrawn() =>
        Assert.Null(Raster.Of(null, null, placeholders: false, measured: () => new CellSize(16, 34)).Cell);

    /// <summary>
    ///     A sixel is encoded in all 256 colours sixel allows, or fewer where the terminal says it has fewer — and in
    ///     none on any other way, so that a box drawn through Kitty is said by its way rather than by a count of
    ///     nought (ADR-0023).
    /// </summary>
    [Fact]
    public void SixelColours_AreTheTerminalsUpToTwoHundredAndFiftySixAndOnlyForSixel()
    {
        Assert.Equal(256, Raster.Of(SixelWith(1024), null, placeholders: false, measured: () => null).SixelColours);
        Assert.Equal(16, Raster.Of(SixelWith(16), null, placeholders: false, measured: () => null).SixelColours);
        Assert.Equal(
            0,
            Raster.Of(SixelWith(256), KittyAt(10, 20), placeholders: false, measured: () => null).SixelColours);
        Assert.Equal(0, Raster.Of(null, null, placeholders: false, measured: () => null).SixelColours);
    }

    /// <summary>
    ///     A terminal known to draw placeholders encodes no sixel even where it also answers sixel: the placeholders
    ///     are taken into account every time the way is chosen, not only where a cell is asked for.
    /// </summary>
    [Fact]
    public void SixelColours_AreNoneWhereThePlaceholdersAreDrawn() =>
        Assert.Equal(0, Raster.Of(SixelWith(256), null, placeholders: true, measured: () => null).SixelColours);

    /// <summary>The terminal that cannot draw, said once: no way, no cell, no colours.</summary>
    [Fact]
    public void None_DrawsNothing()
    {
        Assert.Equal(PictureWay.None, Raster.None.Way);
        Assert.Null(Raster.None.Cell);
        Assert.Equal(0, Raster.None.SixelColours);
    }

    private static KittyGraphicsSupportResult KittyAt(int width, int height) =>
        new() { IsSupported = true, Resolution = new System.Drawing.Size(width, height) };

    private static SixelSupportResult SixelAt(int width, int height) =>
        new() { IsSupported = true, Resolution = new System.Drawing.Size(width, height) };

    private static SixelSupportResult SixelWith(int colours) =>
        new() { IsSupported = true, MaxPaletteColors = colours };
}
