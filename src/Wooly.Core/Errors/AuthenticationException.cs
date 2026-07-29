namespace Wooly.Core.Errors;

/// <summary>
///     There are no usable credentials to act with: no profile has been chosen, the chosen one has no access token
///     stored, or the instance refused the token it was given. Every one of those is fixed the same way — authenticate
///     the profile again — so front ends can treat them as one outcome.
/// </summary>
public sealed class AuthenticationException(string message) : WoolyException(message);
