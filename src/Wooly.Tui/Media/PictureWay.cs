namespace Wooly.Tui.Media;

/// <summary>
///     The ways pixels can reach a terminal, in the order this client tries them. Which one a given terminal gets is
///     <see cref="RasterProtocol.Chosen" />, and it is the one part of drawing a picture that is worth a test with no
///     terminal in the room (#2's testing decisions).
/// </summary>
public enum PictureWay
{
    /// <summary>
    ///     Kitty's Unicode placeholders: a picture sent once and drawn as text, so it moves with the rows for nothing.
    ///     Only on a terminal known by name to draw them (ADR-0022).
    /// </summary>
    Placeholders,

    /// <summary>
    ///     The Kitty graphics protocol through a box, where Terminal.Gui says the terminal speaks it — which it reads from
    ///     the environment alone, so in practice only kitty or Ghostty not known to draw placeholders.
    /// </summary>
    Kitty,

    /// <summary>Sixel, through a box, for a terminal that has that and no Kitty graphics.</summary>
    Sixel,

    /// <summary>
    ///     Neither, so nothing is drawn and the attachment is linked the way the CLI links it.
    ///     <para>
    ///         There was another rung here — a coloured cell per pixel, which needs nothing of the terminal — and it is
    ///         gone on the evidence of what it produced: a photograph at one block per cell is a few dozen rectangles
    ///         that resemble nothing, and is worse than the description it replaced (ADR-0016).
    ///     </para>
    /// </summary>
    None,
}
