using System.Net;
using System.Net.Http;
using Microsoft.Extensions.DependencyInjection;
using Wooly.Core;
using Wooly.Core.Errors;
using Wooly.Core.Profiles;
using Wooly.Tests.Fakes;

namespace Wooly.Tests.Core;

/// <summary>
///     The verifier is the thin adapter behind <see cref="IAccessTokenVerifier" />: it makes one Mastonet call and
///     turns the answer into a domain value. ADR-0005 puts such an adapter's tests at the
///     <see cref="HttpMessageHandler" /> seam, because the mapping being tested here — the account Mastonet
///     deserialized becoming <c>username@instance</c> — is only observable once there is a payload to map. Commands
///     above this fake <see cref="IAccessTokenVerifier" /> instead, and must not fake HTTP.
/// </summary>
public class AccessTokenVerifierTests
{
    private const string AccountJson = """{"id":"1","username":"jeff","acct":"jeff"}""";

    [Fact]
    public async Task VerifyAccount_ReportsTheAccountAPastedTokenSignsInAs()
    {
        var verifier = NewVerifier(new ScriptedHttpMessageHandler(ScriptedHttpMessageHandler.Json(AccountJson)));

        Assert.Equal("jeff@mastodon.social", await verifier.VerifyAccount("mastodon.social", "token-abc"));
    }

    [Fact]
    public async Task VerifyAccount_AsksTheInstanceAboutTheTokenItWasHanded()
    {
        var network = new ScriptedHttpMessageHandler(ScriptedHttpMessageHandler.Json(AccountJson));

        await NewVerifier(network).VerifyAccount("mastodon.social", "token-abc");

        var request = Assert.Single(network.Requests);
        Assert.Equal("https://mastodon.social/api/v1/accounts/verify_credentials", request.RequestUri?.ToString());
        Assert.Equal("Bearer token-abc", request.Headers.Authorization?.ToString());
    }

    /// <summary>
    ///     A token typed with a character missing is the likeliest way this ends, and the instance's own wording for
    ///     it beats anything this client could invent.
    /// </summary>
    [Fact]
    public async Task VerifyAccount_ReportsATokenTheInstanceWillNotAccept()
    {
        var network = new ScriptedHttpMessageHandler(
            ScriptedHttpMessageHandler.Refusal(HttpStatusCode.Unauthorized, "The access token is invalid"));

        var exception = await Assert.ThrowsAsync<AuthenticationException>(
            () => NewVerifier(network).VerifyAccount("mastodon.social", "token-abc"));

        Assert.Contains("mastodon.social", exception.Message);
        Assert.Contains("The access token is invalid", exception.Message);
    }

    /// <summary>Resolved from the container the app builds, so the wiring is under test alongside the behavior.</summary>
    private static IAccessTokenVerifier NewVerifier(HttpMessageHandler network)
    {
        var services = new ServiceCollection();
        services.AddWoolyCore();
        services.AddHttpClient(WoolyClient.HttpClientName).ConfigurePrimaryHttpMessageHandler(() => network);

        return services.BuildServiceProvider().GetRequiredService<IAccessTokenVerifier>();
    }
}
