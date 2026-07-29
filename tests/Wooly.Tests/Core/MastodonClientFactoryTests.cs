using System.Net;
using System.Net.Http;
using Microsoft.Extensions.DependencyInjection;
using Wooly.Core;

namespace Wooly.Tests.Core;

public class MastodonClientFactoryTests
{
    [Fact]
    public void CreateClient_BindsTheClientToTheRequestedInstance()
    {
        var client = BuildFactory().CreateClient("mastodon.social", "token-abc");

        Assert.Equal("mastodon.social", client.Instance);
    }

    [Fact]
    public void CreateAuthenticationClient_BindsTheClientToTheRequestedInstance()
    {
        var client = BuildFactory().CreateAuthenticationClient("hachyderm.io");

        Assert.Equal("hachyderm.io", client.Instance);
    }

    /// <summary>
    ///     ADR-0005 reserves <see cref="HttpMessageHandler" /> fakes for deserialization edge cases, so this is a
    ///     deliberate one-off: the factory sits below the <c>IMastodonClient</c> seam, and the only way to show that
    ///     the clients it builds really run over the injected <see cref="HttpClient" /> is to watch a request arrive.
    /// </summary>
    [Fact]
    public async Task CreateClient_SendsRequestsThroughTheInjectedHttpClient()
    {
        var handler = new StubHttpMessageHandler("{\"id\":\"1\",\"username\":\"wooly\",\"acct\":\"wooly\"}");

        var client = BuildFactory(handler).CreateClient("mastodon.social", "token-abc");
        await client.GetCurrentUser();

        var request = Assert.Single(handler.Requests);
        Assert.Equal("https://mastodon.social/api/v1/accounts/verify_credentials", request.RequestUri?.ToString());
        Assert.Equal("Bearer token-abc", request.Headers.Authorization?.ToString());
    }

    /// <summary>
    ///     Builds the factory the way the app does — through <see cref="ServiceCollectionExtensions.AddWoolyCore" /> —
    ///     so the test exercises the real DI wiring, optionally with the primary HTTP handler swapped out.
    /// </summary>
    private static IMastodonClientFactory BuildFactory(HttpMessageHandler? handler = null)
    {
        var services = new ServiceCollection();
        services.AddWoolyCore();

        if (handler is not null)
        {
            services.AddHttpClient(WoolyClient.HttpClientName).ConfigurePrimaryHttpMessageHandler(() => handler);
        }

        return services.BuildServiceProvider().GetRequiredService<IMastodonClientFactory>();
    }

    private sealed class StubHttpMessageHandler(string responseJson) : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseJson, System.Text.Encoding.UTF8, "application/json"),
            });
        }
    }
}
