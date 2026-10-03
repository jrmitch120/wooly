namespace Wooly.Tui.Media;

/// <summary>
///     A Kitty terminal's own store of images: what is sent to it once, and what it is told to let go of. A port so that
///     whether a scroll sends a picture, and whether a dropped one is forgotten, can be asserted without reading escape
///     sequences back (ADR-0005, ADR-0022).
/// </summary>
public interface ITerminalImages
{
    /// <summary>
    ///     Sends <paramref name="png" /> under <paramref name="id" />, with a virtual placement over
    ///     <paramref name="columns" /> by <paramref name="rows" /> cells, which is what a placeholder cell is drawn from.
    /// </summary>
    void Transmit(int id, byte[] png, int columns, int rows);

    /// <summary>Tells the terminal to let go of the image <paramref name="id" />.</summary>
    void Forget(int id);

    /// <summary>Tells the terminal to let go of every image it holds.</summary>
    void ForgetAll();
}
