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
///     reads it: on a terminal reporting both, Kitty support is set aside so that sixel is what is left. Nothing is
///     touched on a terminal reporting only one of them, so the fallback order is unchanged — a Kitty terminal with no
///     sixel still draws through Kitty, and a terminal with neither still draws cells.
/// </remarks>
internal static class RasterProtocol
{
    /// <summary>
    ///     Keeps <paramref name="driver" /> on this client's preference, now and for as long as it runs.
    /// </summary>
    /// <remarks>
    ///     Subscribed to rather than read once, which is the whole of why this is not two lines in <c>Program</c>.
    ///     Both capabilities are found out by asking the terminal and waiting for its answer, and those answers arrive
    ///     on the input loop some frames after the application starts — so at the moment the shell is built neither has
    ///     been reported yet, and whichever lands second would otherwise overwrite a preference settled before it.
    /// </remarks>
    public static void PreferSixel(IDriver? driver)
    {
        if (driver is null)
        {
            return;
        }

        driver.SixelSupportChanged += (_, _) => Settle(driver);
        driver.KittyGraphicsSupportChanged += (_, _) => Settle(driver);

        Settle(driver);
    }

    /// <summary>
    ///     How a picture is drawn on a terminal reporting <paramref name="sixel" /> and <paramref name="kitty" />.
    ///     Either may be <see langword="null" />, which is a capability nobody has asked the terminal about yet rather
    ///     than one it has denied.
    /// </summary>
    public static PictureWay Chosen(SixelSupportResult? sixel, KittyGraphicsSupportResult? kitty) =>
        sixel?.IsSupported == true ? PictureWay.Sixel
        : kitty?.IsSupported == true ? PictureWay.Kitty
        : PictureWay.Cells;

    /// <summary>
    ///     Puts the driver where <see cref="Chosen" /> says it should be. Setting Kitty aside raises the event this is
    ///     subscribed to, which comes straight back here and finds nothing left to do — so it settles rather than loops.
    /// </summary>
    private static void Settle(IDriver driver)
    {
        if (Chosen(driver.SixelSupport, driver.KittyGraphicsSupport) is PictureWay.Sixel
            && driver.KittyGraphicsSupport?.IsSupported == true)
        {
            driver.SetKittyGraphicsSupport(new KittyGraphicsSupportResult { IsSupported = false });
        }
    }
}
