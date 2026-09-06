using Wooly.Tui.Theme;

namespace Wooly.Tui.Rendering;

/// <summary>
///     The headed runs a screen's rows fall into, and which thing begins the one next along — what <c>[</c> and
///     <c>]</c> move the pick to (#166).
/// </summary>
/// <remarks>
///     Asked of the rows for the reason <see cref="Scroll" /> is asked of them: a heading on screen is what a reader
///     understands a section to be, so deriving the runs from the marks on the rows makes the sections they see and
///     the sections the key moves between the same sections by construction. A screen answering this itself would be
///     keeping a second count in step with the headings it draws by hand, which is the arrangement
///     <see cref="Screens.Picked{T}" /> exists to avoid (#51).
///     <para>
///         Beside <see cref="Scroll" /> rather than in it: every question there is about which row a page begins on,
///         and this one is about which <em>thing</em> the pick lands on, which a screen with no page at all would
///         still answer the same way.
///     </para>
/// </remarks>
public static class Sections
{
    /// <summary>
    ///     The thing beginning the headed run <paramref name="by" /> runs along from the one the reader is in, or
    ///     <see langword="null" /> where there is no run that way — which is what clamping at the ends looks like from
    ///     outside, and what a screen with no headings at all answers to every press.
    /// </summary>
    /// <remarks>
    ///     The first thing of the run rather than the heading itself, because the heading is part of no thing and
    ///     there is nothing there to pick out: <c>]</c> is "take me to the posts", and a reader who arrives at a post
    ///     wants to press <c>⏎</c> rather than <c>j</c> first.
    ///     <para>
    ///         Whole runs on both sides, so <c>[</c> from the fifth post is the first hashtag rather than the first
    ///         post: a key that sometimes moved to the top of the run it was already in and sometimes left it would be
    ///         two keys wearing one bracket.
    ///     </para>
    ///     <para>
    ///         Things standing above the first heading are a run of their own — the account screen's header block is
    ///         one — so <c>]</c> reaches the first headed run from there and <c>[</c> clamps.
    ///     </para>
    /// </remarks>
    /// <param name="lines">The rows as they are drawn now, headings and all.</param>
    /// <param name="by">How many runs along: <c>1</c> for <c>]</c> and <c>-1</c> for <c>[</c>.</param>
    /// <param name="reclaiming">
    ///     The topmost thing on the page, where the pick has been scrolled off it, or <see langword="null" /> while it
    ///     is still visible — so that the jump runs from what the reader is looking at rather than from a pick they
    ///     cannot see (#51).
    /// </param>
    public static int? Along(IReadOnlyList<Line> lines, int by, int? reclaiming)
    {
        if ((reclaiming ?? Picked(lines)) is not { } from)
        {
            return null;
        }

        var starts = Starts(lines);

        // Which run the reader is in, counted as the last one that begins at or before them — and -1 for a thing
        // standing above every heading, which is what makes ] reach the first run from there without a case for it.
        var run = -1;

        for (var at = 0; at < starts.Count; at++)
        {
            if (starts[at] <= from)
            {
                run = at;
            }
        }

        var to = run + by;

        return to >= 0 && to < starts.Count ? starts[to] : null;
    }

    /// <summary>
    ///     Which thing begins each headed run, in the order the runs are drawn: the first row under a heading that is
    ///     part of anything, since a heading is followed by a blank as often as not.
    /// </summary>
    /// <remarks>
    ///     A heading with nothing under it starts no run, which is what keeps this honest on a screen that draws one
    ///     over an empty list — nothing to jump to is nothing to count.
    /// </remarks>
    private static IReadOnlyList<int> Starts(IReadOnlyList<Line> lines)
    {
        var starts = new List<int>();
        var heading = false;

        foreach (var line in lines)
        {
            if (line.Heads)
            {
                heading = true;
            }
            else if (heading && line.Item is { } item)
            {
                starts.Add(item);
                heading = false;
            }
        }

        return starts;
    }

    /// <summary>
    ///     Which thing is picked out, as the rows say it — or <see langword="null" /> on a screen with nothing picked
    ///     out at all, which is nothing to move from.
    /// </summary>
    private static int? Picked(IReadOnlyList<Line> lines)
    {
        foreach (var line in lines)
        {
            if (line.Item is { } item && line.Has(Role.Selection))
            {
                return item;
            }
        }

        return null;
    }
}
