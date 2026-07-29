using System.Security.Cryptography;
using Mastonet;
using Wooly.Core.Errors;

namespace Wooly.Core.Profiles;

/// <summary>
///     The browser half of ADR-0004, in the three steps Mastodon's OAuth takes: register this client with the
///     instance, send the user to the instance's authorization page, and trade the code their browser is redirected
///     back with for an access token.
/// </summary>
/// <param name="patience">
///     How long to wait for the browser after the user has been sent to it. Long enough to log in to an instance and
///     read what is being asked for, short enough that a terminal is never wedged indefinitely by a tab that was
///     closed.
/// </param>
public sealed class BrowserAuthorizer(IMastodonClientFactory clientFactory, TimeSpan patience) : IBrowserAuthorizer
{
    /// <summary>What <see cref="ServiceCollectionExtensions.AddWoolyCore" /> registers this with.</summary>
    public static readonly TimeSpan DefaultPatience = TimeSpan.FromMinutes(5);

    /// <summary>
    ///     What this client asks an account for: everything the CLI and the TUI do on the user's behalf, and nothing
    ///     administrative. Asked for once, at registration, because a scope added later needs authorizing again.
    /// </summary>
    private static readonly GranularScope[] Scopes = [GranularScope.Read, GranularScope.Write, GranularScope.Follow];

    /// <inheritdoc />
    public async Task<IBrowserAuthorization> Begin(string instance, CancellationToken cancellationToken)
    {
        // Started before the instance is told anything, because the redirect URI is part of what it is being told —
        // and an instance holds a client to the address it registered.
        var listener = LoopbackRedirectListener.Start();

        try
        {
            var client = clientFactory.CreateAuthenticationClient(instance);
            var redirectUri = listener.RedirectUri.ToString();

            await RegisterClient(client, instance, redirectUri, cancellationToken);

            // Mastonet builds the authorization URL but has no notion of a state value, so it is added here. It is
            // what tells this sign-in's redirect from one anything else on this machine could have caused.
            var state = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
            var authorizationUrl = new Uri($"{client.OAuthUrl(redirectUri)}&state={Uri.EscapeDataString(state)}");

            return new BrowserAuthorization(client, listener, authorizationUrl, state, instance, patience);
        }
        catch
        {
            // Nobody is going to be redirected here now, and the port is the machine's, not this client's.
            listener.Dispose();

            throw;
        }
    }

    private static async Task RegisterClient(
        IAuthenticationClient client,
        string instance,
        string redirectUri,
        CancellationToken cancellationToken)
    {
        try
        {
            await client.CreateApp(WoolyClient.Name, WoolyClient.Website, redirectUri, Scopes)
                        .WaitAsync(cancellationToken);
        }
        catch (ServerErrorException exception)
        {
            // Registration is open on a stock Mastodon, so an instance refusing it has been configured to — and its
            // own wording is the only account of why.
            throw new AuthenticationException($"{instance} would not register {WoolyClient.Name}: {exception.Message}");
        }
    }

    /// <summary>One sign-in, from the moment the user has somewhere to be sent to.</summary>
    private sealed class BrowserAuthorization(
        IAuthenticationClient client,
        LoopbackRedirectListener listener,
        Uri authorizationUrl,
        string state,
        string instance,
        TimeSpan patience) : IBrowserAuthorization
    {
        /// <inheritdoc />
        public Uri AuthorizationUrl { get; } = authorizationUrl;

        /// <inheritdoc />
        public async Task<string> AwaitAccessToken(CancellationToken cancellationToken)
        {
            var redirect = await AwaitRedirect(cancellationToken);

            if (redirect.Error is not null)
            {
                throw new AuthenticationException(
                    $"{instance} did not authorize {WoolyClient.Name}: {redirect.ErrorDescription ?? redirect.Error}");
            }

            // Ordinal because this is a comparison of two opaque values, not of two pieces of text: nothing about
            // culture or case should make two different states look like one.
            if (!string.Equals(redirect.State, state, StringComparison.Ordinal))
            {
                throw new AuthenticationException(
                    "The browser came back with an authorization this sign-in did not ask for, so it was not used. Authorize the profile again.");
            }

            if (redirect.Code is null)
            {
                throw new AuthenticationException($"{instance} sent the browser back without an authorization code.");
            }

            return await Exchange(redirect.Code, cancellationToken);
        }

        /// <inheritdoc />
        public void Dispose() => listener.Dispose();

        private async Task<AuthorizationRedirect> AwaitRedirect(CancellationToken cancellationToken)
        {
            using var waiting = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            waiting.CancelAfter(patience);

            try
            {
                return await listener.AwaitRedirect(waiting.Token);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                // Only this class's own clock ran out — a caller who cancelled gets their cancellation, not this.
                throw new AuthenticationException(
                    $"The browser never came back from {instance}, so the profile was not connected. Authorize it again, or paste an access token instead.");
            }
        }

        private async Task<string> Exchange(string code, CancellationToken cancellationToken)
        {
            try
            {
                // The redirect URI goes out again because an instance checks the exchange against the address it
                // redirected to, and Mastonet defaults to a different one when not told.
                var auth = await client.ConnectWithCode(code, listener.RedirectUri.ToString())
                                       .WaitAsync(cancellationToken);

                return string.IsNullOrWhiteSpace(auth.AccessToken)
                    ? throw new AuthenticationException($"{instance} answered the authorization with no access token.")
                    : auth.AccessToken;
            }
            catch (ServerErrorException exception)
            {
                // An authorization code is single-use and short-lived, so the commonest way here is a sign-in left
                // half-finished for too long.
                throw new AuthenticationException(
                    $"{instance} would not exchange that authorization for an access token: {exception.Message}");
            }
        }
    }
}
