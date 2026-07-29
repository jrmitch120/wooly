using Mastonet;

namespace Wooly.Core;

/// <summary>
///     Creates Mastonet's API clients, which are bound to one instance (and, for authenticated calls, one access
///     token) at construction. Because a profile — and therefore the instance and token — is only known per command
///     invocation, the clients cannot be registered directly in the container; this factory is what gets injected
///     instead. It is also the seam almost every test fakes (ADR-0005).
/// </summary>
public interface IMastodonClientFactory
{
    /// <summary>Creates an authenticated client for <paramref name="instance" />.</summary>
    /// <param name="instance">The instance's domain, e.g. <c>mastodon.social</c>.</param>
    /// <param name="accessToken">The access token for the profile making the call.</param>
    IMastodonClient CreateClient(string instance, string accessToken);

    /// <summary>
    ///     Creates a client for the endpoints an instance serves to anyone — the ones reachable before a profile
    ///     exists, such as the instance's own metadata.
    /// </summary>
    /// <param name="instance">The instance's domain, e.g. <c>mastodon.social</c>.</param>
    IMastodonClient CreateAnonymousClient(string instance);

    /// <summary>
    ///     Creates a client for <paramref name="instance" />'s authentication endpoints — the app registration and
    ///     OAuth exchange that happen before any access token exists (ADR-0004).
    /// </summary>
    /// <param name="instance">The instance's domain, e.g. <c>mastodon.social</c>.</param>
    IAuthenticationClient CreateAuthenticationClient(string instance);
}
