using Wooly.Core.Accounts;
using Wooly.Core.Posts;

namespace Wooly.Core.Search;

/// <summary>
///     What a search found, in the three kinds an instance answers with. A kind that was not asked for is
///     <see langword="null" /> rather than an empty list, which is the whole distinction this record exists to keep: a
///     caller told <c>[]</c> for the posts cannot tell "you asked for accounts only" from "nothing you searched for was
///     posted", and would report the second to a user who did the first.
/// </summary>
public sealed record SearchResults
{
    /// <summary>The accounts found, or <see langword="null" /> if accounts were not asked for.</summary>
    public IReadOnlyList<Account>? Accounts { get; init; }

    /// <summary>The hashtags found, or <see langword="null" /> if hashtags were not asked for.</summary>
    public IReadOnlyList<Hashtag>? Hashtags { get; init; }

    /// <summary>The posts found, or <see langword="null" /> if posts were not asked for.</summary>
    public IReadOnlyList<Post>? Posts { get; init; }

    /// <summary>Whether the search turned up nothing at all of what it was asked for.</summary>
    public bool IsEmpty => (Accounts?.Count ?? 0) + (Hashtags?.Count ?? 0) + (Posts?.Count ?? 0) == 0;

    /// <summary>
    ///     What a search for <paramref name="kind" /> found, keeping the kinds it asked for and dropping the rest.
    /// </summary>
    /// <remarks>
    ///     The dropping happens here rather than in the adapter that called the instance, so that every front end
    ///     narrows a search the same way — an instance answering with all three kinds however it is asked (see
    ///     ADR-0011) is exactly the situation where two callers would otherwise each decide what <c>--type</c> means.
    /// </remarks>
    public static SearchResults Matching(
        SearchKind kind,
        IReadOnlyList<Account> accounts,
        IReadOnlyList<Hashtag> hashtags,
        IReadOnlyList<Post> posts) => new()
    {
        Accounts = Wanted(kind, SearchKind.Accounts) ? accounts : null,
        Hashtags = Wanted(kind, SearchKind.Hashtags) ? hashtags : null,
        Posts = Wanted(kind, SearchKind.Posts) ? posts : null,
    };

    private static bool Wanted(SearchKind asked, SearchKind kind) => asked is SearchKind.Everything || asked == kind;
}
