using System.Net.Security;
using System.Net.Sockets;
using Microsoft.Extensions.DependencyInjection;
using Wooly.Core;
using Wooly.Core.Posts;
using Wooly.Core.Profiles;

namespace Wooly.Tests.Integration;

/// <summary>
///     Where the live suite (ADR-0005, #33) gets the instance and token <c>tests/integration/seed.sh</c> minted for
///     it, and the one place a real <see cref="System.Net.Http.HttpClient" /> is built for these tests rather than
///     faked — every other test in this project fakes at <see cref="IMastodonClientFactory" /> or
///     <see cref="System.Net.Http.HttpMessageHandler" /> per ADR-0005, but there is nothing left to fake once the
///     point is catching drift against a real instance.
/// </summary>
internal static class LiveInstance
{
    private const string InstanceVariable = "WOOLY_INTEGRATION_INSTANCE";
    private const string TokenVariable = "WOOLY_INTEGRATION_TOKEN";

    /// <summary>The account <c>tests/integration/seed.sh</c> creates and mints <see cref="Profile" />'s token for.</summary>
    public const string Username = "woolytester";

    /// <summary>Why a <c>[Fact]</c> guarded by <see cref="Available" /> was skipped — xunit v3 requires a reason.</summary>
    public const string SkipReason =
        "Requires a live Mastodon instance; run tests/integration/seed.sh and export its output first.";

    /// <summary>
    ///     Whether the live instance's coordinates were handed down through the environment. Referenced by name from
    ///     every integration <c>[Fact]</c>'s <c>SkipUnless</c> so a run with neither the suite filtered out nor the
    ///     instance seeded fails with one clear reason instead of one connection refusal per test.
    /// </summary>
    public static bool Available =>
        !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(InstanceVariable)) &&
        !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(TokenVariable));

    /// <summary>The seeded test account, ready to call the live instance with.</summary>
    public static ActiveProfile Profile => new()
    {
        Name = "integration",
        Instance = Environment.GetEnvironmentVariable(InstanceVariable)
                   ?? throw new InvalidOperationException($"{InstanceVariable} is not set."),
        Account = null,
        AccessToken = Environment.GetEnvironmentVariable(TokenVariable)
                      ?? throw new InvalidOperationException($"{TokenVariable} is not set."),
    };

    /// <summary>
    ///     The application's own container, wired the same way <c>AddWoolyCore</c> wires it for either front end,
    ///     except for the one thing a live run needs that a fake never did: a certificate check that trusts the
    ///     self-signed certificate <c>tests/integration/Caddyfile</c> terminates TLS with (see its own remarks on why
    ///     TLS is there at all).
    /// </summary>
    public static IServiceProvider NewServices()
    {
        var services = new ServiceCollection();
        services.AddWoolyCore();
        services.AddHttpClient(WoolyClient.HttpClientName).ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
        {
            SslOptions = new SslClientAuthenticationOptions
            {
                RemoteCertificateValidationCallback = (_, _, _, _) => true,
            },
        });

        return services.BuildServiceProvider();
    }

    /// <summary>
    ///     A draft worth nothing but a place to attach a mark or read back on a timeline — every test that needs a
    ///     post of its own to act on wants exactly this and nothing more specific, which is what keeps it here rather
    ///     than typed out again at each call site.
    /// </summary>
    public static PostDraft ThrowawayDraft() => new() { Text = $"wooly integration test {Guid.NewGuid():N}" };
}
