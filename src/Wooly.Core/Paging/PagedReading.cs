using Mastonet;
using Mastonet.Entities;
using Wooly.Core.Errors;

namespace Wooly.Core.Paging;

/// <summary>
///     Asks an instance for as many of something as a caller wanted, a page at a time. ADR-0007's first decision made
///     reusable: a caller asks for a count, and the <c>max_id</c> cursor, the ceiling an instance serves and the number
///     of calls it takes stay in here. The ADR warned that every later list — notifications, searches, an account's
///     posts — would otherwise carry a copy of this loop, and that each copy would get the end condition slightly
///     differently; this is the one copy.
/// </summary>
internal static class PagedReading
{
    /// <summary>Collects up to <paramref name="limit" /> items, newest first.</summary>
    /// <param name="pageSize">
    ///     The most the endpoint being read serves in one call, so the most there is any point asking it for. Passed in
    ///     rather than fixed here because Mastodon does not answer with the same ceiling everywhere — a timeline serves
    ///     40 and notifications 30 — and asking for more than an endpoint gives makes every full page look short, which
    ///     is the one thing this loop reads as the end of the list.
    /// </param>
    /// <param name="readPage">Asks the instance for one page, given what to ask for.</param>
    /// <param name="asItem">Turns one thing off the wire into the domain value a caller wanted.</param>
    /// <param name="idOf">
    ///     The instance's id for one thing off the wire, needed as a fallback cursor for an instance that names no next
    ///     page.
    /// </param>
    /// <returns>
    ///     What arrived, and the rate limit that stopped the rest if one did. Nothing waits here — ADR-0006 leaves that
    ///     choice to whichever front end is reading.
    /// </returns>
    public static async Task<Paged<TItem>> Collect<TWire, TItem>(
        int limit,
        int pageSize,
        Func<ArrayOptions, Task<MastodonList<TWire>>> readPage,
        Func<TWire, TItem> asItem,
        Func<TWire, string> idOf,
        CancellationToken cancellationToken)
    {
        var items = new List<TItem>();
        string? nextPage = null;

        while (items.Count < limit)
        {
            // Mastonet's calls take no cancellation token of their own, so a Ctrl-C lands between pages rather than
            // during one. Between pages is where the pages this loop added are, which is the part a caller could not
            // have stopped itself.
            cancellationToken.ThrowIfCancellationRequested();

            var wanted = Math.Min(pageSize, limit - items.Count);
            MastodonList<TWire> page;

            try
            {
                page = await readPage(new ArrayOptions { Limit = wanted, MaxId = nextPage });
            }
            catch (RateLimitedException rateLimit)
            {
                return new Paged<TItem>(items, rateLimit);
            }

            items.AddRange(page.Select(asItem));

            // Nothing came back, so asking again cannot do better however much the instance says is left.
            if (page.Count == 0)
            {
                break;
            }

            // The instance names where the next page starts in a link header, and that is the authority on whether
            // there is one: a page can come back short of what was asked for and still not be the last, because an
            // instance drops filtered items from a page after counting them. Only where it names no next page does a
            // page with room to spare mean the end — and there the oldest thing just read is where a further page
            // would start, since a page comes back newest first.
            nextPage = page.NextPageMaxId ?? (page.Count < wanted ? null : idOf(page[^1]));

            if (nextPage is null)
            {
                break;
            }
        }

        return new Paged<TItem>(items, null);
    }
}
