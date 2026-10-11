namespace Wooly.Tests.Fakes;

/// <summary>
///     A clock a test moves by hand. What <see cref="FixedTimeProvider" /> is for a deadline, this is for anything
///     that ages: a cached destination going stale, a countdown running down — and anything set to go off on it, which
///     goes off as the clock is moved past it rather than after any real waiting (#398).
/// </summary>
internal sealed class MovableTimeProvider(DateTimeOffset now) : TimeProvider
{
    private readonly List<Timer> _timers = [];
    private DateTimeOffset _now = now;

    public override DateTimeOffset GetUtcNow() => _now;

    /// <summary>
    ///     Moves the clock on by <paramref name="howLong" />, setting off every timer that falls due on the way, in the
    ///     order they fall due and each as many times as it falls due.
    /// </summary>
    public void Advance(TimeSpan howLong)
    {
        var until = _now + howLong;

        while (_timers.Where(timer => timer.Due is { } due && due <= until).MinBy(timer => timer.Due) is { } next)
        {
            _now = next.Due!.Value;
            next.GoOff();
        }

        _now = until;
    }

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        var timer = new Timer(this, callback, state);

        timer.Change(dueTime, period);
        _timers.Add(timer);

        return timer;
    }

    /// <summary>A timer set on this clock, which goes off only as the clock is moved past it.</summary>
    private sealed class Timer(MovableTimeProvider clock, TimerCallback callback, object? state) : ITimer
    {
        private TimeSpan _period = Timeout.InfiniteTimeSpan;

        /// <summary>When it next goes off, or <see langword="null" /> where it is not set to.</summary>
        public DateTimeOffset? Due { get; private set; }

        public bool Change(TimeSpan dueTime, TimeSpan period)
        {
            Due = dueTime == Timeout.InfiniteTimeSpan ? null : clock._now + dueTime;
            _period = period;

            return true;
        }

        public void GoOff()
        {
            Due = _period == Timeout.InfiniteTimeSpan || _period == TimeSpan.Zero ? null : Due + _period;
            callback(state);
        }

        public void Dispose()
        {
            Due = null;
            clock._timers.Remove(this);
        }

        public ValueTask DisposeAsync()
        {
            Dispose();

            return ValueTask.CompletedTask;
        }
    }
}
