using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Wooly.Core.Profiles;

namespace Wooly.Core.Http;

/// <summary>
///     Reaches a Mastodon endpoint Mastonet 3.1.3 does not have, over the very <see cref="HttpClient" /> every Mastonet
///     call already runs through — so a call made this way is retried, rate-limit-checked and reported exactly like one
///     made through the library, and nothing above it can tell which kind it was.
///     <para>
///         ADR-0011 named this as the cheaper of the two ways out of a gap in the library, against carrying a fork or
///         waiting on an upstream release, and it stays cheap only while it is one place: two hand-rolled calls
///         building their own URLs and headers would be two chances to authorize a request differently.
///     </para>
///     Nothing here is caught. A refusal, a rate limit and an unreachable instance come out as they went in, and what
///     to do about one is the calling adapter's decision rather than this helper's.
/// </summary>
internal static class RawMastodonCall
{
    /// <summary>Reads <paramref name="path" /> from the profile's instance and deserializes the body it answers with.</summary>
    /// <param name="path">The endpoint's path below the instance, without a leading slash — e.g. <c>api/v1/…</c>.</param>
    /// <param name="query">
    ///     The query parameters, in the order they should be sent, and repeated where the endpoint takes a list:
    ///     Mastodon spells "several ids" as several <c>id[]</c> parameters rather than as one delimited value.
    /// </param>
    /// <returns>The body, or <see langword="null" /> where the instance answered with a bare JSON <c>null</c>.</returns>
    /// <exception cref="Errors.RateLimitedException">The profile has spent its quota with the instance.</exception>
    /// <exception cref="Errors.TransientNetworkException">The instance could not be reached, retries included.</exception>
    /// <exception cref="HttpRequestException">The instance answered, and what it answered was a refusal.</exception>
    /// <exception cref="JsonException">The instance answered with a body that is not what the endpoint documents.</exception>
    public static async Task<T?> Get<T>(
        IHttpClientFactory httpClientFactory,
        ActiveProfile profile,
        string path,
        IReadOnlyList<KeyValuePair<string, string>> query,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            new Uri($"https://{profile.Instance}/{path}{QueryString(query)}"));

        // Said here rather than on the client, because the client is pooled by name across every profile a process
        // touches and a token left on it would outlive the call that brought it.
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", profile.AccessToken);

        var client = httpClientFactory.CreateClient(WoolyClient.HttpClientName);

        using var response = await client.SendAsync(request, cancellationToken);

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<T>(cancellationToken);
    }

    /// <summary>
    ///     The parameters as an instance takes them. Only the values are escaped: a name is this client's own literal,
    ///     and escaping the brackets in <c>id[]</c> would send a parameter Mastodon does not answer to — which is the
    ///     same unescaped form Mastonet's own <c>/accounts/relationships</c> call is sent in.
    /// </summary>
    private static string QueryString(IReadOnlyList<KeyValuePair<string, string>> query) =>
        query.Count == 0
            ? string.Empty
            : $"?{string.Join("&", query.Select(parameter => $"{parameter.Key}={Uri.EscapeDataString(parameter.Value)}"))}";
}
