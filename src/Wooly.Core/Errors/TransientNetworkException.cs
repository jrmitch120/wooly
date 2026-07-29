namespace Wooly.Core.Errors;

/// <summary>
///     A call failed to reach the instance and was still failing after every retry the policy allowed. Distinct from a
///     bare <see cref="HttpRequestException" /> precisely because the retries are already spent: nothing is gained by
///     the caller trying again immediately.
/// </summary>
public sealed class TransientNetworkException(Uri? requestUri, int attempts, Exception innerException)
    : WoolyException(BuildMessage(requestUri, attempts, innerException), innerException)
{
    /// <summary>How many times the request was sent in total, including the first, non-retry attempt.</summary>
    public int Attempts { get; } = attempts;

    private static string BuildMessage(Uri? requestUri, int attempts, Exception innerException)
    {
        var target = requestUri is null ? "the instance" : requestUri.Host;
        var tries = attempts == 1 ? "1 attempt" : $"{attempts} attempts";

        return $"Could not reach {target} after {tries}: {innerException.Message}";
    }
}
