using Wooly.Core.Posts;

namespace Wooly.Tui.Media;

/// <summary>
///     Where a drawn attachment's pixels come from. A port for the same reason every other one here is (ADR-0005): a
///     screen that fetched its own images could not be run without a network, and the decision this ticket is really
///     about — which attachments get drawn, and how much room they take — is settled before any of them arrive.
/// </summary>
public interface IPictures
{
    /// <summary>
    ///     The picture for <paramref name="media" /> if it is here, and <see langword="null" /> while it is not.
    /// </summary>
    /// <remarks>
    ///     Asking is what sends for it. That makes this safe to call on every draw — a picture is fetched once, and a
    ///     redraw is how it appears once it lands — and it means nothing is fetched for a post nobody has scrolled to.
    ///     A picture that cannot be had at all answers <see langword="null" /> for good rather than being asked for
    ///     again on the next frame.
    /// </remarks>
    Picture? Of(PostMedia media);
}
