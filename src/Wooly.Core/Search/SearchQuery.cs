namespace Wooly.Core.Search;

/// <summary>
///     What to look for, and which kinds of result are wanted back. Reached through <see cref="For" /> rather than a
///     constructor so that a query with nothing in it cannot be built — an instance answers a blank search with a
///     refusal, and asking one is a mistake worth catching where the user typed it.
/// </summary>
public sealed record SearchQuery
{
    private SearchQuery(string text, SearchKind kind)
    {
        Text = text;
        Kind = kind;
    }

    /// <summary>What is being looked for, trimmed of the whitespace a shell leaves around a quoted argument.</summary>
    public string Text { get; }

    /// <summary>Which kinds of result are wanted, which is all of them unless the caller narrowed it.</summary>
    public SearchKind Kind { get; }

    /// <summary>
    ///     How a query with nothing to look for is described, shared so that the command turning one down and the
    ///     domain turning one down cannot say different things about the same empty value.
    /// </summary>
    public static string Rejection => "Give something to search for — a word, a #hashtag, an @account or a link.";

    /// <summary>Whether <paramref name="text" /> is something an instance can be asked to look for.</summary>
    public static bool IsWellFormed(string? text) => !string.IsNullOrWhiteSpace(text);

    /// <summary>A search for <paramref name="text" />, narrowed to <paramref name="kind" />.</summary>
    /// <exception cref="ArgumentException">
    ///     <paramref name="text" /> is blank. A caller is expected to have rejected that against what the user typed;
    ///     reaching here with one is a defect, not user error.
    /// </exception>
    public static SearchQuery For(string text, SearchKind kind = SearchKind.Everything)
    {
        if (!IsWellFormed(text))
        {
            throw new ArgumentException(Rejection, nameof(text));
        }

        return new SearchQuery(text.Trim(), kind);
    }
}
