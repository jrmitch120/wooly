using Wooly.Core.Posts;

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
    ///     The picture for <paramref name="media" /> if it is here, and <see langword="null" /> while it is not.
    /// </summary>
    /// <remarks>
    ///     Asking is what sends for it. That makes this safe to call every time the rows are worked out — a picture is
    ///     fetched once, and a redraw is how it appears once it lands — and it means nothing is fetched for a post
    ///     nobody has scrolled to. A picture that cannot be had at all answers <see langword="null" /> for good rather
    ///     than being asked for again on the next frame.
    /// </remarks>
    Picture? Of(PostMedia media);
}
