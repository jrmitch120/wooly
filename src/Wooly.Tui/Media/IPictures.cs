namespace Wooly.Tui.Media;

/// <summary>
///     What the TUI draws pictures from: the pixels for an attachment once they are here, and which of them a frame
///     wants. A port for the same reason every other one is (ADR-0005) — a screen that fetched its own images could not
///     be laid out without a network. Whether this terminal can draw a picture at all, and at what cell size, is not
///     asked of it: that is the frame's <see cref="Raster" /> (#357).
/// </summary>
/// <remarks>
///     <see cref="Of" /> is asked while the rows are being worked out, not while they are being painted, because the
///     answer changes what the rows <em>are</em>: a picture that is here is fitted inside its box, and one that is not
///     leaves the box its Stand-in (ADR-0025).
/// </remarks>
public interface IPictures
{
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
}
