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
    ///     The ladder story 49 asks for, rung by rung: sixel where it is there, Kitty where sixel is not, and coloured
    ///     cells everywhere else. The third rung is why the TUI needs no link-and-alt-text fallback of its own.
    /// </summary>
    [Theory]
    [InlineData(true, true, PictureWay.Sixel)]
    [InlineData(true, false, PictureWay.Sixel)]
    [InlineData(false, true, PictureWay.Kitty)]
    [InlineData(false, false, PictureWay.Cells)]
    public void Chosen_PrefersSixelThenKittyThenCells(bool sixel, bool kitty, PictureWay expected) =>
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
        Assert.Equal(PictureWay.Cells, RasterProtocol.Chosen(null, null));
        Assert.Equal(PictureWay.Kitty, RasterProtocol.Chosen(null, new KittyGraphicsSupportResult { IsSupported = true }));
        Assert.Equal(PictureWay.Sixel, RasterProtocol.Chosen(new SixelSupportResult { IsSupported = true }, null));
    }
}
