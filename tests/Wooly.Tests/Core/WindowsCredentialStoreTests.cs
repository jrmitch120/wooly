using Wooly.Core.Credentials;
using Wooly.Tests.Fakes;

namespace Wooly.Tests.Core;

/// <summary>
///     Access tokens in Windows' Credential Manager, reached through Windows' own API rather than Git Credential
///     Manager — which needs Git installed to open at all, and Git is not something a Windows machine can be assumed to
///     have. Tested over a fake of the API; <see cref="CredentialManagerRoundTripTests" /> is the real thing.
/// </summary>
public class WindowsCredentialStoreTests
{
    private readonly FakeCredentialManager _manager = new();

    [Fact]
    public void SaveAccessToken_ThenFindAccessToken_RoundTripsThroughCredentialManager()
    {
        var store = Open();

        store.SaveAccessToken("personal", "token-abc");

        Assert.Equal("token-abc", store.FindAccessToken("personal"));
    }

    /// <summary>
    ///     One credential per profile, named so that they group together in Credential Manager's own list. The literal
    ///     is spelled out rather than read from the code: the name is a contract with credentials already written.
    /// </summary>
    [Fact]
    public void SaveAccessToken_FilesEachProfileUnderItsOwnNamedCredential()
    {
        var store = Open();

        store.SaveAccessToken("personal", "token-personal");
        store.SaveAccessToken("work", "token-work");

        Assert.Equal(
            new Dictionary<string, (string, string)>
            {
                ["wooly:access-token:personal"] = ("personal", "token-personal"),
                ["wooly:access-token:work"] = ("work", "token-work"),
            },
            _manager.Credentials);
    }

    [Fact]
    public void SaveAccessToken_ReplacesATokenThatHasBeenRenewed()
    {
        var store = Open();

        store.SaveAccessToken("personal", "token-old");
        store.SaveAccessToken("personal", "token-new");

        Assert.Equal("token-new", store.FindAccessToken("personal"));
        Assert.Single(_manager.Credentials);
    }

    [Fact]
    public void FindAccessToken_ReadsAsAbsentForAProfileThatHasNeverSignedIn() =>
        Assert.Null(Open().FindAccessToken("personal"));

    [Fact]
    public void DeleteAccessToken_ForgetsTheTokenAndSaysWhetherThereWasOne()
    {
        var store = Open();

        store.SaveAccessToken("personal", "token-abc");

        Assert.True(store.DeleteAccessToken("personal"));
        Assert.Null(store.FindAccessToken("personal"));
        Assert.False(store.DeleteAccessToken("personal"));
    }

    [Fact]
    public void Storage_ReportsTheOsKeyring() => Assert.Equal(CredentialStorage.OsKeyring, Open().Storage);

    /// <summary>
    ///     A process with nowhere to keep credentials — a service with no logon session — fails here, where
    ///     <see cref="FallbackCredentialStore" /> is watching, rather than later, mid-command.
    /// </summary>
    [Fact]
    public void Open_RefusesACredentialManagerThatWillNotAnswer()
    {
        _manager.Unavailable = true;

        Assert.Throws<System.ComponentModel.Win32Exception>(Open);
    }

    private WindowsCredentialStore Open() => WindowsCredentialStore.Open(_manager);
}
