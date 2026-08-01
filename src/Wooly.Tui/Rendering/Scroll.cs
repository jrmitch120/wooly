using Wooly.Tui.Theme;

namespace Wooly.Tui.Rendering;

/// <summary>
///     Which row a scrolling region draws first, so that what the reader picked out is on screen. Follows the selection
///     rather than keeping a scroll position of its own: what somebody picked out is the thing that has to be visible,
///     and everything that moves it — <c>j</c>, <c>k</c>, a post arriving — moves it deliberately.
/// </summary>
public static class Scroll
{
    /// <summary>
    ///     Where to scroll to, given the rows, the room, and where the scroll is now.
    /// </summary>
    /// <remarks>
    ///     Settling is the property that matters and the one that is easy to lose: asked twice over the same rows, this
    ///     has to answer the same thing twice. It is asked afresh on every frame and its own answer is its next input,
    ///     so one that disagreed with itself would not merely be wrong — it would flip between two positions for as
    ///     long as anything was redrawing, which on a post carrying more pictures than fit on a screen is what it did.
    /// </remarks>
    /// <param name="lines">The rows to be drawn.</param>
    /// <param name="height">How many rows there is room for.</param>
    /// <param name="from">Where the scroll is now.</param>
    public static int To(IReadOnlyList<Line> lines, int height, int from)
    {
        if (height < 1)
        {
            return 0;
        }

        var first = -1;
        var last = -1;

        for (var at = 0; at < lines.Count; at++)
        {
            if (!lines[at].Has(Role.Selection))
            {
                continue;
            }

            if (first < 0)
            {
                first = at;
            }

            last = at;
        }

        // Nothing picked out — a screen with no posts on it, or one that is all prose. Stay where we are.
        if (first < 0)
        {
            return Math.Clamp(from, 0, Math.Max(0, lines.Count - height));
        }

        // A post taller than the terminal is shown from its top rather than its bottom: the byline is what says whose
        // post you are looking at, and losing that is worse than losing the last of the text. Said outright rather than
        // left to fall out of the arithmetic below, which cannot honour it and settle at the same time — keeping the
        // end of a too-tall post on screen means scrolling down, and keeping its start on screen means scrolling back.
        if (last - first + 1 >= height)
        {
            return Math.Clamp(first, 0, Math.Max(0, lines.Count - 1));
        }

        var top = last - height + 1 > from ? last - height + 1 : Math.Min(from, first);

        return Math.Clamp(top, 0, Math.Max(0, lines.Count - 1));
    }
}
