using Wooly.Core.Http;

namespace Wooly.Tests.Fakes;

/// <summary>What an instance last said is left of the budget, without an instance to have said it.</summary>
internal sealed class FakeRateLimitReport(RateLimitQuota? latest = null) : IRateLimitReport
{
    /// <inheritdoc />
    public RateLimitQuota? Latest { get; } = latest;

    /// <summary>A report of <paramref name="remaining" /> calls left out of <paramref name="limit" />.</summary>
    public static FakeRateLimitReport Of(int remaining, int limit = 300) =>
        new(new RateLimitQuota(remaining, limit, new DateTimeOffset(2026, 7, 29, 13, 0, 0, TimeSpan.Zero)));

    /// <summary>An instance that has not been called yet, or one that sends no budget headers.</summary>
    public static FakeRateLimitReport Silent() => new();
}
