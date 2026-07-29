using GitCredentialManager;
using OsKeyring = GitCredentialManager.ICredentialStore;

namespace Wooly.Core.Credentials;

/// <summary>
///     Access tokens held by the OS's own secure store, reached through <c>Devlooped.CredentialManager</c> (ADR-0003).
///     That library speaks in (service, account) pairs and prefixes the service with the namespace it was opened
///     under, so this client's entries read as <c>mastodon-cli:access-token</c> in Keychain or Credential Manager,
///     one per profile.
/// </summary>
public sealed class OsKeyringCredentialStore(OsKeyring keyring) : ICredentialStore
{
    /// <summary>
    ///     What each entry holds. Names the secret rather than the client, because the namespace passed to
    ///     <see cref="Open" /> already supplies the <c>mastodon-cli</c> half of the label the OS displays.
    /// </summary>
    private const string Service = "access-token";

    /// <summary>
    ///     Opens this machine's keyring, and proves it answers before handing the store back. Reading is the cheapest
    ///     question that reaches the real backing store, so a machine with no keyring fails here — where
    ///     <see cref="FallbackCredentialStore" /> is watching for it — rather than later, mid-command.
    /// </summary>
    /// <exception cref="Exception">No keyring is available, or the one that is refused to answer.</exception>
    public static OsKeyringCredentialStore Open()
    {
        var keyring = CredentialManager.Create(WoolyClient.Name);
        keyring.GetAccounts(Service);

        return new OsKeyringCredentialStore(keyring);
    }

    /// <inheritdoc />
    public CredentialStorage Storage => CredentialStorage.OsKeyring;

    /// <inheritdoc />
    public string? FindAccessToken(string profileName) => keyring.Get(Service, profileName)?.Password;

    /// <inheritdoc />
    public void SaveAccessToken(string profileName, string accessToken) =>
        keyring.AddOrUpdate(Service, profileName, accessToken);

    /// <inheritdoc />
    public bool DeleteAccessToken(string profileName) => keyring.Remove(Service, profileName);
}
