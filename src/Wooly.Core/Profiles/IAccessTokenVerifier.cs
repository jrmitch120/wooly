namespace Wooly.Core.Profiles;

/// <summary>
///     Turns an access token into the account it signs in as. A token typed or pasted by hand (ADR-0004's headless
///     path) is checked here before a profile is written for it, so a mistyped token is caught while the user is still
///     looking at it rather than on their next command.
/// </summary>
public interface IAccessTokenVerifier
{
    /// <summary>Asks <paramref name="instance" /> who <paramref name="accessToken" /> belongs to.</summary>
    /// <param name="instance">The instance's domain, e.g. <c>mastodon.social</c>.</param>
    /// <param name="accessToken">The token to check.</param>
    /// <returns>The account the token signs in as, as <c>username@instance</c>.</returns>
    /// <exception cref="Errors.AuthenticationException">The instance would not accept the token.</exception>
    Task<string> VerifyAccount(string instance, string accessToken);
}
