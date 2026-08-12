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

    /// <summary>Whether this is everything the caller asked for, as far as the list goes.</summary>
    public bool IsComplete => StoppedBy is null;

    /// <summary>A fetch that ran to the end of what was asked for.</summary>
    public static Fetch<T> Complete(IReadOnlyList<T> items) => new() { Items = items, StoppedBy = null };

    /// <summary>A fetch the instance's rate limit stopped, holding whatever had already arrived.</summary>
    public static Fetch<T> StoppedShort(IReadOnlyList<T> items, RateLimitedException rateLimit) =>
        new() { Items = items, StoppedBy = rateLimit };
}
