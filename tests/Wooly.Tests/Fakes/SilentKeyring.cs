using GitCredentialManager;

namespace Wooly.Tests.Fakes;

/// <summary>
///     A keyring that is named but not there — a Linux box with no Secret Service running. Every question it is
///     asked throws, the way a real backend does when nothing is listening.
/// </summary>
internal sealed class SilentKeyring : ICredentialStore
{
    public IList<string> GetAccounts(string service) => throw new PlatformNotSupportedException();

    public ICredential? Get(string service, string account) => throw new PlatformNotSupportedException();

    public void AddOrUpdate(string service, string account, string secret) =>
        throw new PlatformNotSupportedException();

    public bool Remove(string service, string account) => throw new PlatformNotSupportedException();
}
