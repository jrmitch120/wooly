using Wooly.Core.Http;

namespace Wooly.Tests.Fakes;

/// <summary>Records the backoff a retry asked for instead of waiting it out.</summary>
internal sealed class RecordingRetryDelay : IRetryDelay
{
    /// <summary>Each wait requested, in order.</summary>
    public List<TimeSpan> Waits { get; } = [];

    public Task WaitAsync(TimeSpan duration, CancellationToken cancellationToken)
    {
        Waits.Add(duration);

        return Task.CompletedTask;
    }
}
