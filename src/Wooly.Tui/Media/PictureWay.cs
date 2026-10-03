namespace Wooly.Tui.Media;

/// <summary>
///     The ways pixels can reach a terminal, in the order this client tries them. Which one a given terminal gets is
///     <see cref="RasterProtocol.Chosen" />, and it is the one part of drawing a picture that is worth a test with no
///     terminal in the room (#2's testing decisions).
/// </summary>
public enum PictureWay
{
    /// <summary>
    ///     The Kitty graphics protocol's placeholders, where the terminal answers that it speaks it: a picture sent
    ///     once and drawn as text. What this client prefers (ADR-0022).
    /// </summary>
    Kitty,

    /// <summary>Sixel, for a terminal that has that and no Kitty graphics.</summary>
    Sixel,

    /// <summary>
    ///     Neither, so nothing is drawn and the attachment is linked the way the CLI links it.
    ///     <para>
    ///         There was a third rung here — a coloured cell per pixel, which needs nothing of the terminal — and it is
    ///         gone on the evidence of what it produced: a photograph at one block per cell is a few dozen rectangles
    ///         that resemble nothing, and is worse than the description it replaced (ADR-0016).
    ///     </para>
    /// </summary>
    None,
}
