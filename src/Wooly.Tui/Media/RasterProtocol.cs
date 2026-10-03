using System.Drawing;
using Terminal.Gui.Drawing;
using Terminal.Gui.Drivers;

namespace Wooly.Tui.Media;

/// <summary>
///     Which way of putting pixels on a terminal this client uses: Kitty's Unicode placeholders where the terminal is
///     known to draw them, then sixel, then the Kitty graphics protocol through a box, and then nothing at all
///     (ADR-0016, ADR-0022).
/// </summary>
/// <remarks>
///     Placeholders first because a picture is sent once and moves with the text for nothing; sixel cannot move an
///     image already on screen, so every scroll resends it. But only where the terminal is known by name to draw them
///     (<see cref="KnownTerminal" />): answering Terminal.Gui's Kitty query is not enough, because WezTerm answers it
///     and prints the placeholders as boxes.
///     <para>
///         Everywhere else a picture is drawn through Terminal.Gui's <c>ImageView</c>, which tries Kitty first and
///         sixel second — the other way round from story 49, and from what is cheaper to scroll. Rather than
///         reimplement the ladder to swap two rungs of it, <see cref="PreferSixel" /> sets Kitty support aside on a
///         terminal reporting both, so that sixel is what is left.
///     </para>
/// </remarks>
internal static class RasterProtocol
{
    /// <summary>
    ///     Keeps <paramref name="driver" /> on sixel where it reports sixel and Kitty both, now and for as long as it
    ///     runs — for a terminal drawing through a box rather than in placeholders.
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
    /// <param name="sixel">What the terminal answered about sixel.</param>
    /// <param name="kitty">What the terminal answered about Kitty graphics.</param>
    /// <param name="placeholders">
    ///     Whether the terminal is known by name to draw Kitty's placeholders (<see cref="KnownTerminal" />), which
    ///     settles it without waiting for either answer.
    /// </param>
    public static PictureWay Chosen(
        SixelSupportResult? sixel,
        KittyGraphicsSupportResult? kitty,
        bool placeholders = false) =>
        placeholders ? PictureWay.Placeholders
        : sixel?.IsSupported == true ? PictureWay.Sixel
        : kitty?.IsSupported == true ? PictureWay.Kitty
        : PictureWay.None;

    /// <summary>
    ///     How big a cell is on <paramref name="driver" />'s terminal, or <see langword="null" /> where it draws no
    ///     pictures at all — which is what makes every attachment on such a terminal a link and a description.
    /// </summary>
    /// <param name="driver">The terminal.</param>
    /// <param name="placeholders">Whether the terminal is known by name to draw Kitty's placeholders.</param>
    /// <param name="measured">The cell as the kernel measured it, where it could (<see cref="WindowSize" />).</param>
    public static CellSize? CellOf(IDriver? driver, bool placeholders, Func<CellSize?> measured) =>
        Cell(driver?.SixelSupport, driver?.KittyGraphicsSupport, placeholders, measured);

    /// <summary>
    ///     How big a cell is, given what the terminal answered, or <see langword="null" /> where it draws no pictures.
    /// </summary>
    /// <remarks>
    ///     In placeholders, the cell the kernel measured, then whatever either protocol answered, then 10×20: the PNG a
    ///     picture is sent as is the box's cells in pixels, so a cell guessed too small is a picture the terminal
    ///     stretches into its box (#292). Through a box, the protocol's own answer, because Terminal.Gui's image view
    ///     sizes what it draws by that — and a box shaped by one cell size and filled by another is a picture of the
    ///     wrong shape.
    /// </remarks>
    public static CellSize? Cell(
        SixelSupportResult? sixel,
        KittyGraphicsSupportResult? kitty,
        bool placeholders,
        Func<CellSize?> measured) =>
        Chosen(sixel, kitty, placeholders) switch
        {
            PictureWay.Placeholders => measured() ?? Sized(
                kitty?.IsSupported == true ? kitty.Resolution
                : sixel?.IsSupported == true ? sixel.Resolution
                : Size.Empty),
            PictureWay.Sixel => Sized(sixel!.Resolution),
            PictureWay.Kitty => Sized(kitty!.Resolution),
            _ => null,
        };

    /// <summary>
    ///     A reported resolution, or the 10×20 both detectors fall back to where the terminal answered that it speaks
    ///     the protocol but not how big its cells are — and where nothing has been answered at all.
    /// </summary>
    private static CellSize Sized(Size resolution) => new(
        resolution.Width > 0 ? resolution.Width : 10,
        resolution.Height > 0 ? resolution.Height : 20);

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
