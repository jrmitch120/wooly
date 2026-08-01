using System.Globalization;
using System.Net;
using Wooly.Core.Errors;

namespace Wooly.Core.Http;

/// <summary>
///     Turns an instance's "too many requests" into a <see cref="RateLimitedException" /> the moment it arrives, so no
///     caller can mistake a rate limit for an ordinary error response or quietly sit waiting on one.
///     <para>
///         Nothing waits here. The CLI reports the failure and exits (ADR-0006); the TUI reads
///         <see cref="RateLimitedException.ResetsAt" /> and runs its own visible countdown. Putting a wait in the
///         handler would take that choice away from both.
///     </para>
/// </summary>
internal sealed class RateLimitHandler(TimeProvider timeProvider, RateLimitReport report) : DelegatingHandler
{
    /// <summary>Mastodon reports the moment a limit lifts here, as an ISO 8601 timestamp.</summary>
    private const string RateLimitResetHeader = "X-RateLimit-Reset";

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var resetsAt = ReadResetsAt(response);

        // Every response, not only the refusal. An instance says what is left on all of them, and a client that only
        // looked at the one that said "none" could show a reader nothing until the moment it was too late (story 54).
        report.Observed(response, resetsAt);

        if (response.StatusCode != HttpStatusCode.TooManyRequests)
        {
            return response;
        }

        var instance = request.RequestUri?.Host ?? "the instance";

        response.Dispose();

        throw new RateLimitedException(instance, resetsAt);
    }

    private DateTimeOffset? ReadResetsAt(HttpResponseMessage response)
    {
        if (response.Headers.TryGetValues(RateLimitResetHeader, out var resets) &&
            DateTimeOffset.TryParse(
                resets.FirstOrDefault(),
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out var resetsAt))
        {
            return resetsAt;
        }

        // Not something Mastodon itself sends, but a proxy in front of it may, and it costs one branch to honour.
        return response.Headers.RetryAfter switch
        {
            { Date: { } date } => date,
            { Delta: { } delta } => timeProvider.GetUtcNow() + delta,
            _ => null,
        };
    }
}
