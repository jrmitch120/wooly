namespace Wooly.Core.Trends;

/// <summary>
///     The three things an instance ranks as trending, each read from an endpoint of its own. Unlike
///     <see cref="Search.SearchKind" /> there is no "everything" here: there is no one call that answers all three, so
///     asking for every kind is asking for each of them in turn.
/// </summary>
public enum TrendKind
{
    /// <summary>Trending hashtags, from <c>/api/v1/trends/tags</c>.</summary>
    Hashtags,

    /// <summary>Trending posts, from <c>/api/v1/trends/statuses</c>.</summary>
    Posts,

    /// <summary>Trending links (GLOSSARY.md's <b>Trending link</b>), from <c>/api/v1/trends/links</c>.</summary>
    Links,
}
