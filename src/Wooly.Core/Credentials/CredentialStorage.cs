namespace Wooly.Core.Credentials;

/// <summary>
///     Where a profile's access token is actually being kept. Front ends surface this so the weaker of the two is a
///     visible tradeoff rather than a silent one (ADR-0003).
/// </summary>
public enum CredentialStorage
{
    /// <summary>The OS's own secure store: Keychain, Credential Manager, or Secret Service.</summary>
    OsKeyring,

    /// <summary>A file this client owns, holding tokens in the clear. Used only where no keyring is available.</summary>
    PlaintextFile,
}
