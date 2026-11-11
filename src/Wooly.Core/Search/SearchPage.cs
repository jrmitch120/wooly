namespace Wooly.Core.Search;

/// <summary>
///     Which page of a search to ask for: how many of each kind, and how far into one kind to start (#599). Both are
///     optional, and one left unsaid is not sent — the instance's own default is then what is served, which is what a
///     search asked nothing of has always been answered with.
///     <para>
///         The offset is only half a fact on its own. An instance pages one kind at a time and ignores an offset sent
///         without a kind, answering with the first page again; <see cref="SearchQuery.For" /> is where a page with an
///         offset is refused for a search of every kind, since that is where the kind and the page meet.
///     </para>
/// </summary>
public sealed record SearchPage
{
    /// <summary>
    ///     The most of one kind an instance serves in one answer. Mastodon caps <c>limit</c> here per kind, so asking for
    ///     more would be asking for something that is quietly not done.
    /// </summary>
    public const int MostPerKind = 40;

    private SearchPage(int? limit, int? offset)
    {
        Limit = limit;
        Offset = offset;
    }

    /// <summary>
    ///     The first page at the instance's own size: nothing sent, and so the answer every search was given before it
    ///     could be paged.
    /// </summary>
    public static SearchPage First { get; } = new(null, null);

    /// <summary>How many of each kind to ask for, or <see langword="null" /> for the instance's own default.</summary>
    public int? Limit { get; }

    /// <summary>How many of the one kind asked for to skip, or <see langword="null" /> to start at the first.</summary>
    public int? Offset { get; }

    /// <summary>Whether <paramref name="limit" /> is a number of each kind an instance can be asked for.</summary>
    public static bool IsWellFormedLimit(int limit) => limit is >= 1 and <= MostPerKind;

    /// <summary>Whether <paramref name="offset" /> is a number of results that can be skipped.</summary>
    public static bool IsWellFormedOffset(int offset) => offset >= 0;

    /// <summary>A page of <paramref name="limit" /> of each kind, starting <paramref name="offset" /> in.</summary>
    /// <exception cref="ArgumentOutOfRangeException">
    ///     Either is outside what an instance takes. A caller is expected to have turned that down against what the
    ///     user typed; reaching here with one is a defect.
    /// </exception>
    public static SearchPage Of(int? limit = null, int? offset = null)
    {
        if (limit is { } asked && !IsWellFormedLimit(asked))
        {
            throw new ArgumentOutOfRangeException(nameof(limit), asked, $"A limit is between 1 and {MostPerKind}.");
        }

        if (offset is { } skipped && !IsWellFormedOffset(skipped))
        {
            throw new ArgumentOutOfRangeException(nameof(offset), skipped, "An offset is zero or more.");
        }

        return new SearchPage(limit, offset);
    }
}
