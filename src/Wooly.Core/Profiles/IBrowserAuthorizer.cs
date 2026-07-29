namespace Wooly.Core.Profiles;

/// <summary>
///     Connects a profile to a Mastodon account the way ADR-0004 makes primary: the user approves this client on the
///     instance's own page in their browser, and no password ever passes through here. What comes back is an access
///     token, stored exactly as a pasted one is.
/// </summary>
public interface IBrowserAuthorizer
{
    /// <summary>
    ///     Registers this client with <paramref name="instance" /> and prepares to be redirected back to, without yet
    ///     sending anyone anywhere. Split from the waiting because the caller owns the two things in between — showing
    ///     the user the address and opening their browser at it.
    /// </summary>
    /// <param name="instance">The instance's domain, e.g. <c>mastodon.social</c>.</param>
    /// <param name="cancellationToken">Abandons the registration call.</param>
    /// <exception cref="Errors.AuthenticationException">The instance would not register this client.</exception>
    Task<IBrowserAuthorization> Begin(string instance, CancellationToken cancellationToken);
}
