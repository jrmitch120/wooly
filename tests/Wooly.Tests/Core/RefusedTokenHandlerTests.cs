using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using Microsoft.Extensions.DependencyInjection;
using Wooly.Core;
using Wooly.Core.Errors;
using Wooly.Core.Http;
using Wooly.Core.Profiles;
using Wooly.Core.Timelines;
using Wooly.Tests.Fakes;

namespace Wooly.Tests.Core;

/// <summary>
///     An instance answering 401 to a request made with a token is refusing that token — revoked, or expired — which
///     <see cref="AuthenticationException" /> already names, and which a front end can do something about: the TUI
///     says <c>ctrl-p</c>, and the CLI exits with its authentication code (ADR-0006, #248).
/// </summary>
public class RefusedTokenHandlerTests
{
    [Fact]
    public async Task SendAsync_TurnsA401ToATokenIntoAnAuthenticationFailure_InTheInstancesWords()
    {
        var network = new ScriptedHttpMessageHandler(
            ScriptedHttpMessageHandler.Refusal(HttpStatusCode.Unauthorized, "The access token was revoked"));
        var cancellationToken = TestContext.Current.CancellationToken;

        var exception = await Assert.ThrowsAsync<AuthenticationException>(
            () => Send(network, bearer: "token-abc", cancellationToken));

        Assert.Equal("mastodon.social refused the access token: The access token was revoked", exception.Message);
    }

    [Fact]
    public async Task SendAsync_SaysSoEvenWhereTheInstanceGaveNoReason()
    {
        var network = new ScriptedHttpMessageHandler(ScriptedHttpMessageHandler.Status(HttpStatusCode.Unauthorized));
        var cancellationToken = TestContext.Current.CancellationToken;

        var exception = await Assert.ThrowsAsync<AuthenticationException>(
            () => Send(network, bearer: "token-abc", cancellationToken));

        Assert.Equal("mastodon.social refused the access token.", exception.Message);
    }

    /// <summary>
    ///     A request with no token to refuse — registering this client, or trading a code for a token — is left to the
    ///     caller, whose own words for what went wrong say more than "refused the access token" would.
    /// </summary>
    [Fact]
    public async Task SendAsync_LeavesA401WithNoTokenOnItToTheCaller()
    {
        var network = new ScriptedHttpMessageHandler(ScriptedHttpMessageHandler.Status(HttpStatusCode.Unauthorized));

        var response = await Send(network, bearer: null, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task SendAsync_HandsBackAnythingElseUntouched()
    {
        var network = new ScriptedHttpMessageHandler(ScriptedHttpMessageHandler.Status(HttpStatusCode.Forbidden));

        var response = await Send(network, bearer: "token-abc", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>
    ///     Through the pipeline the app builds, so that every port reading as a profile — here a timeline — reports a
    ///     refused token as one, rather than as whatever its HTTP library calls a 401.
    /// </summary>
    [Fact]
    public async Task EveryPortReadingAsAProfile_ReportsARefusedTokenAsOne()
    {
        var network = new ScriptedHttpMessageHandler(
            ScriptedHttpMessageHandler.Refusal(HttpStatusCode.Unauthorized, "The access token was revoked"));
        var services = new ServiceCollection();
        services.AddWoolyCore();
        services.AddHttpClient(WoolyClient.HttpClientName).ConfigurePrimaryHttpMessageHandler(() => network);
        var reader = services.BuildServiceProvider().GetRequiredService<ITimelineReader>();
        var profile = new ActiveProfile
        {
            Name = "personal",
            Instance = "mastodon.social",
            Account = "jeff@mastodon.social",
            AccessToken = "token-abc",
        };

        var exception = await Assert.ThrowsAsync<AuthenticationException>(
            () => reader.Read(profile, Timeline.Home, 40, TestContext.Current.CancellationToken));

        Assert.Contains("The access token was revoked", exception.Message);
    }

    private static async Task<HttpResponseMessage> Send(
        HttpMessageHandler network,
        string? bearer,
        CancellationToken cancellationToken)
    {
        var handler = new RefusedTokenHandler { InnerHandler = network };

        using var client = new HttpClient(handler);
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://mastodon.social/api/v1/timelines/home");

        if (bearer is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
        }

        return await client.SendAsync(request, cancellationToken);
    }
}
