using Wooly.Core.Accounts;
using Wooly.Core.Posts;
using Wooly.Core.Search;

namespace Wooly.Tui.Screens;

/// <summary>
///     One thing a search turned up, as the screen walks them: an account, a hashtag, or a post.
/// </summary>
/// <remarks>
///     One list rather than the three <see cref="SearchResults" /> holds, because what <c>j</c> and <c>k</c> walk is
///     one list — and saying so outright is what stops the order the results are drawn in and the order they are
///     picked out in drifting apart.
///     <para>
///         A shape this screen makes rather than one the domain has, which is why it lives here and not in
///         <c>Wooly.Core</c>: the CLI reads a search in its three kinds and has no selection to walk at all, so
///         flattening them is a navigation fact about one front end. ADR-0011's one ask and three kinds is untouched.
///     </para>
/// </remarks>
public abstract record Result
{
    /// <remarks>Closed, so that the three kinds a search has are the three kinds a row can be drawn from.</remarks>
    private Result()
    {
    }

    /// <summary>An account the search found.</summary>
    public sealed record OfAccount(Account Account) : Result;

    /// <summary>A hashtag the search found.</summary>
    public sealed record OfHashtag(Hashtag Hashtag) : Result;

    /// <summary>A post the search found.</summary>
    public sealed record OfPost(Post Post) : Result;
}
