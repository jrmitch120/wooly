using Wooly.Core.Accounts;
using Wooly.Core.Profiles;

namespace Wooly.Tests.Fakes;

/// <summary>
///     The own account's public profile without an instance (#404). A test says what the read finds and what a save
///     answers or refuses with, and then asks what was saved.
/// </summary>
internal sealed class FakePublicProfileEditor : IPublicProfileEditor
{
    private PublicProfile _read;
    private readonly Account _saved;
    private readonly Exception? _refusal;
    private readonly Exception? _readRefusal;

    private FakePublicProfileEditor(PublicProfile read, Account saved, Exception? refusal, Exception? readRefusal = null)
    {
        _read = read;
        _saved = saved;
        _refusal = refusal;
        _readRefusal = readRefusal;
    }

    /// <summary>The profile every read asked as, by name, in order.</summary>
    public List<string> Reads { get; } = [];

    /// <summary>Every change a save sent, in order — where a test proves only what changed was sent.</summary>
    public List<PublicProfileChange> Saves { get; } = [];

    /// <summary>The access token every read and save went out with, in order (#243).</summary>
    public List<string> Tokens { get; } = [];

    /// <summary>A public profile read as <paramref name="read" />, whose saves answer <paramref name="saved" />.</summary>
    public static FakePublicProfileEditor Holding(PublicProfile? read = null, Account? saved = null)
    {
        read ??= APublicProfile.With();

        return new FakePublicProfileEditor(read, saved ?? read.Account, refusal: null);
    }

    /// <summary>A public profile whose every save is turned down with <paramref name="refusal" />.</summary>
    public static FakePublicProfileEditor Refusing(Exception refusal, PublicProfile? read = null)
    {
        read ??= APublicProfile.With();

        return new FakePublicProfileEditor(read, read.Account, refusal);
    }

    /// <summary>A public profile that cannot even be read: every read is turned down with <paramref name="refusal" />.</summary>
    public static FakePublicProfileEditor Unreadable(Exception refusal)
    {
        var read = APublicProfile.With();

        return new FakePublicProfileEditor(read, read.Account, refusal: null, readRefusal: refusal);
    }

    /// <summary>What every read finds from here on — the public profile changed on the web, say.</summary>
    public void NowReading(PublicProfile read) => _read = read;

    public Task<PublicProfile> Read(ActiveProfile profile, CancellationToken cancellationToken)
    {
        Reads.Add(profile.Name);
        Tokens.Add(profile.AccessToken);

        return _readRefusal is null ? Task.FromResult(_read) : Task.FromException<PublicProfile>(_readRefusal);
    }

    public Task<Account> Save(ActiveProfile profile, PublicProfileChange change, CancellationToken cancellationToken)
    {
        Saves.Add(change);
        Tokens.Add(profile.AccessToken);

        return _refusal is null ? Task.FromResult(_saved) : Task.FromException<Account>(_refusal);
    }
}
