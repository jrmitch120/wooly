using Wooly.Core.Errors;
using Wooly.Core.Posts;
using Wooly.Core.Profiles;
using Wooly.Core.Search;
using Wooly.Core.Trends;

namespace Wooly.Tests.Fakes;

/// <summary>
///     An instance's trends without the instance — ADR-0005's primary seam, beside <see cref="FakeInstanceSearch" />:
///     a test says what is trending and then asks what was read, and never fakes HTTP to do it.
///     <para>
///         It pages what it holds as the instance does — passing over the offset and serving the limit, or the
///         instance's own default size where none was asked — so a test of paging sees what a reader would.
///     </para>
/// </summary>
internal sealed class FakeTrends : ITrends
{
    private readonly IReadOnlyList<Hashtag> _hashtags;
    private readonly IReadOnlyList<Post> _posts;
    private readonly IReadOnlyList<TrendingLink> _links;
    private readonly WoolyException? _refusal;

    private FakeTrends(
        IReadOnlyList<Hashtag> hashtags,
        IReadOnlyList<Post> posts,
        IReadOnlyList<TrendingLink> links,
        WoolyException? refusal = null)
    {
        _hashtags = hashtags;
        _posts = posts;
        _links = links;
        _refusal = refusal;
    }

    /// <summary>The access token every read was made with, in order — where a test proves who it was made as.</summary>
    public List<string> Tokens { get; } = [];

    /// <summary>Every read it was asked for, in order — where a test proves which kinds and pages were asked.</summary>
    public List<Call> Reads { get; } = [];

    /// <summary>An instance with all three kinds trending: one of each unless a test says otherwise.</summary>
    public static FakeTrends Trending(
        Hashtag[]? hashtags = null,
        Post[]? posts = null,
        TrendingLink[]? links = null) =>
        new(hashtags ?? [AHashtag.With()], posts ?? [APost.With()], links ?? [ATrendingLink.With()]);

    /// <summary>An instance with nothing trending — one that keeps no trends answers exactly so.</summary>
    public static FakeTrends TrendingNothing() => new([], [], []);

    /// <summary>An instance whose rate limit refuses every read outright, a trends read being one call.</summary>
    public static FakeTrends RateLimited() =>
        new(
            [],
            [],
            [],
            new RateLimitedException("mastodon.social", new DateTimeOffset(2026, 7, 29, 13, 0, 0, TimeSpan.Zero)));

    /// <summary>An instance that turns every read of its trends down, saying <paramref name="reason" />.</summary>
    public static FakeTrends Refusing(string reason = "This method requires an authenticated user") =>
        new([], [], [], TrendsRefusedException.For(reason));

    public Task<IReadOnlyList<Hashtag>> Hashtags(
        ActiveProfile profile,
        TrendPage page,
        CancellationToken cancellationToken) =>
        Answer(profile, TrendKind.Hashtags, page, _hashtags, instanceDefault: 10);

    public Task<IReadOnlyList<Post>> Posts(ActiveProfile profile, TrendPage page, CancellationToken cancellationToken) =>
        Answer(profile, TrendKind.Posts, page, _posts, instanceDefault: 20);

    public Task<IReadOnlyList<TrendingLink>> Links(
        ActiveProfile profile,
        TrendPage page,
        CancellationToken cancellationToken) =>
        Answer(profile, TrendKind.Links, page, _links, instanceDefault: 10);

    private Task<IReadOnlyList<T>> Answer<T>(
        ActiveProfile profile,
        TrendKind kind,
        TrendPage page,
        IReadOnlyList<T> held,
        int instanceDefault)
    {
        Reads.Add(new Call(profile.Name, kind, page));
        Tokens.Add(profile.AccessToken);

        return _refusal is null
            ? Task.FromResult<IReadOnlyList<T>>(held.Skip(page.Offset).Take(page.Limit ?? instanceDefault).ToList())
            : Task.FromException<IReadOnlyList<T>>(_refusal);
    }

    /// <summary>One read: which profile it was made as, which kind, and which page.</summary>
    internal sealed record Call(string Profile, TrendKind Kind, TrendPage Page);
}
