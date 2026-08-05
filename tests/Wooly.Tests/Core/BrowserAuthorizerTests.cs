using System.Collections.Specialized;
using System.Net;
using System.Net.Http;
using System.Web;
using Microsoft.Extensions.DependencyInjection;
using Wooly.Core;
using Wooly.Core.Errors;
using Wooly.Core.Profiles;
using Wooly.Tests.Fakes;

namespace Wooly.Tests.Core;

/// <summary>
///     The authorizer is the adapter behind <see cref="IBrowserAuthorizer" />: it registers this client with an
///     instance, sends the user somewhere, and trades what the browser brings back for an access token. ADR-0005 puts
///     an adapter's tests at the <see cref="HttpMessageHandler" /> seam, and both halves of this one are only
///     observable there — the instance's side of the flow is scripted HTTP, and the browser's side is a real request
///     to the loopback address the authorizer is listening on.
/// </summary>
public class BrowserAuthorizerTests
{
    private const string AppJson =
        """
        {"id":"1","name":"wooly","client_id":"client-abc","client_secret":"secret-abc",
         "redirect_uri":"http://127.0.0.1/","scope":"read write follow"}
        """;

    private const string TokenJson =
        """{"access_token":"token-from-browser","token_type":"Bearer","scope":"read write follow","created_at":1}""";

    [Fact]
    public async Task Begin_RegistersThisClientWithTheInstanceBeforeSendingAnyoneToIt()
    {
        var network = AnInstanceThatAuthorizes();

        using var authorization = await Begin(network);

        var registration = network.Requests[0];
        Assert.Equal("https://mastodon.social/api/v1/apps", registration.RequestUri?.ToString());
        Assert.Null(registration.Headers.Authorization);
    }

    /// <summary>
    ///     What is registered here is the client, not the front end doing the registering: this name is what the
    ///     instance shows on the page listing the applications an account has approved, and the same name the OS
    ///     keyring keeps tokens under. Were either front end to send the command it was invoked as instead, the two
    ///     would authorize as different applications and neither could read the other's tokens.
    /// </summary>
    [Fact]
    public async Task Begin_RegistersUnderTheOneNameBothFrontEndsShare()
    {
        var network = AnInstanceThatAuthorizes();

        using var authorization = await Begin(network);

        Assert.Equal("wooly", HttpUtility.ParseQueryString(network.Bodies[0])["client_name"]);
    }

    [Fact]
    public async Task Begin_SendsTheUserToTheInstancesOwnAuthorizationPage()
    {
        using var authorization = await Begin(AnInstanceThatAuthorizes());

        var query = QueryOf(authorization.AuthorizationUrl);

        Assert.Equal("https://mastodon.social/oauth/authorize", UrlWithoutQuery(authorization.AuthorizationUrl));
        Assert.Equal("code", query["response_type"]);
        Assert.Equal("client-abc", query["client_id"]);
        Assert.StartsWith("http://127.0.0.1:", query["redirect_uri"]);
    }

    /// <summary>
    ///     The redirect lands on a plain, unauthenticated address on this machine, which anything running here — or any
    ///     page the browser happens to have open — could reach. The state value is what makes such a redirect
    ///     distinguishable from the real one, so the authorization request has to carry one.
    /// </summary>
    [Fact]
    public async Task Begin_SendsTheRequestOutWithAStateValueToRecognizeTheAnswerBy()
    {
        using var first = await Begin(AnInstanceThatAuthorizes());
        using var second = await Begin(AnInstanceThatAuthorizes());

        var state = QueryOf(first.AuthorizationUrl)["state"];

        Assert.False(string.IsNullOrWhiteSpace(state));
        Assert.NotEqual(state, QueryOf(second.AuthorizationUrl)["state"]);
    }

    [Fact]
    public async Task AwaitAccessToken_TradesTheCodeTheBrowserBringsBackForATokenTheProfileCanUse()
    {
        var network = AnInstanceThatAuthorizes();
        using var authorization = await Begin(network);

        var accessToken = authorization.AwaitAccessToken(TestContext.Current.CancellationToken);
        await SendBrowserBack(authorization, $"code=code-abc&state={StateOf(authorization)}");

        Assert.Equal("token-from-browser", await accessToken);
        Assert.Equal("https://mastodon.social/oauth/token", network.Requests[1].RequestUri?.ToString());
    }

    /// <summary>
    ///     An instance re-checks the redirect URI when the code is exchanged, so the address sent out with the request
    ///     and the address being listened on have to be the same one.
    /// </summary>
    [Fact]
    public async Task AwaitAccessToken_ExchangesTheCodeAgainstTheAddressTheBrowserWasSentBackTo()
    {
        var exchange = new RecordedRequestBody();
        using var authorization = await Begin(AnInstanceThatAuthorizes(exchange));

        var accessToken = authorization.AwaitAccessToken(TestContext.Current.CancellationToken);
        await SendBrowserBack(authorization, $"code=code-abc&state={StateOf(authorization)}");
        await accessToken;

        var submitted = HttpUtility.ParseQueryString(exchange.Body!);
        Assert.Equal("authorization_code", submitted["grant_type"]);
        Assert.Equal("code-abc", submitted["code"]);
        Assert.Equal(QueryOf(authorization.AuthorizationUrl)["redirect_uri"], submitted["redirect_uri"]);
    }

    /// <summary>
    ///     The point of the state value: an authorization this sign-in never asked for is not spent. It does not end
    ///     the sign-in either — the socket is on an address anything running here can reach, so ending on one would
    ///     let any local process stop a sign-in by guessing a port.
    /// </summary>
    [Fact]
    public async Task AwaitAccessToken_IgnoresAnAuthorizationThisSignInNeverAskedForAndKeepsWaiting()
    {
        var network = AnInstanceThatAuthorizes();
        using var authorization = await Begin(network);

        var accessToken = authorization.AwaitAccessToken(TestContext.Current.CancellationToken);
        await SendBrowserBack(authorization, "code=code-planted&state=state-someone-elses");

        Assert.False(accessToken.IsCompleted);

        await SendBrowserBack(authorization, $"code=code-abc&state={StateOf(authorization)}");

        Assert.Equal("token-from-browser", await accessToken);

        // The planted code was never taken to the instance: one call to register, one to exchange, and the exchange
        // was of the genuine code.
        Assert.Equal(2, network.Requests.Count);
    }

    /// <summary>
    ///     A refusal is text an instance chose, and the same address anything on this machine can reach is where it
    ///     arrives — so one arriving without this sign-in's state must not be repeated to the user as though the
    ///     instance had said it.
    /// </summary>
    [Fact]
    public async Task AwaitAccessToken_IsNotEndedByARefusalFromSomethingOtherThanThisSignIn()
    {
        var network = AnInstanceThatAuthorizes();
        using var authorization = await Begin(network);

        var accessToken = authorization.AwaitAccessToken(TestContext.Current.CancellationToken);
        await SendBrowserBack(authorization, "error=access_denied&error_description=Planted+by+something+else");

        Assert.False(accessToken.IsCompleted);

        await SendBrowserBack(authorization, $"code=code-abc&state={StateOf(authorization)}");

        Assert.Equal("token-from-browser", await accessToken);
    }

    [Fact]
    public async Task AwaitAccessToken_ReportsTheUserTurningTheRequestDownAtTheInstance()
    {
        var network = AnInstanceThatAuthorizes();
        using var authorization = await Begin(network);

        var accessToken = authorization.AwaitAccessToken(TestContext.Current.CancellationToken);

        // With the state the request went out with, which is what an instance echoes back on a refusal too.
        await SendBrowserBack(
            authorization,
            $"error=access_denied&error_description=The+request+was+denied&state={StateOf(authorization)}");

        var exception = await Assert.ThrowsAsync<AuthenticationException>(() => accessToken);

        Assert.Contains("The request was denied", exception.Message);
        Assert.Single(network.Requests);
    }

    /// <summary>
    ///     A browser that never comes back is the likeliest way this ends badly — a closed tab, a machine with no
    ///     browser at all. Waiting forever would leave the terminal wedged with no way out but a keystroke the user has
    ///     to think of themselves.
    /// </summary>
    [Fact]
    public async Task AwaitAccessToken_GivesUpOnABrowserThatNeverComesBack()
    {
        var authorizer = new BrowserAuthorizer(Factory(AnInstanceThatAuthorizes()), TimeSpan.FromMilliseconds(50));

        using var authorization = await authorizer.Begin("mastodon.social", TestContext.Current.CancellationToken);

        var exception = await Assert.ThrowsAsync<AuthenticationException>(
            () => authorization.AwaitAccessToken(TestContext.Current.CancellationToken));

        Assert.Contains("mastodon.social", exception.Message);
    }

    [Fact]
    public async Task Begin_ReportsAnInstanceThatWillNotRegisterThisClientAtAll()
    {
        var network = new ScriptedHttpMessageHandler(
            ScriptedHttpMessageHandler.Refusal(HttpStatusCode.Forbidden, "Client registration is disabled"));

        var exception = await Assert.ThrowsAsync<AuthenticationException>(() => Begin(network));

        Assert.Contains("mastodon.social", exception.Message);
        Assert.Contains("Client registration is disabled", exception.Message);
    }

    /// <summary>Resolved from the container the app builds, so the wiring is under test alongside the behavior.</summary>
    private static async Task<IBrowserAuthorization> Begin(HttpMessageHandler network)
    {
        var services = new ServiceCollection();
        services.AddWoolyCore();
        services.AddHttpClient(WoolyClient.HttpClientName).ConfigurePrimaryHttpMessageHandler(() => network);

        var authorizer = services.BuildServiceProvider().GetRequiredService<IBrowserAuthorizer>();

        return await authorizer.Begin("mastodon.social", TestContext.Current.CancellationToken);
    }

    private static IMastodonClientFactory Factory(HttpMessageHandler network)
    {
        var services = new ServiceCollection();
        services.AddWoolyCore();
        services.AddHttpClient(WoolyClient.HttpClientName).ConfigurePrimaryHttpMessageHandler(() => network);

        return services.BuildServiceProvider().GetRequiredService<IMastodonClientFactory>();
    }

    /// <summary>
    ///     An instance that hands out an app registration first and an access token second — the two calls this flow
    ///     makes, told apart by which endpoint they arrive at rather than by their order, so a test that never gets as
    ///     far as the exchange still gets the right answer to the call it does make.
    /// </summary>
    private static ScriptedHttpMessageHandler AnInstanceThatAuthorizes(RecordedRequestBody? exchange = null) =>
        new(request =>
        {
            if (request.RequestUri?.AbsolutePath != "/oauth/token")
            {
                return ScriptedHttpMessageHandler.Json(AppJson)(request);
            }

            exchange?.Record(request);

            return ScriptedHttpMessageHandler.Json(TokenJson)(request);
        });

    /// <summary>
    ///     Stands in for the browser: the instance would send it to the redirect URI this authorization advertised, and
    ///     so does this.
    /// </summary>
    private static async Task SendBrowserBack(IBrowserAuthorization authorization, string query)
    {
        var redirectUri = new Uri(QueryOf(authorization.AuthorizationUrl)["redirect_uri"]!);

        using var browser = new HttpClient();

        await browser.GetAsync(new Uri(redirectUri, $"?{query}"), TestContext.Current.CancellationToken);
    }

    private static string? StateOf(IBrowserAuthorization authorization) =>
        QueryOf(authorization.AuthorizationUrl)["state"];

    private static NameValueCollection QueryOf(Uri url) => HttpUtility.ParseQueryString(url.Query);

    private static string UrlWithoutQuery(Uri url) => url.GetLeftPart(UriPartial.Path);

    /// <summary>
    ///     Keeps a request's body from being read after <see cref="HttpClient" /> has disposed it, which is the state
    ///     the handler's record of a request is in by the time a test looks at it.
    /// </summary>
    private sealed class RecordedRequestBody
    {
        public string? Body { get; private set; }

        public void Record(HttpRequestMessage request) =>
            Body = request.Content?.ReadAsStringAsync(TestContext.Current.CancellationToken).GetAwaiter().GetResult();
    }
}
