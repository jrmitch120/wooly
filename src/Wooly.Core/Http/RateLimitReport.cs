using System.Collections.Concurrent;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Wooly.Core.Profiles;

namespace Wooly.Core.Http;

/// <summary>
///     Holds the last budget each instance reported for each caller, written by <see cref="RateLimitHandler" /> as
///     responses go past and read by whoever is drawing it.
/// </summary>
/// <remarks>
///     <para>
///         The two sides are on different threads: a response lands on whichever thread the HTTP stack finished on,
///         and the TUI reads this while drawing. A concurrent dictionary of whole quotas means a reader sees either a
///         caller's previous quota or the new one and never half of one — which is all this has to promise, since a
///         budget one call out of date is a budget.
///     </para>
///     <para>
///         A caller is the instance and a hash of the token, never the token itself: this outlives every call, and a
///         token is held only where something is about to call with it.
///     </para>
/// </remarks>
internal sealed class RateLimitReport : IRateLimitReport
{
    /// <summary>How many calls an instance says are left in the current window.</summary>
    private const string RemainingHeader = "X-RateLimit-Remaining";

    /// <summary>How many it allows in a window.</summary>
    private const string LimitHeader = "X-RateLimit-Limit";

    private readonly ConcurrentDictionary<Caller, RateLimitQuota> _latest = new();

    /// <inheritdoc />
    public RateLimitQuota? For(ActiveProfile profile) =>
        _latest.GetValueOrDefault(new Caller(profile.Instance, Digest(profile.AccessToken)));

    /// <summary>
    ///     Takes down whatever <paramref name="response" /> said about the budget of whoever made
    ///     <paramref name="request" />, and leaves their last report standing where it said nothing.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         Both numbers are wanted or neither is taken: a remaining count with no limit to read it against cannot be
    ///         drawn as a proportion, and a limit with no remaining says only what the instance allows in general.
    ///     </para>
    ///     <para>
    ///         A call made with no token — registering this client, trading a code for a token — is nobody's a profile
    ///         could be drawn with, so it is taken down for nobody.
    ///     </para>
    /// </remarks>
    /// <param name="resetsAt">When the window rolls over, as the handler read it off the same response.</param>
    public void Observed(HttpRequestMessage request, HttpResponseMessage response, DateTimeOffset? resetsAt)
    {
        if (CallerOf(request) is { } caller &&
            Number(response, RemainingHeader) is { } remaining &&
            Number(response, LimitHeader) is { } limit)
        {
            _latest[caller] = new RateLimitQuota(remaining, limit, resetsAt);
        }
    }

    private static Caller? CallerOf(HttpRequestMessage request) =>
        request is { RequestUri: { } uri, Headers.Authorization: { Parameter: { Length: > 0 } token } authorization } &&
        authorization.Scheme.Equals("Bearer", StringComparison.OrdinalIgnoreCase)
            ? new Caller(uri.Authority, Digest(token))
            : null;

    private static string Digest(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    private static int? Number(HttpResponseMessage response, string header) =>
        response.Headers.TryGetValues(header, out var values) &&
        int.TryParse(values.FirstOrDefault(), CultureInfo.InvariantCulture, out var number)
            ? number
            : null;

    /// <summary>Whose budget a quota is: the instance, as a host is compared, and the digest of the token called with.</summary>
    private readonly record struct Caller
    {
        public Caller(string instance, string token)
        {
            Instance = instance.ToLowerInvariant();
            Token = token;
        }

        public string Instance { get; }

        public string Token { get; }
    }
}
