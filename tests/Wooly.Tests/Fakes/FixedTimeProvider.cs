namespace Wooly.Tests.Fakes;

/// <summary>A clock frozen at a known instant, so relative deadlines land on an exact expected value.</summary>
internal sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}
