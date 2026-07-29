using OsKeyring = GitCredentialManager.ICredentialStore;

namespace Wooly.Core.Credentials;

/// <summary>
///     Access tokens held by the OS's own secure store, reached through <c>Devlooped.CredentialManager</c> (ADR-0003).
///     That library speaks in (service, account) pairs and prefixes the service with the namespace it was opened
///     under, so this client's entries read as <c>mastodon-cli:access-token</c> in Keychain or Credential Manager,
///     one per profile.
///     <para>
///         Only <see cref="Open()" /> can build one, so the name on this class is a promise rather than a hope: there
///         is no way to end up with an <c>OsKeyringCredentialStore</c> over a store that is not the OS keyring.
///     </para>
/// </summary>
public sealed class OsKeyringCredentialStore : ICredentialStore
{
    /// <summary>
    ///     What each entry holds. Names the secret rather than the client, because the namespace
    ///     <see cref="GcmKeyring.Open" /> opens under already supplies the <c>mastodon-cli</c> half of the label the
    ///     OS displays.
    /// </summary>
    private const string Service = "access-token";

    private readonly OsKeyring _keyring;

    private OsKeyringCredentialStore(OsKeyring keyring) => _keyring = keyring;

    /// <summary>
    ///     Opens this machine's own keyring, and proves it answers before handing the store back.
    /// </summary>
    /// <exception cref="Exception">No keyring is available, or the one that is refused to answer.</exception>
    public static OsKeyringCredentialStore Open() => Open(GcmKeyring.Open);

    /// <summary>
    ///     Accepts Git Credential Manager's answer only if it is the store this client asked for.
    ///     <para>
    ///         GCM hands back a working store object whichever backing store it resolved to, so opening it is not the
    ///         question — <see cref="GcmKeyring.BackingStoreName" /> is. Anything other than the pinned keyring means
    ///         the pin did not take, and this client will not write a token somewhere it cannot describe: it fails
    ///         here, where <see cref="FallbackCredentialStore" /> is watching, and the run continues on the plaintext
    ///         file ADR-0003 names — which at least says what it is, and is readable by nobody but its owner.
    ///     </para>
    ///     <para>
    ///         Naming the right store is still not the same as having one, so the keyring is read from before it is
    ///         handed back. Reading is the cheapest question that reaches the real backing store, so a machine with no
    ///         keyring fails here too rather than later, mid-command.
    ///     </para>
    /// </summary>
    /// <param name="openGcm">
    ///     Opens Git Credential Manager. Kept a delegate so that every store GCM can resolve to is reachable in a test
    ///     without reconfiguring the developer's own Git.
    /// </param>
    /// <exception cref="InvalidOperationException">GCM resolved to something other than this machine's keyring.</exception>
    /// <exception cref="Exception">The keyring GCM named refused to answer.</exception>
    internal static OsKeyringCredentialStore Open(Func<GcmKeyring> openGcm)
    {
        var gcm = openGcm();

        if (gcm.BackingStoreName != GcmKeyring.BackingStoreForThisMachine)
        {
            throw new InvalidOperationException(
                $"Git Credential Manager resolved to '{gcm.BackingStoreName ?? "no store in particular"}' rather " +
                $"than this machine's keyring ('{GcmKeyring.BackingStoreForThisMachine}').");
        }

        gcm.Keyring.GetAccounts(Service);

        return new OsKeyringCredentialStore(gcm.Keyring);
    }

    /// <inheritdoc />
    /// <remarks>
    ///     Constant, and honest for that reason rather than in spite of it: <see cref="Open(Func{GcmKeyring})" />
    ///     refuses to build this store over anything but the OS keyring, so there is no case left for it to misreport.
    /// </remarks>
    public CredentialStorage Storage => CredentialStorage.OsKeyring;

    /// <inheritdoc />
    public string? FindAccessToken(string profileName) => _keyring.Get(Service, profileName)?.Password;

    /// <inheritdoc />
    public void SaveAccessToken(string profileName, string accessToken) =>
        _keyring.AddOrUpdate(Service, profileName, accessToken);

    /// <inheritdoc />
    public bool DeleteAccessToken(string profileName) => _keyring.Remove(Service, profileName);
}
