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

    /// <summary>Where the instance's next page starts, or <see langword="null" /> for the top of the list.</summary>
    internal string? MaxId { get; }
}
