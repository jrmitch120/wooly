namespace Wooly.Core.Http;

/// <summary>The real wait — the only implementation anything but a test uses.</summary>
internal sealed class TaskRetryDelay : IRetryDelay
{
    /// <inheritdoc />
    public Task WaitAsync(TimeSpan duration, CancellationToken cancellationToken) => Task.Delay(duration, cancellationToken);
}
