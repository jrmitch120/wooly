using System.Runtime.ExceptionServices;
using Mastonet;
using Wooly.Core.Errors;

namespace Wooly.Core.Paging;

/// <summary>
///     Asks an instance for one list it will only serve as several — a <b>Tag set</b> of more tags than one ask reads
///     (ADR-0033) — and interleaves them newest first by id, an item more than one run found handed out once. Each run
///     pages on its own <c>max_id</c> cursor, kept with what it fetched and the merge has not yet used inside the
///     <see cref="Onward" />, which stays opaque to callers as every other does.
///     <para>
///         An item is handed out only once every run that is not whole has something fetched to set against it, so the
///         merge never hands out a post ahead of a newer one a run has not yet been asked for. Reading on therefore asks
///         again only of the runs whose fetched items are used up.
///     </para>
///     <para>
///         Any run failing fails the read, and nothing of the runs that answered is kept: a merged list with one run
///         missing looks whole while missing posts from its middle. A run the instance's rate limit refuses is no
///         different, except in how it is said — as a fetch the limit stopped (ADR-0006, ADR-0007), holding nothing and
///         reading on from where this read started, so that waiting it out and reading on asks again exactly what this
///         asked.
///     </para>
/// </summary>
internal static class MergedReading
{
    /// <summary>Orders ids as the numbers Mastodon makes them, though they travel as strings.</summary>
    private static readonly Comparer<string> ByIdAsNumber = Comparer<string>.Create((one, other) =>
        one.Length != other.Length ? one.Length.CompareTo(other.Length) : string.CompareOrdinal(one, other));

    /// <summary>Collects up to <paramref name="limit" /> items from <paramref name="runs" />, merged newest first.</summary>
    /// <param name="limit">The most items to hand out.</param>
    /// <param name="pageSize">The most the endpoint serves in one call.</param>
    /// <param name="runs">Each run's one page, given what to ask for, in the runs' order.</param>
    /// <param name="asItem">Turns one thing off the wire into the domain value a caller wanted.</param>
    /// <param name="idOf">
    ///     The instance's id for an item, which the runs are merged by and paged by where an instance names no next page.
    /// </param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <param name="from">Where to read on from, as a fetch of these same runs handed it out, or null for the top.</param>
    public static async Task<Fetch<TItem>> Collect<TWire, TItem>(
        int limit,
        int pageSize,
        IReadOnlyList<Func<ArrayOptions, Task<Page<TWire>>>> runs,
        Func<TWire, TItem> asItem,
        Func<TItem, string> idOf,
        CancellationToken cancellationToken,
        Onward? from = null)
    {
        var placed = from?.Runs is { } held && held.Count == runs.Count;
        var states = placed
            ? from!.Runs!.Select(run => new Run<TItem>(run.MaxId, run.IsWhole, run.Held.Cast<TItem>())).ToList()
            : runs.Select(_ => new Run<TItem>(maxId: null, isWhole: false, [])).ToList();
        var handedOut = placed ? from!.HandedOut : null;
        var items = new List<TItem>();

        while (items.Count < limit)
        {
            cancellationToken.ThrowIfCancellationRequested();

            DropHandedOut(states, handedOut, idOf);

            var spent = Enumerable.Range(0, runs.Count)
                .Where(index => !states[index].IsWhole && states[index].Held.Count == 0)
                .ToList();

            if (spent.Count > 0)
            {
                var wanted = Math.Min(pageSize, limit - items.Count);
                var asks = spent
                    .Select(index => runs[index](new ArrayOptions { Limit = wanted, MaxId = states[index].MaxId }))
                    .ToList();

                try
                {
                    await Task.WhenAll(asks);
                }
                catch (Exception) when (asks.Any(ask => ask.IsFaulted))
                {
                    var thrown = asks.Where(ask => ask.IsFaulted).SelectMany(ask => ask.Exception!.InnerExceptions);
                    var failures = thrown.ToList();

                    // A run that failed outright is the failure to say, ahead of one that was only asked too soon.
                    if (failures.FirstOrDefault(failure => failure is not RateLimitedException) is { } failed)
                    {
                        ExceptionDispatchInfo.Throw(failed);
                    }

                    return Fetch<TItem>.StoppedShort(
                        [],
                        (RateLimitedException)failures[0],
                        from ?? Onward.FromTheTop);
                }

                foreach (var (index, ask) in spent.Zip(asks))
                {
                    var page = ask.Result;
                    var state = states[index];
                    var read = page.Items.Select(asItem).ToList();

                    foreach (var item in read)
                    {
                        state.Held.Enqueue(item);
                    }

                    // The end of the run as PagedReading reads the end of a list: nothing came back, or the instance
                    // named no next page and the page had room to spare.
                    state.MaxId = read.Count == 0
                        ? null
                        : page.NextPageMaxId ?? (read.Count < wanted ? null : idOf(read[^1]));
                    state.IsWhole = state.MaxId is null;
                }

                continue;
            }

            var newest = states
                .Where(state => state.Held.Count > 0)
                .MaxBy(state => idOf(state.Held.Peek()), ByIdAsNumber);

            // Every run is whole and has nothing left to hand out.
            if (newest is null)
            {
                break;
            }

            var next = newest.Held.Dequeue();

            items.Add(next);
            handedOut = idOf(next);
        }

        DropHandedOut(states, handedOut, idOf);

        if (states.All(state => state.IsWhole && state.Held.Count == 0))
        {
            return Fetch<TItem>.Complete(items);
        }

        return Fetch<TItem>.GoesOn(
            items,
            new Onward(
                [.. states.Select(state => new RunOnward(state.MaxId, state.IsWhole, [.. state.Held.Cast<object>()]))],
                handedOut));
    }

    /// <summary>
    ///     Drops from the head of each run what is not older than the last item handed out: it was handed out already,
    ///     by another run that found it too. A run is newest first, so only its head can be.
    /// </summary>
    private static void DropHandedOut<TItem>(List<Run<TItem>> states, string? handedOut, Func<TItem, string> idOf)
    {
        if (handedOut is null)
        {
            return;
        }

        foreach (var state in states)
        {
            while (state.Held.TryPeek(out var head) && ByIdAsNumber.Compare(idOf(head), handedOut) >= 0)
            {
                state.Held.Dequeue();
            }
        }
    }

    /// <summary>One run as the merge goes through it.</summary>
    private sealed class Run<TItem>(string? maxId, bool isWhole, IEnumerable<TItem> held)
    {
        public string? MaxId { get; set; } = maxId;

        public bool IsWhole { get; set; } = isWhole;

        public Queue<TItem> Held { get; } = new(held);
    }
}
