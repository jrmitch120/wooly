namespace Wooly.Core.Credentials;

/// <summary>
///     A keyring that opened and then would not answer for one profile — locked, a permission prompt declined, or its
///     service down — so that profile's token could not be read. Said rather than passed off as a profile
///     with no token, which is what it looks like otherwise (#296).
/// </summary>
/// <param name="ProfileName">The profile the keyring would not answer for.</param>
/// <param name="Error">What the keyring said when it refused, in its own words.</param>
public sealed record UnansweredKeyring(string ProfileName, string Error);
