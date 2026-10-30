namespace Wooly.Core.Paging;

/// <summary>
///     Where to <b>read on</b> from: the place in a list just past what a <see cref="Fetch{T}" /> holds, carried so that
///     the next read of that list asks the instance for what stands behind it rather than reading it again from the top
///     (GLOSSARY.md, ADR-0007's amendment).
/// </summary>
/// <remarks>
///     Opaque on purpose. A caller can hold one and hand it back to the reader it came from, and that is all: what it
///     says inside — a <c>max_id</c>, or nothing where a read stopped before anything arrived — is the reader's, as
///     the page size and the end condition are. A caller that could read the cursor would be a caller that could
///     start paging by it, which is the copy of the loop ADR-0007 keeps in one place.
/// </remarks>
public sealed record Onward
{
    /// <summary>Where a read that had got nothing yet reads on from: the top of the list.</summary>
    internal static Onward FromTheTop { get; } = new((string?)null);

    /// <param name="maxId">Where the instance's next page starts, or <see langword="null" /> for the top.</param>
    internal Onward(string? maxId) => MaxId = maxId;

    /// <param name="runs">Where each run of a list read as several reads on from, in the runs' order.</param>
    /// <param name="handedOut">The id of the last item handed out of them, or <see langword="null" /> for none yet.</param>
    internal Onward(IReadOnlyList<RunOnward> runs, string? handedOut)
    {
        Runs = runs;
        HandedOut = handedOut;
    }

    /// <summary>Where the instance's next page starts, or <see langword="null" /> for the top of the list.</summary>
    internal string? MaxId { get; }

    /// <summary>
    ///     Where each run of a list read as several (<see cref="MergedReading" />) reads on from, or
    ///     <see langword="null" /> for a list read as one — or for the top, where no run has been read yet.
    /// </summary>
    internal IReadOnlyList<RunOnward>? Runs { get; }

    /// <summary>
    ///     The id of the last item a merged read handed out, so that a read on hands out only what is older — which is
    ///     how an item two runs both found is handed out once however far apart the two pages it is on arrive.
    /// </summary>
    internal string? HandedOut { get; }
}

/// <summary>Where one run of a list read as several reads on from.</summary>
/// <param name="MaxId">Where the run's next page starts, or <see langword="null" /> for the top of it.</param>
/// <param name="IsWhole">Whether the instance has nothing behind what the run already fetched.</param>
/// <param name="Held">
///     What the run fetched and the merge has not yet handed out, newest first — held rather than asked for again, so
///     reading on asks only of the runs whose fetched items are used up.
/// </param>
internal sealed record RunOnward(string? MaxId, bool IsWhole, IReadOnlyList<object> Held);
