using Wooly.Core;
using Wooly.Core.Credentials;
using Wooly.Tests.Fakes;

namespace Wooly.Tests.Core;

/// <summary>
///     Covers the choice ADR-0003 describes — OS keyring where one answers, plaintext file where none does — without
///     needing a machine that actually has no keyring: the keyring is opened through a delegate the test controls.
/// </summary>
public class FallbackCredentialStoreTests : IDisposable
{
    private readonly TemporaryDirectory _directory = new();
    private readonly FakeOsKeyring _keyring = new();

    public void Dispose() => _directory.Dispose();

    [Fact]
    public void SaveAccessToken_PutsTheTokenInTheKeyringWhenTheMachineHasOne()
    {
        var store = WithKeyring();

        store.SaveAccessToken("personal", "token-abc");

        Assert.Equal("token-abc", Assert.Single(_keyring.Secrets).Value);
        Assert.False(File.Exists(CredentialFile), "no plaintext file should be written when a keyring answers");
    }

    [Fact]
    public void Storage_ReportsTheKeyringWhenTheMachineHasOne()
    {
        Assert.Equal(CredentialStorage.OsKeyring, WithKeyring().Storage);
    }

    [Fact]
    public void Storage_ReportsPlaintextWhenNoKeyringIsAvailable()
    {
        Assert.Equal(CredentialStorage.PlaintextFile, WithoutKeyring().Storage);
    }

    /// <summary>The fallback exists so the tool still works on a bare server — not so it fails more politely.</summary>
    [Fact]
    public void SaveAccessToken_StillRoundTripsWhenNoKeyringIsAvailable()
    {
        var store = WithoutKeyring();

        store.SaveAccessToken("personal", "token-abc");

        Assert.Equal("token-abc", store.FindAccessToken("personal"));
        Assert.True(File.Exists(CredentialFile));
        Assert.True(store.DeleteAccessToken("personal"));
    }

    /// <summary>Opening a keyring can prompt or block, so it happens once per run rather than once per call.</summary>
    [Fact]
    public void FindAccessToken_OpensTheKeyringOnceHoweverManyTimesTheStoreIsUsed()
    {
        var opened = 0;
        var store = new FallbackCredentialStore(
            () =>
            {
                opened++;

                return new OsKeyringCredentialStore(_keyring);
            },
            new PlaintextFileCredentialStore(new WoolyPaths(_directory.Path)));

        store.SaveAccessToken("personal", "token-abc");
        store.FindAccessToken("personal");
        store.DeleteAccessToken("personal");
        _ = store.Storage;

        Assert.Equal(1, opened);
    }

    [Fact]
    public void FindAccessToken_DoesNotRetryAKeyringThatAlreadyRefusedToOpen()
    {
        var attempts = 0;
        var store = new FallbackCredentialStore(
            () =>
            {
                attempts++;

                throw new InvalidOperationException("no usable credential store");
            },
            new PlaintextFileCredentialStore(new WoolyPaths(_directory.Path)));

        store.FindAccessToken("personal");
        store.FindAccessToken("personal");

        Assert.Equal(1, attempts);
    }

    private string CredentialFile => Path.Combine(_directory.Path, "credentials.toml");

    private FallbackCredentialStore WithKeyring() => new(
        () => new OsKeyringCredentialStore(_keyring),
        new PlaintextFileCredentialStore(new WoolyPaths(_directory.Path)));

    private FallbackCredentialStore WithoutKeyring() => new(
        () => throw new InvalidOperationException("no usable credential store was found on this machine"),
        new PlaintextFileCredentialStore(new WoolyPaths(_directory.Path)));
}
