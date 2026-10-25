using Wooly.Core.Errors;

namespace Wooly.Core.Paging;

/// <summary>
///     What a read of a paged list came back with, and whether that is all of what was asked for. ADR-0007's second
///     decision, once: a read a rate limit stopped part way through may hold nothing at all, and a caller unable to
///     tell that from an empty list would report "no posts" to a user with a full timeline, "nothing waiting" to
///     somebody with an inbox, and "no followers" to an account with thousands.
///     <para>
///         One type for every list this client reads, rather than one per feature naming its own contents. Naming them
///         cost four records, four unwraps and four JSON envelopes to buy the word <c>Posts</c> at a handful of call
///         sites, and the four copies were four chances for one list to come to answer a rate limit unlike the others
///         (#101). What a fetch holds is said by what a caller asked for — <c>Fetch&lt;Post&gt;</c> is posts — and
///         <c>Items</c> is what they are called on the way past.
///     </para>
///     <para>
///         And, unless the list is <b>whole</b>, where to <b>read on</b> from (<see cref="Onward" />, ADR-0007's
///         amendment): a caller still sees no page and no cursor, only a value it can hand back.
///     </para>
/// </summary>
/// <typeparam name="T">What was being read: a post on a timeline, a notification in an inbox, an account, a conversation.</typeparam>
public sealed record Fetch<T>
{
    /// <summary>What arrived, in the order the instance listed it.</summary>
    public required IReadOnlyList<T> Items { get; init; }

    /// <summary>
    ///     The rate limit that cut the fetch short, or <see langword="null" /> if nothing did. Held as the exception
    ///     itself so a front end that treats this as a failure — the CLI does, per ADR-0006 — can throw the instance's
    ///     own answer rather than a second-hand copy of it.
    /// </summary>
    public required RateLimitedException? StoppedBy { get; init; }

    /// <summary>
    ///     Where to read on from, or <see langword="null" /> where the list is whole — the instance named no next page,
    ///     or a page came back with nothing on it. A fetch a rate limit stopped is never whole, and keeps where it
    ///     stopped, so the next read resumes there rather than past what it never got.
    /// </summary>
    public Onward? Onward { get; init; }

    /// <summary>Whether this is everything the caller asked for, as far as the list goes.</summary>
    public bool IsComplete => StoppedBy is null;

    /// <summary>
    ///     Whether the instance has nothing behind what this holds, which is the instance's call rather than a
    ///     count's: a short page that still names a next page is not the end of the list (ADR-0007).
    /// </summary>
    public bool IsWhole => Onward is null;

    /// <summary>
    ///     A fetch that ran to the end of what was asked for and found the list ends there too: complete, and
    ///     <b>whole</b>.
    /// </summary>
    /// <param name="items">What arrived.</param>
    public static Fetch<T> Complete(IReadOnlyList<T> items) => new() { Items = items, StoppedBy = null };

    /// <summary>
    ///     A fetch that ran to the end of what was asked for, with the list going on behind it: complete, and not
    ///     <b>whole</b>, so it reads on from <paramref name="onward" />.
    /// </summary>
    /// <remarks>
    ///     Apart from <see cref="Complete" />, rather than an onward handed to it, so that what a fetch is called where
    ///     it is built is what <see cref="IsComplete" /> and <see cref="IsWhole" /> go on to answer of it.
    /// </remarks>
    /// <param name="items">What arrived.</param>
    /// <param name="onward">Where the list goes on from.</param>
    public static Fetch<T> GoesOn(IReadOnlyList<T> items, Onward onward) =>
        new() { Items = items, StoppedBy = null, Onward = onward };

    /// <summary>A fetch the instance's rate limit stopped, holding whatever had already arrived.</summary>
    /// <param name="items">What had arrived before the limit.</param>
    /// <param name="rateLimit">The limit that stopped it.</param>
    /// <param name="onward">
    ///     Where the page the limit refused would have started — or, where not said, the top of the list, which is
    ///     where a read stopped on its first page starts again.
    /// </param>
    public static Fetch<T> StoppedShort(
        IReadOnlyList<T> items,
        RateLimitedException rateLimit,
        Onward? onward = null) =>
        new() { Items = items, StoppedBy = rateLimit, Onward = onward ?? Paging.Onward.FromTheTop };
}
