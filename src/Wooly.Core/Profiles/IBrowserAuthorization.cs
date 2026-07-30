namespace Wooly.Core.Profiles;

/// <summary>
///     One sign-in in progress: an address to send the user to, and the wait for what their browser brings back.
///     Disposing it gives up the port that was borrowed to be redirected to, whether or not the sign-in finished.
/// </summary>
public interface IBrowserAuthorization : IDisposable
{
    /// <summary>The instance's authorization page, to be opened in the user's browser.</summary>
    Uri AuthorizationUrl { get; }

    /// <summary>
    ///     Waits for the browser to be redirected back, then trades what it carried for an access token. Gives up
    ///     after a while rather than waiting on a browser that is never coming.
    /// </summary>
    /// <param name="cancellationToken">Stops waiting, e.g. because the user gave up first.</param>
    /// <returns>The access token the account authorized.</returns>
    /// <exception cref="Errors.AuthenticationException">
    ///     The user turned the request down, the browser never came back, or what it came back with did not belong to
    ///     this sign-in.
    /// </exception>
    Task<string> AwaitAccessToken(CancellationToken cancellationToken);
}
