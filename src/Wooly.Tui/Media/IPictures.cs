namespace Wooly.Tui.Media;

/// <summary>
///     What the TUI draws pictures with: whether this terminal can draw one at all, and the pixels for an attachment
///     once they are here. A port for the same reason every other one is (ADR-0005) — a screen that fetched its own
///     images could not be laid out without a network.
/// </summary>
/// <remarks>
///     Both questions are asked while the rows are being worked out, not while they are being painted, because the
///     answers change what the rows <em>are</em>: a terminal that cannot draw shows an attachment the way the CLI does,
///     as a link and its description, and a picture's own proportions are what settle how many rows its box takes
///     (ADR-0016).
/// </remarks>
public interface IPictures
{
    /// <summary>
    ///     How big one cell is, or <see langword="null" /> where this terminal draws no pictures at all — it offers
    ///     neither sixel nor the Kitty graphics protocol, or has not said yet.
    /// </summary>
    CellSize? Cell { get; }

    /// <summary>
    ///     The picture for <paramref name="drawn" /> if it is here, and <see langword="null" /> while it is not.
    /// </summary>
    /// <remarks>
    ///     A lookup and nothing else — asking does not send for anything. The rows of every post on a screen are worked
    ///     out whether or not that post is anywhere near the viewport, so a lookup that also fetched would fetch an
    ///     account's whole gallery to draw the four of it a reader can see (ADR-0016).
    /// </remarks>
    Picture? Of(Drawn drawn);

    /// <summary>
    ///     Says that <paramref name="drawn" /> is on screen, or nearly, and its picture is worth having.
    /// </summary>
    /// <remarks>
    ///     Safe to call on every frame: a picture is sent for once, a redraw is how the picture appears when it
    ///     lands, and one that cannot be had is not asked for again. Said by whatever knows where the scroll has got
    ///     to, which is the view rather than the post.
    /// </remarks>
    void Want(Drawn drawn);
}
