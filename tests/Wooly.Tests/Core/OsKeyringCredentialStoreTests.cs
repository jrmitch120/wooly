using Wooly.Core.Credentials;
using Wooly.Tests.Fakes;

namespace Wooly.Tests.Core;

/// <summary>
///     Covers both halves of this store: what it accepts from Git Credential Manager when it opens, and how it files
///     tokens once it has. GCM's answer arrives through a delegate as data, so a store that holds tokens in the
///     clear, or holds nothing at all, is reachable here without touching the developer's own Git configuration.
/// </summary>
public class OsKeyringCredentialStoreTests
{
    private readonly FakeOsKeyring _keyring = new();

    [Fact]
    public void SaveAccessToken_ThenFindAccessToken_RoundTripsThroughTheKeyring()
    {
        var store = NewStore();

        store.SaveAccessToken("personal", "token-abc");

        Assert.Equal("token-abc", store.FindAccessToken("personal"));
    }

    /// <summary>Profiles are separate identities, so one profile's token must never answer for another's.</summary>
    [Fact]
    public void FindAccessToken_KeepsEachProfilesTokenToItself()
    {
        var store = NewStore();
        store.SaveAccessToken("personal", "token-personal");
        store.SaveAccessToken("work", "token-work");

        Assert.Equal("token-personal", store.FindAccessToken("personal"));
        Assert.Equal("token-work", store.FindAccessToken("work"));
    }

    [Fact]
    public void FindAccessToken_ReadsAsAbsentForAProfileThatHasNeverSignedIn()
    {
        Assert.Null(NewStore().FindAccessToken("personal"));
    }

    [Fact]
    public void SaveAccessToken_ReplacesATokenThatHasBeenRenewed()
    {
        var store = NewStore();
        store.SaveAccessToken("personal", "token-old");

        store.SaveAccessToken("personal", "token-new");

        Assert.Equal("token-new", store.FindAccessToken("personal"));
    }

    [Fact]
    public void DeleteAccessToken_TakesTheTokenOutOfTheKeyringAndSaysItDidSo()
    {
        var store = NewStore();
        store.SaveAccessToken("personal", "token-abc");

        Assert.True(store.DeleteAccessToken("personal"));
        Assert.Null(store.FindAccessToken("personal"));
    }

    [Fact]
    public void DeleteAccessToken_SaysNothingWasThereWhenTheProfileHasNoToken()
    {
        Assert.False(NewStore().DeleteAccessToken("personal"));
    }

    /// <summary>
    ///     Every token is filed under one service, keyed by profile, so they group together in the OS's own credential
    ///     UI. The literal is spelled out rather than read from the constant: the name is a contract with entries a
    ///     previous version already wrote, and changing it should fail here.
    /// </summary>
    [Fact]
    public void SaveAccessToken_FilesTheTokenUnderOneServiceKeyedByProfile()
    {
        NewStore().SaveAccessToken("personal", "token-abc");

        Assert.Equal([("access-token", "personal")], _keyring.Secrets.Keys);
    }

    [Fact]
    public void Storage_ReportsThatTheTokenIsUnderTheOsKeyringsProtection()
    {
        Assert.Equal(CredentialStorage.OsKeyring, NewStore().Storage);
    }

    /// <summary>
    ///     GCM's plaintext store writes secrets to a file this client does not own and cannot vouch for. Refusing it
    ///     hands the run to <see cref="FallbackCredentialStore" />, whose plaintext file at least says what it is.
    /// </summary>
    [Fact]
    public void Open_RefusesAStoreThatWouldHoldTheTokenInTheClear()
    {
        Assert.Throws<InvalidOperationException>(() => OpenOver("plaintext"));
    }

    /// <summary>
    ///     GCM's <c>none</c> store swallows writes and answers reads with nothing, so a user would authenticate, be
    ///     told it worked, and be signed out again on the next run.
    /// </summary>
    [Fact]
    public void Open_RefusesAStoreThatHoldsNothingAtAll()
    {
        Assert.Throws<InvalidOperationException>(() => OpenOver("none"));
    }

    /// <summary>Git's credential cache forgets tokens on a timer, which is not somewhere to keep a sign-in.</summary>
    [Fact]
    public void Open_RefusesAStoreThatForgetsTheTokenLater()
    {
        Assert.Throws<InvalidOperationException>(() => OpenOver("cache"));
    }

    /// <summary>
    ///     Nothing is pinned unless the pin took: a <see langword="null" /> name means GCM was left to choose for
    ///     itself, which is the very thing this client stopped doing.
    /// </summary>
    [Fact]
    public void Open_RefusesWhenGcmWasLeftToChooseForItself()
    {
        Assert.Throws<InvalidOperationException>(() => OpenOver(backingStoreName: null));
    }

    /// <summary>
    ///     Naming the right store is not the same as having one. A machine with no keyring must fail here, where
    ///     <see cref="FallbackCredentialStore" /> is watching, rather than later, mid-command.
    /// </summary>
    [Fact]
    public void Open_RefusesAKeyringThatWillNotAnswer()
    {
        Assert.Throws<PlatformNotSupportedException>(
            () => OsKeyringCredentialStore.Open(
                () => new GcmKeyring(GcmKeyring.BackingStoreForThisMachine, new SilentKeyring())));
    }

    private OsKeyringCredentialStore NewStore() => OpenOver(GcmKeyring.BackingStoreForThisMachine);

    private OsKeyringCredentialStore OpenOver(string? backingStoreName) =>
        OsKeyringCredentialStore.Open(() => new GcmKeyring(backingStoreName, _keyring));
}
