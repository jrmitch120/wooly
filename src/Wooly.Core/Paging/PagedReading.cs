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
    ///     The instance's id for one thing off the wire, used as a fallback cursor for an instance that names no next
    ///     page — or <see langword="null" /> where the things being read are not what the endpoint pages by. A timeline
    ///     and an inbox page by the id of the post or notification in hand, so they have a fallback to give; a list of
    ///     accounts pages by the id of the follow rather than of the account, which is not a value this client ever
    ///     sees. Guessing there would ask for a page starting somewhere in another id space altogether, and silently
    ///     skip or repeat accounts. Where it is null, an instance that names no next page has ended the list.
    /// </param>
    /// <param name="stopWhen">
    ///     Read against each item as it arrives; the collection ends with the page holding the first one it answers
    ///     true for. This is how something is looked <em>up</em> in a list an instance will only serve in order —
    ///     asking for the caller's whole limit and searching afterwards would spend every page's worth of calls to
    ///     find a thing on the first page. Where null, the collection runs to the limit or to the end of the list.
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
        Func<TWire, string>? idOf,
        CancellationToken cancellationToken,
        Func<TItem, bool>? stopWhen = null)
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

            var arrived = page.Select(asItem).ToList();

            items.AddRange(arrived);

            // Nothing came back, so asking again cannot do better however much the instance says is left.
            if (page.Count == 0)
            {
                break;
            }

            // What the caller was looking for has turned up, so there is nothing further to ask for. The rest of this
            // page comes back with it rather than being trimmed away: it arrived, and which of a page's items a caller
            // wanted is the caller's business.
            if (stopWhen is not null && arrived.Any(stopWhen))
            {
                break;
            }

            // The instance names where the next page starts in a link header, and that is the authority on whether
            // there is one: a page can come back short of what was asked for and still not be the last, because an
            // instance drops filtered items from a page after counting them. Only where it names no next page does a
            // page with room to spare mean the end — and there the oldest thing just read is where a further page
            // would start, since a page comes back newest first. A caller that gave no fallback has nothing this
            // endpoint pages by, and takes the missing link header as the end of the list.
            nextPage = page.NextPageMaxId ?? (page.Count < wanted || idOf is null ? null : idOf(page[^1]));

            if (nextPage is null)
            {
                break;
            }
        }

        return new Paged<TItem>(items, null);
    }
}
