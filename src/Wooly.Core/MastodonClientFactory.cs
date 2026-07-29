using Mastonet;

namespace Wooly.Core;

/// <summary>
///     Builds Mastonet clients over a pooled <see cref="HttpClient" /> from <see cref="IHttpClientFactory" />, so every
///     call the client makes runs through handlers this application controls — the seam used to fake HTTP responses in
///     tests, and the place retry/backoff policies hang once they exist.
/// </summary>
public sealed class MastodonClientFactory(IHttpClientFactory httpClientFactory) : IMastodonClientFactory
{
    /// <inheritdoc />
    public IMastodonClient CreateClient(string instance, string accessToken) =>
        new MastodonClient(instance, accessToken, httpClientFactory.CreateClient(WoolyClient.HttpClientName));

    /// <inheritdoc />
    public IMastodonClient CreateAnonymousClient(string instance) =>
        new MastodonClient(instance, string.Empty, httpClientFactory.CreateClient(WoolyClient.HttpClientName));

    /// <inheritdoc />
    public IAuthenticationClient CreateAuthenticationClient(string instance) =>
        new AuthenticationClient(instance, httpClientFactory.CreateClient(WoolyClient.HttpClientName));
}
