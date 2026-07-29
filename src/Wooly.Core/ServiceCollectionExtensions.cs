using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Wooly.Core.Configuration;
using Wooly.Core.Credentials;
using Wooly.Core.Http;
using Wooly.Core.Profiles;

namespace Wooly.Core;

/// <summary>Registers the core layer that both front ends — the CLI and the TUI — are built on.</summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    ///     Adds the shared API/auth/config layer: the named <see cref="HttpClient" /> every Mastodon call goes through,
    ///     the factory that builds Mastonet clients over it, this client's identity, the two stores it persists
    ///     through — non-secret config, and access tokens — and the profile registry that spans them.
    /// </summary>
    public static IServiceCollection AddWoolyCore(this IServiceCollection services)
    {
        services.AddSingleton(RetryPolicy.Default);
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IRetryDelay, TaskRetryDelay>();
        services.AddTransient<TransientFaultRetryHandler>();
        services.AddTransient<RateLimitHandler>();

        // Retry sits outermost so that a rate limit — raised by the inner handler — is never mistaken for a fault
        // worth retrying, and so a retried request is re-checked against the limit on every attempt.
        services.AddHttpClient(WoolyClient.HttpClientName)
                .AddHttpMessageHandler<TransientFaultRetryHandler>()
                .AddHttpMessageHandler<RateLimitHandler>();

        services.AddSingleton<IMastodonClientFactory, MastodonClientFactory>();

        services.AddSingleton(WoolyPaths.ForCurrentUser());
        services.AddSingleton<IConfigStore, TomlConfigStore>();

        // Which store this resolves to depends on the machine, and is settled on first use rather than here —
        // opening a keyring can prompt or block, and a command that never needs a token should never pay for it.
        services.AddSingleton<ICredentialStore>(provider => new FallbackCredentialStore(
            OsKeyringCredentialStore.Open,
            new PlaintextFileCredentialStore(provider.GetRequiredService<WoolyPaths>())));

        // Sits above both stores. Commands ask this rather than the stores, so that a profile's config half and its
        // token half are never written one without the other.
        services.AddSingleton<IProfileRegistry, ProfileRegistry>();
        services.AddSingleton<IAccessTokenVerifier, AccessTokenVerifier>();

        // The version users see must be the front end's own — reading this assembly instead would report the core
        // library's version, which is only right for as long as every project happens to share one version number.
        var versionSource = Assembly.GetEntryAssembly() ?? typeof(AssemblyClientInfo).Assembly;
        services.AddSingleton<IClientInfo>(new AssemblyClientInfo(versionSource));

        return services;
    }
}
