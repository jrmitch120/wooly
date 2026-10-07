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
    ///     Says which pictures this frame wants: everything on screen or near it, nearest first, with those on screen
    ///     marked. Whatever was wanted before and is not in <paramref name="frame" /> is wanted no more.
    /// </summary>
    /// <remarks>
    ///     One call a frame rather than one a picture, because only a whole frame can say which pictures are on screen
    ///     now and which used to be wanted and are not any more — and those are what a cache needs, to know what it
    ///     must never let go of and what it should let go of first (ADR-0025). Safe to call on every frame: a picture
    ///     is sent for once, a redraw is how the picture appears when it lands, and one that cannot be had is not
    ///     asked for again. Said by whatever knows where the scroll has got to, which is the view rather than the post.
    /// </remarks>
    void Want(IReadOnlyList<WantedPicture> frame);

    /// <summary>
    ///     The pictures let go of since the last drain, by <see cref="Drawn.Id" />, each handed back once and never
    ///     again — whether a frame's <see cref="Want" /> let go of them or a picture landing did.
    /// </summary>
    /// <remarks>
    ///     Handed back rather than announced, so that whatever has to hear of them — a Kitty terminal holding a copy,
    ///     which is told to let go of it too (ADR-0022) — hears on the UI thread, once a frame, and never under this
    ///     cache's lock or on a thread a picture landed on. One let go of as another lands is drained on the redraw
    ///     that landing asks for.
    /// </remarks>
    IReadOnlyList<string> Drain();
}
