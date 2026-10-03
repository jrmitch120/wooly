using Wooly.Core.Credentials;

namespace Wooly.Tests.Fakes;

/// <summary>
///     Windows' Credential Manager as a dictionary of generic credentials by target name, so the store over it can be
///     tested on any machine. The real one is <see cref="CredentialManagerApi" />, reached only on Windows.
/// </summary>
internal sealed class FakeCredentialManager : ICredentialManager
{
    private readonly Dictionary<string, (string UserName, string Secret)> _credentials = [];

    public IReadOnlyDictionary<string, (string UserName, string Secret)> Credentials => _credentials;

    /// <summary>Whether every call fails, as it does for a process with no logon session to keep credentials in.</summary>
    public bool Unavailable { get; set; }

    public string? Read(string target)
    {
        Refuse();

        return _credentials.TryGetValue(target, out var credential) ? credential.Secret : null;
    }

    public void Write(string target, string userName, string secret)
    {
        Refuse();

        _credentials[target] = (userName, secret);
    }

    public bool Delete(string target)
    {
        Refuse();

        return _credentials.Remove(target);
    }

    private void Refuse()
    {
        if (Unavailable)
        {
            throw new System.ComponentModel.Win32Exception(1312, "A specified logon session does not exist.");
        }
    }
}
