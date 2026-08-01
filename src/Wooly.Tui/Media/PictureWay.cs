namespace Wooly.Tui.Media;

/// <summary>
///     The three ways pixels reach a terminal, in the order story 49 asks for them. Which one a given terminal gets is
///     <see cref="RasterProtocol.Chosen" />, and it is the one part of drawing a picture that is worth a test with no
///     terminal in the room (#2's testing decisions).
/// </summary>
public enum PictureWay
{
    /// <summary>Sixel, where the terminal answers that it speaks it. What this client prefers.</summary>
    Sixel,

    /// <summary>The Kitty graphics protocol, for a terminal that has that and no sixel.</summary>
    Kitty,

    /// <summary>
    ///     A coloured cell per pixel, which needs nothing of the terminal at all. Not a failure but the floor: this is
    ///     what a terminal with neither protocol shows, and it is why the TUI needs no link-and-alt-text fallback of
    ///     the sort the CLI always uses (#31).
    /// </summary>
    Cells,
}
