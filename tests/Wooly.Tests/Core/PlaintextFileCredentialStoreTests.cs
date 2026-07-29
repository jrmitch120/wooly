using System.Runtime.Versioning;
using Wooly.Core;
using Wooly.Core.Credentials;
using Wooly.Tests.Fakes;

namespace Wooly.Tests.Core;

public class PlaintextFileCredentialStoreTests : IDisposable
{
    private readonly TemporaryDirectory _directory = new();

    public void Dispose() => _directory.Dispose();

    [Fact]
    public void SaveAccessToken_ThenFindAccessToken_RoundTripsThroughTheFile()
    {
        var store = NewStore();

        store.SaveAccessToken("personal", "token-abc");

        Assert.Equal("token-abc", store.FindAccessToken("personal"));
    }

    /// <summary>A second process has to see what the first one wrote — the file, not memory, is the store.</summary>
    [Fact]
    public void FindAccessToken_ReadsWhatAnEarlierRunWrote()
    {
        NewStore().SaveAccessToken("personal", "token-abc");

        Assert.Equal("token-abc", NewStore().FindAccessToken("personal"));
    }

    [Fact]
    public void FindAccessToken_ReadsAsAbsentBeforeAnyTokenHasBeenStored()
    {
        Assert.Null(NewStore().FindAccessToken("personal"));
    }

    [Fact]
    public void SaveAccessToken_KeepsEachProfilesTokenToItself()
    {
        var store = NewStore();

        store.SaveAccessToken("personal", "token-personal");
        store.SaveAccessToken("work", "token-work");

        Assert.Equal("token-personal", store.FindAccessToken("personal"));
        Assert.Equal("token-work", store.FindAccessToken("work"));
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
    public void DeleteAccessToken_TakesTheTokenOutOfTheFileAndSaysItDidSo()
    {
        var store = NewStore();
        store.SaveAccessToken("personal", "token-abc");
        store.SaveAccessToken("work", "token-work");

        Assert.True(store.DeleteAccessToken("personal"));
        Assert.Null(store.FindAccessToken("personal"));
        Assert.Equal("token-work", store.FindAccessToken("work"));
    }

    [Fact]
    public void DeleteAccessToken_SaysNothingWasThereWhenTheProfileHasNoToken()
    {
        Assert.False(NewStore().DeleteAccessToken("personal"));
    }

    /// <summary>
    ///     Storing secrets in the clear is already the weaker option; leaving them readable by every account on the
    ///     machine would make it indefensible.
    /// </summary>
    [Fact]
    [UnsupportedOSPlatform("windows")]
    public void SaveAccessToken_LeavesTheFileReadableOnlyByItsOwner()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "Windows has no Unix file mode; its ACLs inherit from the folder.");

        NewStore().SaveAccessToken("personal", "token-abc");

        var mode = File.GetUnixFileMode(Path.Combine(_directory.Path, "credentials.toml"));

        Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, mode);
    }

    [Fact]
    public void SaveAccessToken_CreatesTheConfigDirectoryTheFirstTimeItIsNeeded()
    {
        var nested = Path.Combine(_directory.Path, "not", "created", "yet");

        new PlaintextFileCredentialStore(new WoolyPaths(nested)).SaveAccessToken("personal", "token-abc");

        Assert.True(File.Exists(Path.Combine(nested, "credentials.toml")));
    }

    /// <summary>The whole point of this store is that the tradeoff it makes is visible rather than silent.</summary>
    [Fact]
    public void Storage_ReportsThatTheTokenIsSittingInTheClear()
    {
        Assert.Equal(CredentialStorage.PlaintextFile, NewStore().Storage);
    }

    private PlaintextFileCredentialStore NewStore() => new(new WoolyPaths(_directory.Path));
}
