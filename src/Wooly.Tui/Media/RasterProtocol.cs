using Terminal.Gui.Drawing;
using Terminal.Gui.Drivers;

namespace Wooly.Tui.Media;

/// <summary>
///     Which way of putting pixels on a terminal this client prefers. Story 49 asks for sixel first, the Kitty graphics
///     protocol where sixel is not there, and coloured cells everywhere else; Terminal.Gui's <c>ImageView</c> tries
///     Kitty first and sixel second, which is the same ladder in the other order (ADR-0016).
/// </summary>
/// <remarks>
///     Rather than reimplement the ladder to reverse two rungs of it, the preference is expressed where the ladder
///     reads it: on a terminal that reports both, Kitty support is set aside so that sixel is what is left. Nothing is
///     turned off on a terminal that has only one of them, so the fallback order is untouched — a Kitty terminal with
///     no sixel still draws through Kitty, and a terminal with neither still draws cells.
/// </remarks>
internal static class RasterProtocol
{
    /// <summary>Settles the preference on <paramref name="driver" />, once, before anything is drawn.</summary>
    public static void PreferSixel(IDriver? driver)
    {
        if (driver?.SixelSupport?.IsSupported != true || driver.KittyGraphicsSupport?.IsSupported != true)
        {
            return;
        }

        driver.SetKittyGraphicsSupport(new KittyGraphicsSupportResult { IsSupported = false });
    }
}
