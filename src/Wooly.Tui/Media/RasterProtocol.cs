using System.Drawing;
using Terminal.Gui.Drawing;
using Terminal.Gui.Drivers;

namespace Wooly.Tui.Media;

/// <summary>
///     Which way of putting pixels on a terminal this client uses: Kitty's Unicode placeholders where the terminal is
///     known to draw them, then the Kitty graphics protocol through a box, then sixel, and then nothing at all
///     (ADR-0016, ADR-0022, ADR-0023).
/// </summary>
/// <remarks>
///     Placeholders first because a picture is sent once and moves with the text for nothing; sixel cannot move an
///     image already on screen, so every scroll resends it. But only where the terminal is known by name to draw them
///     (<see cref="KnownTerminal" />): speaking Kitty graphics is not enough, because WezTerm speaks them and prints the
///     placeholders as boxes.
///     <para>
///         Everywhere else a picture is drawn through a box, Kitty before sixel, which is also the order
///         Terminal.Gui's <c>ImageView</c> tries them in. Terminal.Gui says a terminal speaks Kitty only where its
///         environment names kitty or Ghostty, so in practice a box is sixel. Story 49 asked for sixel first, and ADR-0016 kept it so;
///         measuring a scroll reversed it. Kitty sends a picture once and moves it, where sixel sends every picture on
///         the page again on every step, and draws it in 256 colours at most (ADR-0023).
///     </para>
/// </remarks>
internal static class RasterProtocol
{
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
        : kitty?.IsSupported == true ? PictureWay.Kitty
        : sixel?.IsSupported == true ? PictureWay.Sixel
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
}
