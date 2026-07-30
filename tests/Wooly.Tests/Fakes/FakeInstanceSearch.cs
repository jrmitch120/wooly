using Wooly.Core.Accounts;
using Wooly.Core.Errors;
using Wooly.Core.Posts;
using Wooly.Core.Profiles;
using Wooly.Core.Search;

namespace Wooly.Tests.Fakes;

/// <summary>
///     An instance's search without the instance. ADR-0005's primary seam for anything above the API layer: a command
///     test says what is out there to be found and then asks what was searched for, and never fakes HTTP to do it.
///     <para>
///         It narrows what it holds through <see cref="SearchResults.Matching" />, exactly as the real adapter does, so
///         that a test of <c>--type</c> sees what a user would rather than what a fake decided to hand back.
///     </para>
/// </summary>
internal sealed class FakeInstanceSearch : IInstanceSearch
{
    private readonly IReadOnlyList<Account> _accounts;
    private readonly IReadOnlyList<Hashtag> _hashtags;
    private readonly IReadOnlyList<Post> _posts;
    private readonly RateLimitedException? _refusal;

    private FakeInstanceSearch(
        IReadOnlyList<Account> accounts,
        IReadOnlyList<Hashtag> hashtags,
        IReadOnlyList<Post> posts,
        RateLimitedException? refusal = null)
    {
        _accounts = accounts;
        _hashtags = hashtags;
        _posts = posts;
        _refusal = refusal;
    }

    /// <summary>Every search it was asked for, in order — where a test proves what a command went looking for.</summary>
    public List<Call> Searches { get; } = [];

    /// <summary>An instance holding all three kinds of thing, whatever is searched for.</summary>
    public static FakeInstanceSearch Finding(
        Account[]? accounts = null,
        Hashtag[]? hashtags = null,
        Post[]? posts = null) =>
        new(accounts ?? [AnAccount.With()], hashtags ?? [AHashtag.With()], posts ?? [APost.With()]);

    /// <summary>An instance with nothing matching whatever is searched for.</summary>
    public static FakeInstanceSearch FindingNothing() => new([], [], []);

    /// <summary>
    ///     An instance whose rate limit refuses the search outright. Unlike a timeline read, there is no half-answer to
    ///     hand back: a search is one call.
    /// </summary>
    public static FakeInstanceSearch RateLimited() =>
        new(
            [],
            [],
            [],
            new RateLimitedException("mastodon.social", new DateTimeOffset(2026, 7, 29, 13, 0, 0, TimeSpan.Zero)));

    public Task<SearchResults> Find(ActiveProfile profile, SearchQuery query, CancellationToken cancellationToken)
    {
        Searches.Add(new Call(profile.Name, query));

        return _refusal is null
            ? Task.FromResult(SearchResults.Matching(query.Kind, _accounts, _hashtags, _posts))
            : Task.FromException<SearchResults>(_refusal);
    }

    /// <summary>One search: which profile it was made as, and what it asked for.</summary>
    internal sealed record Call(string Profile, SearchQuery Query);
}
