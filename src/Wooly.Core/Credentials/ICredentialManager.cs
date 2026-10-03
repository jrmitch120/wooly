namespace Wooly.Core.Credentials;

/// <summary>
///     Windows' Credential Manager, as much of it as this client uses: generic credentials, read, written and deleted by
///     their target name. A port so that <see cref="WindowsCredentialStore" /> can be tested on any machine
///     (ADR-0005); the real one is <see cref="CredentialManagerApi" />.
/// </summary>
internal interface ICredentialManager
{
    /// <summary>The secret held under <paramref name="target" />, or <see langword="null" /> where none is.</summary>
    string? Read(string target);

    /// <summary>Holds <paramref name="secret" /> under <paramref name="target" />, replacing whatever was there.</summary>
    void Write(string target, string userName, string secret);

    /// <summary>Forgets what is held under <paramref name="target" />, and says whether there was anything.</summary>
    bool Delete(string target);
}
