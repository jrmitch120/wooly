namespace Wooly.Core.Http;

/// <summary>
///     Waits out a retry's backoff. Exists as a seam only so tests can observe the backoff a policy asks for without
///     paying it in real wall-clock time.
/// </summary>
public interface IRetryDelay
{
    /// <summary>Waits <paramref name="duration" />, or until <paramref name="cancellationToken" /> is cancelled.</summary>
    Task WaitAsync(TimeSpan duration, CancellationToken cancellationToken);
}
