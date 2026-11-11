using Microsoft.Extensions.DependencyInjection;
using Wooly.Core.Trends;

namespace Wooly.Tests.Integration;

/// <summary>
///     Runs <see cref="InstanceTrends" /> against the live instance, for the one claim about trends a fake cannot settle:
///     that a real Mastodon with nothing trending answers all three endpoints in a shape this adapter reads as nothing
///     trending, rather than as a refusal or a body it cannot parse (#601).
///     <para>
///         Nothing on the throwaway instance trends — a trend needs activity across accounts and a moderator's approval
///         — so a populated answer is not testable here; the payloads are covered at the handler seam instead. Read-only,
///         so the suite's delete throttle is untouched.
///     </para>
/// </summary>
[Trait("Category", "Integration")]
[Collection(LiveInstanceCollection.Name)]
public class TrendsIntegrationTests
{
    [Fact(Skip = LiveInstance.SkipReason, SkipType = typeof(LiveInstance), SkipUnless = nameof(LiveInstance.Available))]
    public async Task EachKind_ReadsAFreshInstanceAsNothingTrending()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var trends = LiveInstance.NewServices().GetRequiredService<ITrends>();
        var profile = LiveInstance.Profile;

        // A page asked for in full, limit and offset both, so the instance is sent the query string this adapter
        // builds rather than only the bare path a default read sends.
        var page = TrendPage.Of(5, offset: 5);

        Assert.Empty(await trends.Hashtags(profile, TrendPage.First, cancellationToken));
        Assert.Empty(await trends.Hashtags(profile, page, cancellationToken));
        Assert.Empty(await trends.Posts(profile, page, cancellationToken));
        Assert.Empty(await trends.Links(profile, page, cancellationToken));
    }
}
