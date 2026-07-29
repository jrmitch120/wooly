using Wooly.Core.Credentials;
using Wooly.Tests.Fakes;

namespace Wooly.Tests.Core;

/// <summary>
///     The tests that talk to the machine's real Keychain / Credential Manager / Secret Service, proving the adapter
///     matches what <c>Devlooped.CredentialManager</c> actually does rather than what a fake keyring was written to
///     do. Opt-in, because a routine test run has no business writing to a developer's own keyring: set
///     <c>WOOLY_KEYRING_TESTS=1</c> to include them.
/// </summary>
[Collection(nameof(GcmEnvironmentCollection))]
public class OsKeyringRoundTripTests : IDisposable
{
    /// <summary>A name no real profile would take, so a failed run leaves nothing confusing behind.</summary>
    private const string ProfileName = "wooly-test-profile";

    private readonly TemporaryEnvironmentVariable _configuredForGit = new(GcmKeyring.BackingStoreVariable);

    public static bool Enabled => Environment.GetEnvironmentVariable("WOOLY_KEYRING_TESTS") == "1";

    public void Dispose() => _configuredForGit.Dispose();

    [Fact(Skip = "Writes to the machine's own keyring. Set WOOLY_KEYRING_TESTS=1 to run it.", SkipUnless = nameof(Enabled))]
    public void AnAccessTokenSurvivesAStoreAndReadThroughTheMachinesOwnKeyring()
    {
        var store = OsKeyringCredentialStore.Open();

        try
        {
            store.SaveAccessToken(ProfileName, "token-abc");

            Assert.Equal(CredentialStorage.OsKeyring, store.Storage);
            Assert.Equal("token-abc", store.FindAccessToken(ProfileName));
            Assert.True(store.DeleteAccessToken(ProfileName));
            Assert.Null(store.FindAccessToken(ProfileName));
        }
        finally
        {
            store.DeleteAccessToken(ProfileName);
        }
    }

    /// <summary>
    ///     The one test that proves the pin against the real library rather than against this client's own
    ///     bookkeeping: with GCM told to write cleartext, it must still report the keyring as the store it chose.
    ///     Nothing is written either way — a pin that failed to take makes <c>Open</c> throw before a token exists.
    /// </summary>
    [Fact(Skip = "Reads the machine's own GCM configuration. Set WOOLY_KEYRING_TESTS=1 to run it.", SkipUnless = nameof(Enabled))]
    public void GitsOwnChoiceOfCredentialStoreDoesNotDecideWhereThisClientsTokensGo()
    {
        _configuredForGit.Value = "plaintext";

        Assert.Equal(GcmKeyring.BackingStoreForThisMachine, GcmKeyring.Open().BackingStoreName);
    }
}
