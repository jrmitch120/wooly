using System.Drawing;
using Terminal.Gui.Drawing;
using Terminal.Gui.Drivers;

namespace Wooly.Tui.Media;

/// <summary>
///     How this terminal paints pixels, as one value: the way a picture is drawn, how big a cell is, and how many
///     colours a sixel is encoded in. Worked out once a frame, as the content region settles it, and carried on the
///     <see cref="Rendering.Drawing" />, so that the screens, the view and the picture cache cannot come to disagree
///     about what kind of terminal this is (#357).
/// </summary>
/// <remarks>
///     Kitty's Unicode placeholders where the terminal is known to draw them, then Kitty graphics through a box, then
///     sixel, and then nothing at all (ADR-0016, ADR-0022, ADR-0023). Placeholders first because a picture is sent
///     once and moves with the text for nothing; sixel cannot move an image already on screen, so every scroll resends
///     it. But only where the terminal is known by name to draw them (<see cref="KnownTerminal" />): speaking
///     Kitty graphics is not enough, because WezTerm speaks them and prints the placeholders as boxes.
///     <para>
///         Everywhere else a picture is drawn through a box, Kitty before sixel, which is also the order
///         Terminal.Gui's <c>ImageView</c> tries them in. Terminal.Gui says a terminal speaks Kitty only where its
///         environment names kitty or Ghostty, so in practice a box is sixel. Story 49 asked for sixel first, and
///         ADR-0016 kept it so; measuring a scroll reversed it. Kitty sends a picture once and moves it, where sixel
///         sends every picture on the page again on every step, and draws it in 256 colours at most (ADR-0023).
///     </para>
/// </remarks>
/// <param name="Way">Which way a picture is drawn.</param>
/// <param name="Cell">
///     How big one cell is, or <see langword="null" /> where this terminal draws no pictures at all — which is what
///     makes every attachment on such a terminal a link and a description.
/// </param>
/// <param name="SixelColours">
///     How many colours a sixel is encoded in, or none on every way but <see cref="PictureWay.Sixel" />.
/// </param>
public sealed record Raster(PictureWay Way, CellSize? Cell, int SixelColours)
{
    /// <summary>A terminal that draws no pictures: every one of them linked.</summary>
    public static readonly Raster None = new(PictureWay.None, null, 0);

    /// <summary>How <paramref name="driver" />'s terminal paints pixels, as it has answered so far.</summary>
    /// <param name="driver">The terminal, or <see langword="null" /> before there is one.</param>
    /// <param name="placeholders">
    ///     Whether the terminal is known by name to draw Kitty's placeholders (<see cref="KnownTerminal" />), which
    ///     settles it without waiting for either answer.
    /// </param>
    /// <param name="measured">The cell as the kernel measured it, where it could (<see cref="WindowSize" />).</param>
    public static Raster Of(IDriver? driver, bool placeholders, Func<CellSize?> measured) =>
        Of(driver?.SixelSupport, driver?.KittyGraphicsSupport, placeholders, measured);

    /// <summary>
    ///     How a terminal reporting <paramref name="sixel" /> and <paramref name="kitty" /> paints pixels. Either may
    ///     be <see langword="null" />, which is a capability nobody has asked the terminal about yet rather than one it
    ///     has denied — and not one to draw through either.
    /// </summary>
    /// <remarks>
    ///     In placeholders, the cell the kernel measured, then whatever the terminal answered about Kitty graphics or
    ///     sixel, then 10×20: the PNG a picture is sent as is the box's cells in pixels, so a cell guessed too small is
    ///     a picture the terminal stretches into its box (#292). Only asked of the kernel there. Through a box, the
    ///     terminal's own answer for the way it draws, because Terminal.Gui's image view sizes what it draws by that —
    ///     and a box shaped by one cell size and filled by another is a picture of the wrong shape.
    ///     <para>
    ///         A sixel in all 256 colours sixel allows, or fewer where the terminal says it has fewer. Terminal.Gui's
    ///         image view stops at 64, at which a photograph's gradients break into patches (ADR-0023).
    ///     </para>
    /// </remarks>
    /// <param name="sixel">What the terminal answered about sixel.</param>
    /// <param name="kitty">What the terminal answered about Kitty graphics.</param>
    /// <param name="placeholders">Whether the terminal is known by name to draw Kitty's placeholders.</param>
    /// <param name="measured">The cell as the kernel measured it, where it could.</param>
    public static Raster Of(
        SixelSupportResult? sixel,
        KittyGraphicsSupportResult? kitty,
        bool placeholders,
        Func<CellSize?> measured) =>
        Chosen(sixel, kitty, placeholders) switch
        {
            PictureWay.Placeholders => new Raster(
                PictureWay.Placeholders,
                measured() ?? Sized(
                    kitty?.IsSupported == true ? kitty.Resolution
                    : sixel?.IsSupported == true ? sixel.Resolution
                    : Size.Empty),
                0),
            PictureWay.Kitty => new Raster(PictureWay.Kitty, Sized(kitty!.Resolution), 0),
            PictureWay.Sixel => new Raster(
                PictureWay.Sixel,
                Sized(sixel!.Resolution),
                Math.Min(256, sixel.MaxPaletteColors)),
            _ => None,
        };

    /// <summary>
    ///     Which way a picture is drawn: the one place it is chosen, with the placeholders taken into account every
    ///     time.
    /// </summary>
    private static PictureWay Chosen(
        SixelSupportResult? sixel,
        KittyGraphicsSupportResult? kitty,
        bool placeholders) =>
        placeholders ? PictureWay.Placeholders
        : kitty?.IsSupported == true ? PictureWay.Kitty
        : sixel?.IsSupported == true ? PictureWay.Sixel
        : PictureWay.None;

    /// <summary>
    ///     A reported resolution, or the 10×20 both detectors fall back to where the terminal answered that it draws
    ///     that way but not how big its cells are — and where nothing has been answered at all.
    /// </summary>
    private static CellSize Sized(Size resolution) => new(
        resolution.Width > 0 ? resolution.Width : 10,
        resolution.Height > 0 ? resolution.Height : 20);
}
