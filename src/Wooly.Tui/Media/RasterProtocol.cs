using System.Drawing;
using Terminal.Gui.Drawing;
using Terminal.Gui.Drivers;

namespace Wooly.Tui.Media;

/// <summary>
///     Which way of putting pixels on a terminal this client uses: the Kitty graphics protocol, then sixel, and then
///     nothing at all (ADR-0016, ADR-0022).
/// </summary>
/// <remarks>
///     Kitty first because a Kitty terminal is sent a picture once and draws it as placeholder cells, which move with
///     the text for nothing; sixel cannot move an image already on screen, so every scroll resends it. That is also
///     Terminal.Gui's own order, so <c>ImageView</c>, which still draws the sixel rung, needs no preference set on it.
/// </remarks>
internal static class RasterProtocol
{
    /// <summary>
    ///     How a picture is drawn on a terminal reporting <paramref name="sixel" /> and <paramref name="kitty" />.
    ///     Either may be <see langword="null" />, which is a capability nobody has asked the terminal about yet rather
    ///     than one it has denied.
    /// </summary>
    public static PictureWay Chosen(SixelSupportResult? sixel, KittyGraphicsSupportResult? kitty) =>
        kitty?.IsSupported == true ? PictureWay.Kitty
        : sixel?.IsSupported == true ? PictureWay.Sixel
        : PictureWay.None;

    /// <summary>How a picture is drawn on <paramref name="driver" />'s terminal, as far as it has said so far.</summary>
    public static PictureWay WayOf(IDriver? driver) => Chosen(driver?.SixelSupport, driver?.KittyGraphicsSupport);

    /// <summary>Whether <paramref name="driver" />'s terminal draws pictures as Kitty placeholders (ADR-0022).</summary>
    public static bool DrawsKitty(IDriver? driver) => WayOf(driver) is PictureWay.Kitty;

    /// <summary>
    ///     How big a cell is on <paramref name="driver" />'s terminal, or <see langword="null" /> where it draws no
    ///     pictures at all — which is what makes every attachment on such a terminal a link and a description.
    /// </summary>
    /// <remarks>
    ///     The resolution comes from whichever protocol is being drawn through, because it is that protocol's answer:
    ///     both detectors ask the terminal how many pixels its window is and how many cells, and divide.
    /// </remarks>
    public static CellSize? CellOf(IDriver? driver) =>
        WayOf(driver) switch
        {
            PictureWay.Sixel => Sized(driver!.SixelSupport!.Resolution),
            PictureWay.Kitty => Sized(driver!.KittyGraphicsSupport!.Resolution),
            _ => null,
        };

    /// <summary>
    ///     A reported resolution, or the 10×20 both detectors fall back to where the terminal answered that it speaks
    ///     the protocol but not how big its cells are.
    /// </summary>
    private static CellSize Sized(Size resolution) => new(
        resolution.Width > 0 ? resolution.Width : 10,
        resolution.Height > 0 ? resolution.Height : 20);
}
