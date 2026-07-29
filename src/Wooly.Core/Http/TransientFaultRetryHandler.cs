using Wooly.Core.Errors;

namespace Wooly.Core.Http;

/// <summary>
///     Rides out a flaky connection: a request that never reached the instance is sent again, up to the number of times
///     <see cref="RetryPolicy" /> allows, and only reported once those are spent.
///     <para>
///         Only a failure to reach the instance is retried. An HTTP response, including a 5xx, is handed straight back,
///         because resending a request the instance already accepted could publish a post twice. A cancellation is not
///         retried either: by the time one reaches this handler it is indistinguishable from
///         <see cref="HttpClient.Timeout" /> elapsing, and that budget covers the whole send — a retry would have none
///         of it left (ADR-0006). Rate limiting is a separate concern, handled by <see cref="RateLimitHandler" />.
///     </para>
/// </summary>
internal sealed class TransientFaultRetryHandler(RetryPolicy policy, IRetryDelay delay) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        for (var retries = 0; ; retries++)
        {
            try
            {
                return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
            }
            catch (HttpRequestException exception)
            {
                if (retries == policy.Backoff.Count)
                {
                    throw new TransientNetworkException(request.RequestUri, retries + 1, exception);
                }

                await delay.WaitAsync(policy.Backoff[retries], cancellationToken).ConfigureAwait(false);
            }
        }
    }
}
