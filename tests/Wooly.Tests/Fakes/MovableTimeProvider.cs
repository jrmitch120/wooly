namespace Wooly.Tests.Fakes;

/// <summary>
///     A clock a test moves by hand. What <see cref="FixedTimeProvider" /> is for a deadline, this is for anything
///     that ages: a cached destination going stale, a countdown running down.
/// </summary>
internal sealed class MovableTimeProvider(DateTimeOffset now) : TimeProvider
{
    private DateTimeOffset _now = now;

    public override DateTimeOffset GetUtcNow() => _now;

    /// <summary>Moves the clock on by <paramref name="howLong" />.</summary>
    public void Advance(TimeSpan howLong) => _now += howLong;
}
