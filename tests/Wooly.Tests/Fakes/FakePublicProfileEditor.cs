using Wooly.Core.Accounts;
using Wooly.Core.Profiles;

namespace Wooly.Tests.Fakes;

/// <summary>
///     The own account's public profile without an instance (#404). A test says what the read finds and what a save
///     answers or refuses with, and then asks what was saved.
/// </summary>
internal sealed class FakePublicProfileEditor : IPublicProfileEditor
{
    private readonly PublicProfile _read;
    private readonly Account _saved;
    private readonly Exception? _refusal;

    private FakePublicProfileEditor(PublicProfile read, Account saved, Exception? refusal)
    {
        _read = read;
        _saved = saved;
        _refusal = refusal;
    }

    /// <summary>The profile every read asked as, by name, in order.</summary>
    public List<string> Reads { get; } = [];

    /// <summary>Every change a save sent, in order — where a test proves only what changed was sent.</summary>
    public List<PublicProfileChange> Saves { get; } = [];

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

    public Task<PublicProfile> Read(ActiveProfile profile, CancellationToken cancellationToken)
    {
        Reads.Add(profile.Name);

        return Task.FromResult(_read);
    }

    public Task<Account> Save(ActiveProfile profile, PublicProfileChange change, CancellationToken cancellationToken)
    {
        Saves.Add(change);

        return _refusal is null ? Task.FromResult(_saved) : Task.FromException<Account>(_refusal);
    }
}
