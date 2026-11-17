using Wooly.Core.Posts;
using Wooly.Core.Search;
using Wooly.Core.Trends;

namespace Wooly.Cli.Output;

/// <summary>
///     What <c>trends</c> read, one list per kind, where a kind not asked for is <see langword="null" /> and a kind with
///     nothing trending is empty — the distinction ADR-0011 has <c>search</c> keep, held the same way so both writers
///     can keep it too.
/// </summary>
internal sealed record TrendsReading(
    IReadOnlyList<Hashtag>? Hashtags,
    IReadOnlyList<Post>? Posts,
    IReadOnlyList<TrendingLink>? Links)
{
    /// <summary>Whether nothing asked for is trending.</summary>
    public bool IsEmpty => Hashtags is not { Count: > 0 } && Posts is not { Count: > 0 } && Links is not { Count: > 0 };
}
