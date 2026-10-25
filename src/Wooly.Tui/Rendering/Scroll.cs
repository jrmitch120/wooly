using Wooly.Tui.Theme;

namespace Wooly.Tui.Rendering;

/// <summary>
///     Which row a scrolling region draws first. Five questions about a page of rows, all of them answered without a
///     terminal: where to scroll so that what the reader picked out is on screen, where to scroll so that the heading
///     over it is too, where a row of scrolling lands, whether the selection is still on the page, and what is topmost
///     on it if it is not.
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
    /// <param name="pinned">
    ///     How many rows at the top of the page are pinned there, drawn over the rows scrolled under them, so that only
    ///     the rest of the page shows what has scrolled (<see cref="Screens.Screen.Pinned" />).
    /// </param>
    public static int To(IReadOnlyList<Line> lines, int height, int from, int pinned = 0) =>
        ToShow(lines, height, from, pinned, line => line.Has(Role.Selection));

    /// <summary>
    ///     Where to scroll to so that <paramref name="reference" />, the one walked to, is on the page — the answer to
    ///     <c>→</c> or <c>←</c> having walked to one off it (#473).
    /// </summary>
    /// <remarks>
    ///     The same minimum move <see cref="To" /> makes for the pick, keyed on every row drawn for the reference
    ///     (<see cref="Draws" />), so that a reference wrapped across two rows is brought on whole where
    ///     it fits. With no row drawn for it, the page stays where it is.
    /// </remarks>
    /// <param name="lines">The rows to be drawn.</param>
    /// <param name="height">How many rows there is room for.</param>
    /// <param name="from">Where the scroll is now.</param>
    /// <param name="reference">The reference walked to.</param>
    /// <param name="pinned">How many rows at the top of the page are pinned there (<see cref="To" />).</param>
    public static int ToReference(
        IReadOnlyList<Line> lines,
        int height,
        int from,
        Reference reference,
        int pinned = 0) =>
        ToShow(lines, height, from, pinned, line => Draws(line, reference));

    /// <summary>
    ///     Whether <paramref name="reference" />, the one walked to, has gone off the page starting at
    ///     <paramref name="from" />: none of the rows drawn for it are on it, which is what lets it go (#478).
    /// </summary>
    /// <remarks>
    ///     Its rows are every row drawn for it (<see cref="Draws" />), the middle row of an address
    ///     wrapped across three included: a reference is off only when no part of it shows. One drawn on no row at all
    ///     says nothing either way, so it is never let go for that.
    /// </remarks>
    /// <param name="lines">The rows drawn.</param>
    /// <param name="height">How many rows there is room for.</param>
    /// <param name="from">Where the page starts.</param>
    /// <param name="reference">The reference walked to.</param>
    /// <param name="pinned">
    ///     How many rows at the top of the page are pinned there (<see cref="To" />): a row scrolled under them is not on
    ///     the page, since nobody can see it.
    /// </param>
    public static bool ReferenceOffThePage(
        IReadOnlyList<Line> lines,
        int height,
        int from,
        Reference reference,
        int pinned = 0)
    {
        var drawn = false;

        for (var at = 0; at < lines.Count; at++)
        {
            if (!Draws(lines[at], reference))
            {
                continue;
            }

            if (at >= from + pinned && at < from + height)
            {
                return false;
            }

            drawn = true;
        }

        return drawn;
    }

    /// <summary>
    ///     Whether <paramref name="line" /> is drawn for <paramref name="reference" />, the one walked to: a row of the
    ///     pick (<see cref="Role.Selection" />) carrying it. A reference is walked inside the pick, and one that reads the
    ///     same at the same place in another post — the reader's own handle opening two replies to them — compares
    ///     equal without being it.
    /// </summary>
    private static bool Draws(Line line, Reference reference) => line.Has(Role.Selection) && line.Has(reference);

    /// <summary>
    ///     Where to scroll to so that every row that is <paramref name="part" /> of what is to be shown is on the page.
    /// </summary>
    private static int ToShow(IReadOnlyList<Line> lines, int height, int from, int pinned, Func<Line, bool> part)
    {
        if (height < 1)
        {
            return 0;
        }

        var first = -1;
        var last = -1;

        for (var at = 0; at < lines.Count; at++)
        {
            if (!part(lines[at]))
            {
                continue;
            }

            if (first < 0)
            {
                first = at;
            }

            last = at;
        }

        var room = Math.Max(1, height - pinned);

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
        // All of it in the room under the pinned rows, which is the page that shows what has scrolled, and which starts
        // that many rows further down the rows than the page does.
        if (last - first + 1 >= room)
        {
            return Math.Clamp(first - pinned, 0, Math.Max(0, lines.Count - 1));
        }

        var top = last - room + 1 - pinned > from ? last - room + 1 - pinned : Math.Min(from, first - pinned);

        return Math.Clamp(top, 0, Math.Max(0, lines.Count - 1));
    }

    /// <summary>
    ///     The same, with the heading of the selection's run brought onto the page along with it — what <c>[</c> and
    ///     <c>]</c> ask for, rather than what <c>j</c> and <c>k</c> do (#166).
    /// </summary>
    /// <remarks>
    ///     Anchored on the heading only where the heading is not already on the page, and otherwise left exactly where
    ///     it is. <see cref="To" /> moves the minimum needed to show the selection, so an unaided jump downwards lands
    ///     the picked row on the bottom row with the run just left still filling the screen, which reads as a press
    ///     that did nothing; always anchoring settles cleanly but carries the first run off the top of a search that
    ///     fits on one screen, for nothing. So the page moves only where it has to.
    ///     <para>
    ///         <see cref="To" /> has the last word either way, which is what makes this settle: fed the heading row it
    ///         answers that same row back on every redraw, and where the run is taller than the room it shows the
    ///         thing picked out instead — what the reader jumped to has to be on screen, and the anchor is only ever a
    ///         preference about where.
    ///     </para>
    /// </remarks>
    /// <param name="lines">The rows to be drawn.</param>
    /// <param name="height">How many rows there is room for.</param>
    /// <param name="from">Where the scroll is now.</param>
    /// <param name="pinned">How many rows at the top of the page are pinned there (<see cref="To" />).</param>
    public static int ToSection(IReadOnlyList<Line> lines, int height, int from, int pinned = 0)
    {
        if (height < 1)
        {
            return 0;
        }

        var heading = Heading(lines);

        return To(
            lines,
            height,
            heading is { } at && (at < from + pinned || at >= from + height) ? at - pinned : from,
            pinned);
    }

    /// <summary>
    ///     Which row heads the run the selection is in — the nearest one above it — or <see langword="null" /> where
    ///     nothing does, which is every screen that draws no headings and every thing standing above the first one.
    /// </summary>
    private static int? Heading(IReadOnlyList<Line> lines)
    {
        for (var at = 0; at < lines.Count; at++)
        {
            if (!lines[at].Has(Role.Selection))
            {
                continue;
            }

            for (var above = at; above >= 0; above--)
            {
                if (lines[above].Heads)
                {
                    return above;
                }
            }

            return null;
        }

        return null;
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
    /// <param name="lines">The rows drawn.</param>
    /// <param name="height">How many rows there is room for.</param>
    /// <param name="from">Where the page starts.</param>
    /// <param name="pinned">
    ///     How many rows at the top of the page are pinned there (<see cref="To" />): a row scrolled under them is not on
    ///     the page, since nobody can see it.
    /// </param>
    public static bool Shows(IReadOnlyList<Line> lines, int height, int from, int pinned = 0)
    {
        for (var at = Math.Max(0, from + pinned); at < Math.Min(lines.Count, from + height); at++)
        {
            if (lines[at].Has(Role.Selection))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///     Which thing <c>j</c> reclaims once the pick is off the page starting at <paramref name="from" />: the first
    ///     thing whose top is on it, or <see langword="null" /> for a screen with no items on it at all (#472).
    /// </summary>
    /// <remarks>
    ///     A thing's top is the first row stamped with it — a boost line, a notification's strip, a byline, an
    ///     account's header block — read off the rows as drawn, so no screen keeps its own record of where things
    ///     begin. The foot of a post filling the top of the page is passed over for the first thing the reader can see
    ///     beginning.
    ///     <para>
    ///         Where nothing begins on the page, because one post taller than it fills it, the answer is
    ///         <see cref="Topmost" />'s: the post being read, so that <c>↓ ↓ ↓ j</c> keeps the reader on it, and past
    ///         the last thing that last thing.
    ///     </para>
    /// </remarks>
    /// <param name="lines">The rows drawn.</param>
    /// <param name="height">How many rows there is room for.</param>
    /// <param name="from">Where the page starts.</param>
    /// <param name="pinned">
    ///     How many rows at the top of the page are pinned there (<see cref="To" />): a top scrolled under them is not
    ///     on the page.
    /// </param>
    public static int? Reclaim(IReadOnlyList<Line> lines, int height, int from, int pinned = 0)
    {
        var begun = new HashSet<int>();
        var start = Math.Max(0, from + pinned);

        for (var at = 0; at < Math.Min(lines.Count, from + height); at++)
        {
            if (lines[at].Item is { } item && begun.Add(item) && at >= start)
            {
                return item;
            }
        }

        return Topmost(lines, from, pinned);
    }

    /// <summary>
    ///     The references of the <paramref name="item" />th thing with a row on the page starting at
    ///     <paramref name="from" />, in the order they are drawn — where <c>→</c> and <c>←</c> enter a walk (#477).
    /// </summary>
    /// <remarks>
    ///     Read off the runs drawn for each reference (<see cref="Span.Reference" />), so a reference wrapped across
    ///     two rows is on the page while either half is.
    /// </remarks>
    /// <param name="lines">The rows drawn.</param>
    /// <param name="height">How many rows there is room for.</param>
    /// <param name="from">Where the page starts.</param>
    /// <param name="item">Which thing's references are asked after.</param>
    /// <param name="pinned">How many rows at the top of the page are pinned there (<see cref="To" />).</param>
    public static IReadOnlyList<Reference> ReferencesOn(
        IReadOnlyList<Line> lines,
        int height,
        int from,
        int item,
        int pinned = 0)
    {
        var shown = new List<Reference>();

        for (var at = Math.Max(0, from + pinned); at < Math.Min(lines.Count, from + height); at++)
        {
            if (lines[at].Item != item)
            {
                continue;
            }

            foreach (var span in lines[at].Spans)
            {
                if (span.Reference is { } reference && !shown.Contains(reference))
                {
                    shown.Add(reference);
                }
            }
        }

        return shown;
    }

    /// <summary>
    ///     Which item the page starting at <paramref name="from" /> begins on, or <see langword="null" /> for a screen
    ///     with no items on it at all. <see cref="Reclaim" />'s fallback, where no thing's top is on the page.
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
    /// <param name="lines">The rows drawn.</param>
    /// <param name="from">Where the page starts.</param>
    /// <param name="pinned">
    ///     How many rows at the top of the page are pinned there (<see cref="To" />): the page begins, for this, on the
    ///     first row below them.
    /// </param>
    public static int? Topmost(IReadOnlyList<Line> lines, int from, int pinned = 0)
    {
        from += pinned;

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
