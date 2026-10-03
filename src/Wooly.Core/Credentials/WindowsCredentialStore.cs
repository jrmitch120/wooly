using System.Runtime.Versioning;

namespace Wooly.Core.Credentials;

/// <summary>
///     Access tokens held in Windows' Credential Manager, one generic credential per profile, named
///     <c>wooly:access-token:&lt;profile&gt;</c> so they group together in its list (ADR-0003).
/// </summary>
/// <remarks>
///     Reached through Windows' own API rather than through Git Credential Manager, which macOS and Linux still use.
///     GCM opens only where Git is installed — it looks Git up on the <c>PATH</c> as it starts — and a Windows machine
///     cannot be assumed to have Git. Without it, every token on Windows went to the plaintext file.
/// </remarks>
public sealed class WindowsCredentialStore : ICredentialStore
{
    private const string Prefix = "wooly:access-token:";

    private readonly ICredentialManager _manager;

    private WindowsCredentialStore(ICredentialManager manager) => _manager = manager;

    /// <inheritdoc />
    public CredentialStorage Storage => CredentialStorage.OsKeyring;

    /// <summary>
    ///     This user's Credential Manager, asked once before it is handed back, so that one that will not answer fails
    ///     here, where <see cref="FallbackCredentialStore" /> is watching.
    /// </summary>
    [SupportedOSPlatform("windows")]
    public static WindowsCredentialStore Open() => Open(new CredentialManagerApi());

    /// <summary>The same, over <paramref name="manager" />.</summary>
    internal static WindowsCredentialStore Open(ICredentialManager manager)
    {
        manager.Read(Prefix);

        return new WindowsCredentialStore(manager);
    }

    /// <inheritdoc />
    public string? FindAccessToken(string profileName) => _manager.Read(Prefix + profileName);

    /// <inheritdoc />
    public void SaveAccessToken(string profileName, string accessToken) =>
        _manager.Write(Prefix + profileName, profileName, accessToken);

    /// <inheritdoc />
    public bool DeleteAccessToken(string profileName) => _manager.Delete(Prefix + profileName);
}
