using Wooly.Core.Credentials;
using Wooly.Tests.Fakes;

namespace Wooly.Tests.Core;

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

    private OsKeyringCredentialStore NewStore() => new(_keyring);
}
