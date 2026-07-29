using Wooly.Core.Credentials;

namespace Wooly.Tests.Core;

/// <summary>
///     The one test that talks to the machine's real Keychain / Credential Manager / Secret Service, proving the
///     adapter matches what <c>Devlooped.CredentialManager</c> actually does rather than what a fake keyring was
///     written to do. Opt-in, because a routine test run has no business writing to a developer's own keyring: set
///     <c>WOOLY_KEYRING_TESTS=1</c> to include it.
/// </summary>
public class OsKeyringRoundTripTests
{
    /// <summary>A name no real profile would take, so a failed run leaves nothing confusing behind.</summary>
    private const string ProfileName = "wooly-test-profile";

    public static bool Enabled => Environment.GetEnvironmentVariable("WOOLY_KEYRING_TESTS") == "1";

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
}
