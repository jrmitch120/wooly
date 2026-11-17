namespace Wooly.Core.Trends;

/// <summary>
///     How a <see cref="TrendKind" /> is spelled where a user writes one, as <see cref="Search.SearchKindName" /> is for
///     a search: <c>tags</c>, <c>posts</c> or <c>links</c>. Hashtags are <c>tags</c> here rather than search's
///     <c>hashtags</c> because that is the word the instance's own endpoint uses, and the spec's (#601).
/// </summary>
public static class TrendKindName
{
    /// <summary>The three, in the order <c>trends</c> writes them.</summary>
    public static IReadOnlyList<TrendKind> All { get; } = [TrendKind.Hashtags, TrendKind.Posts, TrendKind.Links];

    /// <summary>Every spelling this client accepts, listed the way an error message wants them.</summary>
    public static string Accepted => string.Join(", ", All.Select(Of));

    /// <summary>The spelling of <paramref name="kind" />, as <c>--type</c> takes it.</summary>
    public static string Of(TrendKind kind) => kind switch
    {
        TrendKind.Hashtags => "tags",
        TrendKind.Posts => "posts",
        TrendKind.Links => "links",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Not a kind of trend."),
    };

    /// <summary>The kind <paramref name="name" /> spells, or <see langword="null" /> if it spells none of them.</summary>
    /// <remarks>
    ///     Matched by hand rather than through <see cref="Enum.TryParse{TEnum}(string, out TEnum)" />, which would take
    ///     the underlying numbers and the enum's own member names — <c>hashtags</c> among them, a second spelling of
    ///     <c>tags</c> that the help never offers.
    /// </remarks>
    public static TrendKind? Parse(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        var trimmed = name.Trim();

        foreach (var kind in All)
        {
            if (string.Equals(Of(kind), trimmed, StringComparison.OrdinalIgnoreCase))
            {
                return kind;
            }
        }

        return null;
    }

    /// <summary>How a word that is not a kind of trend is described, by every entry point turning one down.</summary>
    public static string Rejection(string name) => $"'{name}' is not a kind of trend. Use one of: {Accepted}.";
}
