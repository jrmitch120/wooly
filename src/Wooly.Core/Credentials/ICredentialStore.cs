namespace Wooly.Core.Credentials;

/// <summary>
///     The secret half of what this client persists: one access token per profile. Everything non-secret goes through
///     <see cref="Configuration.IConfigStore" /> instead.
/// </summary>
public interface ICredentialStore
{
    /// <summary>Where tokens are being kept on this machine.</summary>
    CredentialStorage Storage { get; }

    /// <summary>
    ///     The access token stored for <paramref name="profileName" />, or <see langword="null" /> if that profile has
    ///     never signed in.
    /// </summary>
    string? FindAccessToken(string profileName);

    /// <summary>Stores <paramref name="accessToken" /> for <paramref name="profileName" />, replacing any earlier one.</summary>
    void SaveAccessToken(string profileName, string accessToken);

    /// <summary>
    ///     Forgets <paramref name="profileName" />'s access token. Returns <see langword="false" /> if there was
    ///     nothing to forget.
    /// </summary>
    bool DeleteAccessToken(string profileName);
}
