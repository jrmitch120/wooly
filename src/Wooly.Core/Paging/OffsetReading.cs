namespace Wooly.Core.Paging;

/// <summary>
///     Reads a list the instance pages by offset — a search narrowed to one kind, a kind of trend — a page at a time,
///     handing back where to <b>read on</b> from as the same opaque <see cref="Onward" /> a list paged by
///     <c>max_id</c> hands back (ADR-0007's amendments, #604). A screen reading on one cannot tell which kind of paging
///     is behind it, and needs no second way of holding its place.
/// </summary>
/// <remarks>
///     The one copy of the end condition for these lists, as <see cref="PagedReading" /> is for the others: a page
///     that comes back short of what was asked is the end of the list, and anything at least as long as the ask is
///     not — an instance puts an exact-match hashtag ahead of the forty it ranked, so a full first page can hold one
///     more than was asked for (#599). The next page starts where the ask says, the offset this one started at plus
///     the limit, rather than where the count says, since that one extra is not part of the ranking the offset counts
///     through.
///     <para>
///         Reached by what the port already asks with — a limit and an offset, which the CLI names outright — rather
///         than by a second read on every port that pages this way: the port answers one page, and only this says
///         which page comes next.
///     </para>
/// </remarks>
public static class OffsetReading
{
    /// <summary>Reads the page <paramref name="from" /> says, or the first where it says nothing.</summary>
    /// <param name="limit">How many to ask for at a time, which is what a full page holds.</param>
    /// <param name="from">
    ///     Where a fetch of this same list said to read on from, or <see langword="null" /> for the top of it.
    /// </param>
    /// <param name="page">Asks the instance for one page, given how many and how far in.</param>
    /// <param name="cancellationToken">Cancels the ask.</param>
    /// <returns>
    ///     The page, and where to read on from unless it was short. Never stopped by a rate limit: a page is one call,
    ///     which a limit refuses with nothing in hand, so it is raised rather than answered as half a page (ADR-0011) —
    ///     and whoever reads on waits it out or fails, as its front end decides (ADR-0006).
    /// </returns>
    /// <exception cref="Errors.RateLimitedException">The rate limit refused the page.</exception>
    public static async Task<Fetch<T>> Read<T>(
        int limit,
        Onward? from,
        Func<int, int, CancellationToken, Task<IReadOnlyList<T>>> page,
        CancellationToken cancellationToken)
    {
        var offset = from?.Offset ?? 0;
        var items = await page(limit, offset, cancellationToken);

        return items.Count < limit ? Fetch<T>.Complete(items) : Fetch<T>.GoesOn(items, Onward.Past(offset + limit));
    }
}
