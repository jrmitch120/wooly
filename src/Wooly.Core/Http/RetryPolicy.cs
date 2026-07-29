namespace Wooly.Core.Http;

/// <summary>
///     How hard a transient network failure is retried before it is reported. Expressed as the list of backoff waits
///     rather than a count plus a formula, so "how many retries" and "how long between them" are one readable value.
/// </summary>
public sealed class RetryPolicy
{
    /// <summary>
    ///     Two retries, roughly a quarter of a second then three quarters. Short enough that an interactive command
    ///     still feels immediate, long enough to ride out a momentary blip.
    /// </summary>
    public static RetryPolicy Default { get; } = new(TimeSpan.FromMilliseconds(250), TimeSpan.FromMilliseconds(750));

    /// <summary>Builds a policy that retries once per wait given.</summary>
    /// <param name="backoff">One wait per retry, in order. Pass nothing for a policy that never retries.</param>
    public RetryPolicy(params TimeSpan[] backoff)
    {
        Backoff = backoff;
    }

    /// <summary>The wait before each retry. Its length is the number of retries allowed after the first attempt.</summary>
    public IReadOnlyList<TimeSpan> Backoff { get; }
}
