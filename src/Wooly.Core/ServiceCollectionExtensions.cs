using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace Wooly.Core;

/// <summary>Registers the core layer that both front ends — the CLI and the TUI — are built on.</summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    ///     Adds the shared API/auth layer: the named <see cref="HttpClient" /> every Mastodon call goes through, the
    ///     factory that builds Mastonet clients over it, and this client's identity.
    /// </summary>
    public static IServiceCollection AddWoolyCore(this IServiceCollection services)
    {
        services.AddHttpClient(WoolyClient.HttpClientName);
        services.AddSingleton<IMastodonClientFactory, MastodonClientFactory>();

        // The version users see must be the front end's own — reading this assembly instead would report the core
        // library's version, which is only right for as long as every project happens to share one version number.
        var versionSource = Assembly.GetEntryAssembly() ?? typeof(AssemblyClientInfo).Assembly;
        services.AddSingleton<IClientInfo>(new AssemblyClientInfo(versionSource));

        return services;
    }
}
