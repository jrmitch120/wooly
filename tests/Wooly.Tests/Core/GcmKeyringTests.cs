using Wooly.Core.Credentials;
using Wooly.Tests.Fakes;

namespace Wooly.Tests.Core;

/// <summary>
///     Covers this client choosing its own backing store rather than inheriting Git's, without going near the
///     developer's real Git or GCM configuration: the pin is observed through the process environment it writes,
///     which the test owns and puts back.
/// </summary>
[Collection(nameof(GcmEnvironmentCollection))]
public class GcmKeyringTests : IDisposable
{
    private readonly TemporaryEnvironmentVariable _configuredForGit = new(GcmKeyring.BackingStoreVariable);

    public void Dispose() => _configuredForGit.Dispose();

    /// <summary>
    ///     The names are GCM's, not this client's, so they are spelled out here rather than read back from the code
    ///     under test — a rename on either side has to fail somewhere.
    /// </summary>
    [Fact]
    public void BackingStoreForThisMachine_NamesTheKeyringThisPlatformActuallyHas()
    {
        var expected = OperatingSystem.IsWindows() ? "wincredman"
            : OperatingSystem.IsMacOS() ? "keychain"
            : "secretservice";

        Assert.Equal(expected, GcmKeyring.BackingStoreForThisMachine);
    }

    [Fact]
    public void PinBackingStore_OverridesAStoreThatWasConfiguredForGit()
    {
        _configuredForGit.Value = "plaintext";

        GcmKeyring.PinBackingStore();

        Assert.Equal(GcmKeyring.BackingStoreForThisMachine, _configuredForGit.Value);
    }

    [Fact]
    public void PinBackingStore_NamesAStoreEvenWhenNothingWasConfiguredAtAll()
    {
        _configuredForGit.Value = null;

        GcmKeyring.PinBackingStore();

        Assert.Equal(GcmKeyring.BackingStoreForThisMachine, _configuredForGit.Value);
    }
}
