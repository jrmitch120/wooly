namespace Wooly.Core.Profiles;

/// <summary>
///     What an instance sends the user's browser back with once they have finished with the authorization page: either
///     a one-time code to trade for an access token, or a refusal. Both are the same redirect, which is why one type
///     carries either.
/// </summary>
/// <param name="Code">The authorization code to exchange, or <see langword="null" /> if the request was refused.</param>
/// <param name="State">
///     The opaque value the authorization request was sent out with, echoed back. It is how a redirect belonging to
///     this sign-in is told from one some other page on this machine could have caused.
/// </param>
/// <param name="Error">The instance's machine-readable reason for a refusal, e.g. <c>access_denied</c>.</param>
/// <param name="ErrorDescription">The instance's sentence about the refusal, where it sent one.</param>
public sealed record AuthorizationRedirect(string? Code, string? State, string? Error, string? ErrorDescription)
{
    /// <summary>
    ///     Whether this redirect is the one being waited for at all. A browser asks a web address for more than the
    ///     page it was sent to, and none of the rest settle an authorization either way.
    /// </summary>
    public bool CarriesAnAuthorization => Code is not null || Error is not null;
}
