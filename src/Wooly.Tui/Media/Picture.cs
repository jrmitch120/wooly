using Terminal.Gui.Drawing;

namespace Wooly.Tui.Media;

/// <summary>
///     One attachment's preview, decoded and ready to draw: the pixels, and how they are arranged. Whether those
///     pixels reach the terminal as sixels, as a Kitty transmission or as coloured cells is not this record's business
///     — it is the same picture either way, and the ladder is settled once, at the view (ADR-0016).
/// </summary>
/// <param name="Pixels">
///     The pixels, indexed <c>[x, y]</c> — width first, then height, which is the order Terminal.Gui's encoders read.
/// </param>
public sealed record Picture(Color[,] Pixels)
{
    /// <summary>How many pixels across.</summary>
    public int Width => Pixels.GetLength(0);

    /// <summary>How many pixels down.</summary>
    public int Height => Pixels.GetLength(1);
}
