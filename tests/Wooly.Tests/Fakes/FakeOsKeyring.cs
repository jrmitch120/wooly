using GitCredentialManager;

namespace Wooly.Tests.Fakes;

/// <summary>
///     An OS keyring that answers, standing in for Keychain / Credential Manager / Secret Service. Backed by a
///     dictionary so tests can assert on what a real keyring would be holding.
/// </summary>
internal sealed class FakeOsKeyring : ICredentialStore
{
    private readonly Dictionary<(string Service, string Account), string> _secrets = new();

    public IReadOnlyDictionary<(string Service, string Account), string> Secrets => _secrets;

    /// <summary>
    ///     Whether writing is refused, as Windows' Credential Manager refused a service name that was not a URI: the
    ///     keyring opens and reads, and throws on the save.
    /// </summary>
    public bool RefusesWrites { get; set; }

    public IList<string> GetAccounts(string service) =>
        _secrets.Keys.Where(key => key.Service == service).Select(key => key.Account).ToList();

    public ICredential? Get(string service, string account) =>
        _secrets.TryGetValue((service, account), out var secret) ? new Credential(account, secret) : null;

    public void AddOrUpdate(string service, string account, string secret)
    {
        if (RefusesWrites)
        {
            throw new UriFormatException("Invalid URI: The format of the URI could not be determined.");
        }

        _secrets[(service, account)] = secret;
    }

    public bool Remove(string service, string account) => _secrets.Remove((service, account));

    private sealed record Credential(string Account, string Password) : ICredential;
}
