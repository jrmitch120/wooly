using Wooly.Tui.Theme;

namespace Wooly.Tui.Rendering;

/// <summary>
///     Which row a scrolling region draws first. Four questions about a page of rows, all of them answered without a
///     terminal: where to scroll so that what the reader picked out is on screen, where a row of scrolling lands,
///     whether the selection is still on the page, and what is topmost on it if it is not.
/// </summary>
/// <remarks>
///     The offset is the reader's, not this class's: <c>↓</c> and <c>↑</c> put it where they like, and the answers
///     here are what a <c>j</c> or <c>k</c> press asks for rather than what every frame recomputes (#51).
/// </remarks>
public static class Scroll
{
    /// <summary>
    ///     Where to scroll to, given the rows, the room, and where the scroll is now — the answer to <c>j</c> or
    ///     <c>k</c> asking for the selection to be brought back into view.
    /// </summary>
    /// <remarks>
    ///     Settling is the property that matters and the one that is easy to lose: asked twice over the same rows, this
    ///     has to answer the same thing twice. Its own answer is its next input, and the screen goes on following the
    ///     selection for as long as the arrows leave it alone — so one that disagreed with itself would not merely be
    ///     wrong. It would flip between two positions for as long as anything was redrawing, which on a post carrying
    ///     more pictures than fit on a screen is what it did.
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

        // Nothing picked out — a screen with no posts on it, or one that is all prose. Stay exactly where the arrows
        // left us: with nothing to bring into view, the only wrong answer is moving. Clamped the way they clamp, so
        // that pressing j on a screen with no selection cannot yank a keymap read to its foot back up a page.
        if (first < 0)
        {
            return By(lines, from, 0);
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

    /// <summary>
    ///     Where <paramref name="rows" /> of scrolling from <paramref name="from" /> lands: what <c>↓</c> and <c>↑</c>
    ///     do, which is move the screen and leave the selection alone.
    /// </summary>
    /// <remarks>
    ///     Down to the last row rather than to the last page, which is the same bound <see cref="To" /> keeps to — a
    ///     post taller than the room is shown from its top, and that is an offset past the last page already.
    ///     <para>
    ///         Asked for no rows at all, this is the clamp on its own: rows are worked out afresh on every frame, and
    ///         a post deleted out from under a reader who has scrolled to the foot of a screen leaves an offset past
    ///         the end of it.
    ///     </para>
    /// </remarks>
    public static int By(IReadOnlyList<Line> lines, int from, int rows) =>
        (int)Math.Clamp((long)from + rows, 0, Math.Max(0, lines.Count - 1));

    /// <summary>
    ///     Whether the page starting at <paramref name="from" /> has any row of the selection on it — which is what
    ///     settles whether <c>j</c> moves from the selection or reclaims a new one.
    /// </summary>
    /// <remarks>
    ///     Any row of it, so a post whose middle is showing counts: what is being asked is whether the reader can see
    ///     what they picked, and part of a post is enough to have not lost it.
    /// </remarks>
    public static bool Shows(IReadOnlyList<Line> lines, int height, int from)
    {
        for (var at = Math.Max(0, from); at < Math.Min(lines.Count, from + height); at++)
        {
            if (lines[at].Has(Role.Selection))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///     Which item the page starting at <paramref name="from" /> begins on, or <see langword="null" /> for a screen
    ///     with no items on it at all. What <c>j</c> reclaims after the arrows have walked away from the selection.
    /// </summary>
    /// <remarks>
    ///     A post whose top has been scrolled off but whose lower rows are still on the page is the topmost post,
    ///     because its rows are the first ones here — decided that way rather than left to fall out, so that
    ///     <c>↓ ↓ ↓ j</c> selects the post being read rather than the one after it (#51).
    ///     <para>
    ///         Looking back up when there is nothing below is what a screen scrolled to its very foot needs: the rows
    ///         there are the blank after the last post and nothing else, and answering "no item" would put <c>j</c>
    ///         back to moving from a selection the reader cannot see — the one thing it must not do.
    ///     </para>
    /// </remarks>
    public static int? Topmost(IReadOnlyList<Line> lines, int from)
    {
        for (var at = Math.Max(0, from); at < lines.Count; at++)
        {
            if (lines[at].Item is { } item)
            {
                return item;
            }
        }

        for (var at = Math.Min(lines.Count, from) - 1; at >= 0; at--)
        {
            if (lines[at].Item is { } item)
            {
                return item;
            }
        }

        return null;
    }
}
