namespace Wooly.Core.Search;

/// <summary>
///     What a search is looking for. One search covers all three kinds of result, so the interesting member is the
///     first: a search asked for nothing in particular wants everything the instance found, and the other three are
///     ways of asking for less.
/// </summary>
public enum SearchKind
{
    /// <summary>Accounts, hashtags and posts alike — what a search asks for unless it says otherwise.</summary>
    Everything,

    /// <summary>Only the accounts.</summary>
    Accounts,

    /// <summary>Only the hashtags.</summary>
    Hashtags,

    /// <summary>Only the posts.</summary>
    Posts,
}
