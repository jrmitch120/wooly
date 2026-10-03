using Terminal.Gui.Drawing;
using Wooly.Tui.Media;

namespace Wooly.Tests.Tui;

/// <summary>
///     Which way a picture is drawn, given what the terminal said it can do. #2's testing decisions name exactly this
///     — "the image-rendering fallback chain's selection logic (which mode gets chosen for a given terminal
///     capability — not the actual pixel rendering)" — and it is the one part of drawing that is assertable with no
///     terminal in the room.
/// </summary>
public class RasterProtocolTests
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
    public void Chosen_PrefersKittyThenSixelThenNothing(bool sixel, bool kitty, PictureWay expected) =>
        Assert.Equal(
            expected,
            RasterProtocol.Chosen(
                new SixelSupportResult { IsSupported = sixel },
                new KittyGraphicsSupportResult { IsSupported = kitty }));

    /// <summary>
    ///     A capability nobody has asked the terminal about yet is not one it has denied — but it is not one to draw
    ///     through either, so it falls to the rung below exactly as a denial would.
    /// </summary>
    [Fact]
    public void Chosen_TreatsACapabilityNotYetReportedAsOneNotThere()
    {
        Assert.Equal(PictureWay.None, RasterProtocol.Chosen(null, null));
        Assert.Equal(PictureWay.Kitty, RasterProtocol.Chosen(null, new KittyGraphicsSupportResult { IsSupported = true }));
        Assert.Equal(PictureWay.Sixel, RasterProtocol.Chosen(new SixelSupportResult { IsSupported = true }, null));
    }

    /// <summary>
    ///     A terminal known by name to draw Kitty's placeholders draws them, at once and whatever it has answered —
    ///     including nothing yet, which in Ghostty was seconds of no pictures at all (#292, ADR-0022).
    /// </summary>
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void Chosen_DrawsPlaceholdersWhereTheTerminalIsKnownToDrawThem(bool sixel, bool kitty) =>
        Assert.Equal(
            PictureWay.Placeholders,
            RasterProtocol.Chosen(
                new SixelSupportResult { IsSupported = sixel },
                new KittyGraphicsSupportResult { IsSupported = kitty },
                placeholders: true));

    /// <summary>With placeholders, the cell the kernel measured, where it measured one.</summary>
    [Fact]
    public void Cell_WithPlaceholdersIsWhatWasMeasured() =>
        Assert.Equal(
            new CellSize(16, 34),
            RasterProtocol.Cell(null, KittyAt(10, 20), placeholders: true, measured: () => new CellSize(16, 34)));

    /// <summary>Then what the protocol reported, then the 10×20 both detectors fall back to.</summary>
    [Fact]
    public void Cell_WithPlaceholdersFallsBackToWhatWasReportedAndThenTenByTwenty()
    {
        Assert.Equal(
            new CellSize(9, 18),
            RasterProtocol.Cell(null, KittyAt(9, 18), placeholders: true, measured: () => null));
        Assert.Equal(
            new CellSize(8, 16),
            RasterProtocol.Cell(SixelAt(8, 16), null, placeholders: true, measured: () => null));
        Assert.Equal(new CellSize(10, 20), RasterProtocol.Cell(null, null, placeholders: true, measured: () => null));
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
            RasterProtocol.Cell(SixelAt(9, 18), null, placeholders: false, measured: () => new CellSize(16, 34)));
        Assert.Equal(
            new CellSize(9, 18),
            RasterProtocol.Cell(null, KittyAt(9, 18), placeholders: false, measured: () => new CellSize(16, 34)));
    }

    [Fact]
    public void Cell_IsNothingWhereNoPictureIsDrawn() =>
        Assert.Null(RasterProtocol.Cell(null, null, placeholders: false, measured: () => new CellSize(16, 34)));

    private static KittyGraphicsSupportResult KittyAt(int width, int height) =>
        new() { IsSupported = true, Resolution = new System.Drawing.Size(width, height) };

    private static SixelSupportResult SixelAt(int width, int height) =>
        new() { IsSupported = true, Resolution = new System.Drawing.Size(width, height) };
}
