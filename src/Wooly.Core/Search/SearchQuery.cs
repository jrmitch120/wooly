namespace Wooly.Core.Search;

/// <summary>
///     What to look for, and which kinds of result are wanted back. Reached through <see cref="For" /> rather than a
///     constructor so that a query with nothing in it cannot be built — an instance answers a blank search with a
///     refusal, and asking one is a mistake worth catching where the user typed it.
/// </summary>
public sealed record SearchQuery
{
    private SearchQuery(string text, SearchKind kind, SearchPage page)
    {
        Text = text;
        Kind = kind;
        Page = page;
    }

    /// <summary>What is being looked for, trimmed of the whitespace a shell leaves around a quoted argument.</summary>
    public string Text { get; }

    /// <summary>Which kinds of result are wanted, which is all of them unless the caller narrowed it.</summary>
    public SearchKind Kind { get; }

    /// <summary>
    ///     Which page of what was found to ask for (#599), which is the instance's own first page unless the caller
    ///     said otherwise.
    /// </summary>
    public SearchPage Page { get; }

    /// <summary>
    ///     How a page past the first of every kind is described: an instance pages one kind at a time, and ignores an
    ///     offset sent without one. Shared for the reason <see cref="Rejection" /> is.
    /// </summary>
    public static string PagingRejection =>
        "Only a search for one kind of result can be paged: an instance ignores an offset without one.";

    /// <summary>Whether a search for <paramref name="kind" /> can start past its first result.</summary>
    public static bool IsPageable(SearchKind kind) => kind is not SearchKind.Everything;

    /// <summary>
    ///     How a query with nothing to look for is described, shared so that the command turning one down and the
    ///     domain turning one down cannot say different things about the same empty value.
    /// </summary>
    public static string Rejection => "Give something to search for — a word, a #hashtag, an @account or a link.";

    /// <summary>Whether <paramref name="text" /> is something an instance can be asked to look for.</summary>
    public static bool IsWellFormed(string? text) => !string.IsNullOrWhiteSpace(text);

    /// <summary>
    ///     A search for <paramref name="text" />, narrowed to <paramref name="kind" />, asking for
    ///     <paramref name="page" /> — the instance's own first page where none is given.
    /// </summary>
    /// <exception cref="ArgumentException">
    ///     <paramref name="text" /> is blank, or <paramref name="page" /> has an offset and <paramref name="kind" /> is
    ///     every kind. A caller is expected to have rejected either against what the user typed; reaching here with
    ///     one is a defect, not user error.
    /// </exception>
    public static SearchQuery For(string text, SearchKind kind = SearchKind.Everything, SearchPage? page = null)
    {
        if (!IsWellFormed(text))
        {
            throw new ArgumentException(Rejection, nameof(text));
        }

        if (page?.Offset is not null && !IsPageable(kind))
        {
            throw new ArgumentException(PagingRejection, nameof(page));
        }

        return new SearchQuery(text.Trim(), kind, page ?? SearchPage.First);
    }
}
