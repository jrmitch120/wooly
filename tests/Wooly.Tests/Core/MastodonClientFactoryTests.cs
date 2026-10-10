using System.Net.Http;
using Microsoft.Extensions.DependencyInjection;
using Wooly.Core;
using Wooly.Core.Http;
using Wooly.Core.Profiles;
using Wooly.Tests.Fakes;

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
    public void CreateAnonymousClient_BindsTheClientToTheRequestedInstance()
    {
        var client = BuildFactory().CreateAnonymousClient("mastodon.social");

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
        var network = new ScriptedHttpMessageHandler(
            ScriptedHttpMessageHandler.Json("""{"id":"1","username":"wooly","acct":"wooly"}"""));

        var client = BuildFactory(network).CreateClient("mastodon.social", "token-abc");
        await client.GetCurrentUser();

        var request = Assert.Single(network.Requests);
        Assert.Equal("https://mastodon.social/api/v1/accounts/verify_credentials", request.RequestUri?.ToString());
        Assert.Equal("Bearer token-abc", request.Headers.Authorization?.ToString());
    }

    /// <summary>An anonymous client is for endpoints that take no credentials, so it must not send one.</summary>
    [Fact]
    public async Task CreateAnonymousClient_SendsRequestsWithoutAnAccessToken()
    {
        var network = new ScriptedHttpMessageHandler(
            ScriptedHttpMessageHandler.Json("""{"domain":"mastodon.social","version":"4.3.1"}"""));

        var client = BuildFactory(network).CreateAnonymousClient("mastodon.social");
        await client.GetInstanceV2();

        var request = Assert.Single(network.Requests);
        Assert.Equal("https://mastodon.social/api/v2/instance", request.RequestUri?.ToString());
        Assert.Null(request.Headers.Authorization);
    }

    /// <summary>
    ///     A client the factory builds sends its token the way the rate-limit report reads it, so the budget an instance
    ///     reports to it is taken down as the budget of the profile it was built for (#257).
    /// </summary>
    [Fact]
    public async Task CreateClient_ReportsTheBudgetAsTheProfileItWasBuiltFor()
    {
        var network = new ScriptedHttpMessageHandler(request =>
        {
            var response = ScriptedHttpMessageHandler.Json("""{"id":"1","username":"wooly","acct":"wooly"}""")(request);
            response.Headers.Add("X-RateLimit-Remaining", "213");
            response.Headers.Add("X-RateLimit-Limit", "300");

            return response;
        });
        using var services = BuildServices(network);

        await services.GetRequiredService<IMastodonClientFactory>()
                      .CreateClient("mastodon.social", "token-abc")
                      .GetCurrentUser();

        var report = services.GetRequiredService<IRateLimitReport>();
        Assert.Equal(213, report.For(Profile("mastodon.social", "token-abc"))?.Remaining);
        Assert.Null(report.For(Profile("mastodon.social", "token-xyz")));
    }

    /// <summary>A profile on <paramref name="instance" />, calling with <paramref name="token" />.</summary>
    private static ActiveProfile Profile(string instance, string token) => new()
    {
        Name = "wooly",
        Instance = instance,
        Account = null,
        AccessToken = token,
    };

    /// <summary>
    ///     Builds the factory the way the app does — through <see cref="ServiceCollectionExtensions.AddWoolyCore" /> —
    ///     so the test exercises the real DI wiring, optionally with the primary HTTP handler swapped out.
    /// </summary>
    private static IMastodonClientFactory BuildFactory(HttpMessageHandler? handler = null) =>
        BuildServices(handler).GetRequiredService<IMastodonClientFactory>();

    private static ServiceProvider BuildServices(HttpMessageHandler? handler)
    {
        var services = new ServiceCollection();
        services.AddWoolyCore();

        if (handler is not null)
        {
            services.AddHttpClient(WoolyClient.HttpClientName).ConfigurePrimaryHttpMessageHandler(() => handler);
        }

        return services.BuildServiceProvider();
    }
}
