using Wooly.Core.Posts;
using Wooly.Core.Profiles;
using Wooly.Core.Search;

namespace Wooly.Core.Trends;

/// <summary>
///     Reads what an instance ranks as trending — its <b>Trend</b>s, in GLOSSARY.md's word: hashtags, posts and
///     trending links, each a page at a time. The narrow port ADR-0005 asks for, beside
///     <see cref="IInstanceSearch" />; front ends depend on this, and their tests fake this rather than the network.
///     It is a port of its own over <see cref="HttpClient" /> rather than Mastonet, for the reasons ADR-0036 gives.
/// </summary>
/// <remarks>
///     Each read is one call, so — as with a search (ADR-0011) — a rate limit stops it with nothing in hand and is
///     raised rather than reported alongside a half-answer. An instance with trends turned off answers with nothing,
///     which is read as nothing trending rather than as a failure.
/// </remarks>
public interface ITrends
{
    /// <summary>
    ///     The hashtags <paramref name="profile" />'s instance ranks as trending, most talked about first. A trending
    ///     hashtag is a hashtag, carrying the usage a searched one carries.
    /// </summary>
    /// <exception cref="Errors.RateLimitedException">The profile has spent its quota with the instance.</exception>
    /// <exception cref="Errors.TrendsRefusedException">The instance turned the read down.</exception>
    /// <exception cref="Errors.InstanceFailedException">The instance failed to answer — a <c>5xx</c>.</exception>
    Task<IReadOnlyList<Hashtag>> Hashtags(ActiveProfile profile, TrendPage page, CancellationToken cancellationToken);

    /// <summary>
    ///     The posts <paramref name="profile" />'s instance ranks as trending, most talked about first. A trending post
    ///     is a post, read as <paramref name="profile" /> sees it.
    /// </summary>
    /// <exception cref="Errors.RateLimitedException">The profile has spent its quota with the instance.</exception>
    /// <exception cref="Errors.TrendsRefusedException">The instance turned the read down.</exception>
    /// <exception cref="Errors.InstanceFailedException">The instance failed to answer — a <c>5xx</c>.</exception>
    Task<IReadOnlyList<Post>> Posts(ActiveProfile profile, TrendPage page, CancellationToken cancellationToken);

    /// <summary>
    ///     The <b>Trending link</b>s <paramref name="profile" />'s instance ranks as shared now, most shared first.
    /// </summary>
    /// <exception cref="Errors.RateLimitedException">The profile has spent its quota with the instance.</exception>
    /// <exception cref="Errors.TrendsRefusedException">The instance turned the read down.</exception>
    /// <exception cref="Errors.InstanceFailedException">The instance failed to answer — a <c>5xx</c>.</exception>
    Task<IReadOnlyList<TrendingLink>> Links(ActiveProfile profile, TrendPage page, CancellationToken cancellationToken);
}
