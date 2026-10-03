using Wooly.Core;
using Wooly.Core.Credentials;
using Wooly.Tests.Fakes;

namespace Wooly.Tests.Core;

/// <summary>
///     Covers the choice ADR-0003 describes — OS keyring where one answers, plaintext file where none does — without
///     needing a machine that actually has no keyring: the keyring is opened through a delegate the test controls,
///     and what Git Credential Manager resolved to arrives through that same delegate as data.
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

                return OsKeyringCredentialStore.Open(
                    () => new GcmKeyring(GcmKeyring.BackingStoreForThisMachine, _keyring));
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

    /// <summary>
    ///     A user who configured Git Credential Manager for cleartext did so for Git, not for Mastodon. Their tokens
    ///     must not be reported as keyring-protected while GCM writes them in the clear (#34) — the run lands on this
    ///     client's own plaintext file instead, which says what it is and is readable by nobody but its owner.
    /// </summary>
    [Fact]
    public void Storage_ReportsPlaintextWhenGcmWouldHaveWrittenTheTokenInTheClear()
    {
        Assert.Equal(CredentialStorage.PlaintextFile, WithGcmResolvingTo("plaintext").Storage);
    }

    /// <summary>
    ///     GCM's <c>none</c> store answers every write with success and every read with nothing. Reporting that as
    ///     secure storage would sign the user out on every later run without ever saying so.
    /// </summary>
    [Fact]
    public void SaveAccessToken_KeepsTheTokenWhenGcmWouldHaveStoredNothingAtAll()
    {
        var store = WithGcmResolvingTo("none");

        store.SaveAccessToken("personal", "token-abc");

        Assert.Equal(CredentialStorage.PlaintextFile, store.Storage);
        Assert.Equal("token-abc", store.FindAccessToken("personal"));
        Assert.Empty(_keyring.Secrets);
    }

    /// <summary>
    ///     A keyring that opens and then refuses the save — as Windows' did, on a service name it could not parse —
    ///     loses nothing: the token goes to the plaintext file, and the store says that is where tokens now are, so
    ///     the warning about it is shown. Before, the exception took the whole TUI down.
    /// </summary>
    [Fact]
    public void SaveAccessToken_FallsBackToThePlaintextFileWhenTheKeyringRefusesTheSave()
    {
        _keyring.RefusesWrites = true;
        var store = WithKeyring();

        store.SaveAccessToken("personal", "token-abc");

        Assert.Equal(CredentialStorage.PlaintextFile, store.Storage);
        Assert.Equal("token-abc", store.FindAccessToken("personal"));
        Assert.True(File.Exists(CredentialFile));
        Assert.Empty(_keyring.Secrets);
    }

    /// <summary>
    ///     On the next run the keyring opens again, and a token it could not hold is still found — in the file it was
    ///     put in instead — rather than the profile being signed out.
    /// </summary>
    [Fact]
    public void FindAccessToken_FindsATokenTheKeyringCouldNotHoldOnTheNextRun()
    {
        _keyring.RefusesWrites = true;
        WithKeyring().SaveAccessToken("personal", "token-abc");
        _keyring.RefusesWrites = false;

        var nextRun = WithKeyring();

        Assert.Equal(CredentialStorage.OsKeyring, nextRun.Storage);
        Assert.Equal("token-abc", nextRun.FindAccessToken("personal"));
    }

    /// <summary>
    ///     A token the keyring does take, once it takes them again, is no longer left in the clear as well — the file's
    ///     copy is the very thing the keyring is there to avoid.
    /// </summary>
    [Fact]
    public void SaveAccessToken_TakesThePlaintextCopyAwayOnceTheKeyringHoldsTheToken()
    {
        _keyring.RefusesWrites = true;
        WithKeyring().SaveAccessToken("personal", "token-old");
        _keyring.RefusesWrites = false;

        var nextRun = WithKeyring();

        nextRun.SaveAccessToken("personal", "token-new");

        Assert.Equal("token-new", nextRun.FindAccessToken("personal"));
        Assert.Null(new PlaintextFileCredentialStore(new WoolyPaths(_directory.Path)).FindAccessToken("personal"));
    }

    /// <summary>
    ///     A refusal that passes, within the same run, stops being reported once the keyring has taken the token the
    ///     file was holding — nothing is left in the clear for the warning to be about.
    /// </summary>
    [Fact]
    public void Storage_ReportsTheKeyringAgainOnceItTakesTheTokenItRefused()
    {
        var store = WithKeyring();

        _keyring.RefusesWrites = true;
        store.SaveAccessToken("personal", "token-old");
        _keyring.RefusesWrites = false;
        store.SaveAccessToken("personal", "token-new");

        Assert.Equal(CredentialStorage.OsKeyring, store.Storage);
    }

    /// <summary>
    ///     But only for that profile: another one's token the keyring refused is still in the file, and still warned
    ///     about, until it is saved again or the profile removed.
    /// </summary>
    [Fact]
    public void Storage_ReportsPlaintextWhileAnyRefusedTokenIsStillInTheFile()
    {
        var store = WithKeyring();

        _keyring.RefusesWrites = true;
        store.SaveAccessToken("personal", "token-personal");
        _keyring.RefusesWrites = false;
        store.SaveAccessToken("work", "token-work");

        Assert.Equal(CredentialStorage.PlaintextFile, store.Storage);

        store.DeleteAccessToken("personal");

        Assert.Equal(CredentialStorage.OsKeyring, store.Storage);
    }

    /// <summary>Removing a profile forgets its token wherever it was put.</summary>
    [Fact]
    public void DeleteAccessToken_ForgetsTheTokenInTheKeyringAndInTheFile()
    {
        _keyring.RefusesWrites = true;
        WithKeyring().SaveAccessToken("personal", "token-abc");
        _keyring.RefusesWrites = false;

        var nextRun = WithKeyring();

        nextRun.SaveAccessToken("work", "token-work");

        Assert.True(nextRun.DeleteAccessToken("personal"));
        Assert.True(nextRun.DeleteAccessToken("work"));
        Assert.Null(nextRun.FindAccessToken("personal"));
        Assert.Null(nextRun.FindAccessToken("work"));
        Assert.False(nextRun.DeleteAccessToken("personal"));
    }

    private string CredentialFile => Path.Combine(_directory.Path, "credentials.toml");

    private FallbackCredentialStore WithGcmResolvingTo(string? backingStoreName) => new(
        () => OsKeyringCredentialStore.Open(() => new GcmKeyring(backingStoreName, _keyring)),
        new PlaintextFileCredentialStore(new WoolyPaths(_directory.Path)));

    private FallbackCredentialStore WithKeyring() => WithGcmResolvingTo(GcmKeyring.BackingStoreForThisMachine);

    private FallbackCredentialStore WithoutKeyring() => new(
        () => throw new InvalidOperationException("no usable credential store was found on this machine"),
        new PlaintextFileCredentialStore(new WoolyPaths(_directory.Path)));
}
