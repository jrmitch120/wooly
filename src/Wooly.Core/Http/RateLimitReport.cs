using System.Globalization;

namespace Wooly.Core.Http;

/// <summary>
///     Holds the last budget an instance reported, written by <see cref="RateLimitHandler" /> as responses go past and
///     read by whoever is drawing it.
/// </summary>
/// <remarks>
///     The field is volatile because the two sides are on different threads: a response lands on whichever thread the
///     HTTP stack finished on, and the TUI reads this while drawing. A reference assignment is atomic, so a reader sees
///     either the previous quota or the new one and never half of one — which is all this has to promise, since a
///     budget one call out of date is a budget.
/// </remarks>
internal sealed class RateLimitReport : IRateLimitReport
{
    /// <summary>How many calls an instance says are left in the current window.</summary>
    private const string RemainingHeader = "X-RateLimit-Remaining";

    /// <summary>How many it allows in a window.</summary>
    private const string LimitHeader = "X-RateLimit-Limit";

    private volatile RateLimitQuota? _latest;

    /// <inheritdoc />
    public RateLimitQuota? Latest => _latest;

    /// <summary>
    ///     Takes down whatever <paramref name="response" /> said about the budget, and leaves the last report standing
    ///     where it said nothing.
    /// </summary>
    /// <remarks>
    ///     Both numbers are wanted or neither is taken: a remaining count with no limit to read it against cannot be
    ///     drawn as a proportion, and a limit with no remaining says only what the instance allows in general.
    /// </remarks>
    /// <param name="resetsAt">When the window rolls over, as the handler read it off the same response.</param>
    public void Observed(HttpResponseMessage response, DateTimeOffset? resetsAt)
    {
        if (Number(response, RemainingHeader) is { } remaining && Number(response, LimitHeader) is { } limit)
        {
            _latest = new RateLimitQuota(remaining, limit, resetsAt);
        }
    }

    private static int? Number(HttpResponseMessage response, string header) =>
        response.Headers.TryGetValues(header, out var values) &&
        int.TryParse(values.FirstOrDefault(), CultureInfo.InvariantCulture, out var number)
            ? number
            : null;
}
