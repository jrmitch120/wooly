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
    ///     The ladder, rung by rung: Kitty where it is there, sixel where Kitty is not, and nothing everywhere else.
    ///     Story 49 asked for sixel first; Kitty placeholders scroll with the text for nothing and sixel resends every
    ///     picture on every step, so a terminal offering both — WezTerm — gets Kitty (ADR-0022).
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
}
