using Wooly.Core.Http;
using Wooly.Core.Profiles;

namespace Wooly.Tests.Fakes;

/// <summary>What each instance last said is left of a caller's budget, without an instance to have said it.</summary>
/// <remarks>
///     Keyed on the instance and the token, as the real one is, so that two accounts on one instance are two budgets —
///     and on the token as it is, which a fake can afford and the real one does not hold (#257).
/// </remarks>
internal sealed class FakeRateLimitReport : IRateLimitReport
{
    private readonly Dictionary<(string Instance, string Token), RateLimitQuota> _said = [];

    /// <inheritdoc />
    public RateLimitQuota? For(ActiveProfile profile) =>
        _said.GetValueOrDefault((profile.Instance.ToLowerInvariant(), profile.AccessToken));

    /// <summary>
    ///     A response to a call made to <paramref name="instance" /> with <paramref name="token" />, saying
    ///     <paramref name="remaining" /> calls are left out of <paramref name="limit" /> — as the real one is written by
    ///     every response that goes past, including one landing after the session has stopped acting as its caller.
    /// </summary>
    /// <returns>The quota taken down, for a test to tell it apart from another of the same numbers.</returns>
    public RateLimitQuota Said(string instance, string token, int remaining, int limit = 300)
    {
        var quota = new RateLimitQuota(remaining, limit, new DateTimeOffset(2026, 7, 29, 13, 0, 0, TimeSpan.Zero));

        _said[(instance.ToLowerInvariant(), token)] = quota;

        return quota;
    }

    /// <summary>No instance has been called yet, or none sends budget headers.</summary>
    public static FakeRateLimitReport Silent() => new();
}
