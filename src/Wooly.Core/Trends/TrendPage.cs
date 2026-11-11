namespace Wooly.Core.Trends;

/// <summary>
///     Which page of a kind of trend to read: how many, and how far in. Every trends endpoint pages by offset rather than
///     by an id to read on from, because what it serves is a ranking that the instance recomputes as it goes rather than
///     a list that only grows at one end (ADR-0036).
/// </summary>
public sealed record TrendPage
{
    private TrendPage(int? limit, int offset)
    {
        Limit = limit;
        Offset = offset;
    }

    /// <summary>
    ///     The first page at whatever size the instance serves when asked for none — ten hashtags, twenty posts and ten
    ///     links on a stock Mastodon — which is what <c>trends</c> with no <c>--limit</c> asks for.
    /// </summary>
    public static TrendPage First { get; } = new(null, 0);

    /// <summary>How many to ask for, or <see langword="null" /> to leave it to the instance.</summary>
    public int? Limit { get; }

    /// <summary>How many of the ranking to pass over first; zero for the top of it.</summary>
    public int Offset { get; }

    /// <summary>A page of <paramref name="limit" /> trends, <paramref name="offset" /> into the ranking.</summary>
    /// <exception cref="ArgumentOutOfRangeException">
    ///     <paramref name="limit" /> is below one or <paramref name="offset" /> below zero. A caller is expected to have
    ///     turned either down against what the user typed (<see cref="Rejection" />); reaching here with one is a defect.
    /// </exception>
    public static TrendPage Of(int? limit, int offset = 0)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(limit ?? 1, 1, nameof(limit));
        ArgumentOutOfRangeException.ThrowIfNegative(offset);

        return new TrendPage(limit, offset);
    }

    /// <summary>
    ///     The most of <paramref name="kind" /> an instance serves in one page: forty posts, or twenty hashtags or links.
    ///     Mastodon quietly serves its maximum for any limit above it, which would leave a script asking for fifty
    ///     reading a page of twenty as the end of the ranking — so a larger limit is turned down instead.
    /// </summary>
    public static int Most(TrendKind kind) => kind is TrendKind.Posts ? 40 : 20;

    /// <summary>
    ///     What is wrong with asking every kind in <paramref name="asked" /> for a page of <paramref name="limit" /> at
    ///     <paramref name="offset" />, or <see langword="null" /> where nothing is — the one rule the CLI's options and
    ///     any other entry point turn a page down by, so neither can come to accept a page the other refuses.
    /// </summary>
    public static string? Rejection(int? limit, int offset, IReadOnlyCollection<TrendKind> asked)
    {
        if (offset < 0)
        {
            return "An offset cannot be below zero.";
        }

        if (limit is null)
        {
            return null;
        }

        if (limit < 1)
        {
            return "Ask for at least one trend.";
        }

        // Every kind asked takes the same limit, so the smallest maximum among them is the one that binds: asking all
        // three for thirty would be a full answer for posts and a quietly short one for hashtags and links.
        var tightest = asked.OrderBy(Most).First();

        return limit > Most(tightest)
            ? $"At most {Most(tightest)} trending {TrendKindName.Of(tightest)} can be asked for at once."
            : null;
    }
}
