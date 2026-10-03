using Wooly.Core.Credentials;

namespace Wooly.Tests.Core;

/// <summary>
///     The real Credential Manager, through Windows' own API: what <see cref="WindowsCredentialStoreTests" /> takes on
///     trust. Writes to the machine's own credentials, so it runs only on Windows and only when asked.
/// </summary>
public class CredentialManagerRoundTripTests
{
    private const string ProfileName = "wooly-round-trip-test";

    public static bool Enabled =>
        OperatingSystem.IsWindows() && Environment.GetEnvironmentVariable("WOOLY_KEYRING_TESTS") == "1";

    [Fact(
        Skip = "Writes to this Windows machine's Credential Manager. Set WOOLY_KEYRING_TESTS=1 on Windows to run it.",
        SkipUnless = nameof(Enabled))]
    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    public void AnAccessTokenSurvivesAStoreAndReadThroughCredentialManager()
    {
        var store = WindowsCredentialStore.Open();

        try
        {
            store.SaveAccessToken(ProfileName, "token-abc");
            store.SaveAccessToken(ProfileName, "token-renewed");

            Assert.Equal("token-renewed", store.FindAccessToken(ProfileName));
            Assert.True(store.DeleteAccessToken(ProfileName));
            Assert.Null(store.FindAccessToken(ProfileName));
            Assert.False(store.DeleteAccessToken(ProfileName));
        }
        finally
        {
            store.DeleteAccessToken(ProfileName);
        }
    }
}
