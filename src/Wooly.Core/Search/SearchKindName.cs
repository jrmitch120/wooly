namespace Wooly.Core.Search;

/// <summary>
///     How a <see cref="SearchKind" /> is spelled where a user writes one, in the one place every entry point can reach
///     it — <see cref="Posts.PostVisibilityName" /> for <c>--type</c>, and for the same reason: the CLI's flag and the
///     TUI's search prompt take the same three words, and neither may come to accept a word the other does not.
/// </summary>
public static class SearchKindName
{
    /// <summary>
    ///     The three a user can ask for. <see cref="SearchKind.Everything" /> is not among them: it is what a search
    ///     that named no kind is already doing, so offering a word for it would be a second way to write the default.
    /// </summary>
    private static readonly SearchKind[] Askable = [SearchKind.Accounts, SearchKind.Hashtags, SearchKind.Posts];

    /// <summary>Every spelling this client accepts, listed the way an error message wants them.</summary>
    public static string Accepted => string.Join(", ", Askable.Select(Of));

    /// <summary>The spelling of <paramref name="kind" />: lower case and plural, as <c>--type</c> takes it.</summary>
    public static string Of(SearchKind kind) => kind.ToString().ToLowerInvariant();

    /// <summary>The kind <paramref name="name" /> spells, or <see langword="null" /> if it spells none of them.</summary>
    /// <remarks>
    ///     Matched against the spellings by hand rather than through <see cref="Enum.TryParse{TEnum}(string, out TEnum)" />,
    ///     which would also accept the underlying numbers ("1") and — worse here — the name of the member that stands
    ///     for asking for nothing in particular.
    /// </remarks>
    public static SearchKind? Parse(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        var trimmed = name.Trim();

        foreach (var kind in Askable)
        {
            if (string.Equals(Of(kind), trimmed, StringComparison.OrdinalIgnoreCase))
            {
                return kind;
            }
        }

        return null;
    }

    /// <summary>
    ///     How a value that is not a kind of result is described, shared so that every entry point turning one down
    ///     says the same thing about the same word.
    /// </summary>
    public static string Rejection(string name) => $"'{name}' is not a kind of result. Use one of: {Accepted}.";
}
