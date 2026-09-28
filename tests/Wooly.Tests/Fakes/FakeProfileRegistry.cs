using Wooly.Core.Configuration;
using Wooly.Core.Credentials;
using Wooly.Core.Errors;
using Wooly.Core.Profiles;

namespace Wooly.Tests.Fakes;

/// <summary>
///     This machine's profiles without a config file or a keyring: the list a test says is set up, and the store it
///     says the tokens are in. The TUI's side of the registry, where <c>ProfileRegistryTests</c> exercises the real one
///     over both stores.
/// </summary>
/// <remarks>
///     What the profiles screen reads, the writes that have keys so far — adding, as the add screen does (#245), and
///     making a profile the default, as <c>D</c> does (#244) — and resolving, which a switch does (#243). The rest arrive
///     with the keys that make them (#238), and until then a shell that reached for one would be doing something no
///     ticket has asked it to.
/// </remarks>
internal sealed class FakeProfileRegistry(IReadOnlyList<ProfileSummary> profiles) : IProfileRegistry
{
    private readonly List<ProfileSummary> _profiles = [.. profiles];

    /// <summary>
    ///     Every profile written, in order, with the token it was written with — where a test proves what was stored.
    /// </summary>
    public List<(string Name, ProfileConfig Profile, string AccessToken)> Added { get; } = [];

    /// <summary>Every profile made the default, in order — where a test proves what was written.</summary>
    public List<string> Switched { get; } = [];

    /// <summary>Every profile removed, in order — where a test proves what was written.</summary>
    public List<string> Removed { get; } = [];

    /// <summary>
    ///     The profiles whose token is missing from the store, which <see cref="Resolve" /> refuses as the real one does.
    /// </summary>
    public HashSet<string> Tokenless { get; } = [];

    /// <inheritdoc />
    public CredentialStorage TokenStorage { get; set; } = CredentialStorage.OsKeyring;

    /// <summary>
    ///     <paramref name="profiles" />, with <paramref name="current" /> the default profile — or none, where
    ///     it is <see langword="null" />.
    /// </summary>
    public static FakeProfileRegistry Holding(string? current, params ProfileSummary[] profiles) =>
        new([.. profiles.Select(profile => profile with { IsCurrent = profile.Name == current })]);

    /// <summary>One profile as the list reports it, named <paramref name="name" />.</summary>
    public static ProfileSummary Profile(string name, string instance, string? account) => new()
    {
        Name = name,
        Instance = instance,
        Account = account,
        IsCurrent = false,
    };

    /// <inheritdoc />
    public IReadOnlyList<ProfileSummary> List() =>
        [.. _profiles.OrderBy(profile => profile.Name, StringComparer.Ordinal)];

    /// <inheritdoc />
    /// <remarks>
    ///     Decides what the real one decides — a name taken is replaced, and the profile becomes the default only where
    ///     none was — so a shell reading the list back sees what it would see over the config file.
    /// </remarks>
    public ProfileAddition Add(string name, ProfileConfig profile, string accessToken)
    {
        Added.Add((name, profile, accessToken));

        var replaced = _profiles.RemoveAll(held => held.Name == name) > 0;
        var isCurrent = _profiles.All(held => !held.IsCurrent);

        _profiles.Add(new ProfileSummary
        {
            Name = name,
            Instance = profile.Instance,
            Account = profile.Account,
            IsCurrent = isCurrent,
        });

        return new ProfileAddition(replaced, isCurrent);
    }

    /// <inheritdoc />
    /// <remarks>Moves the default as the real one does, so a shell reading the list back sees it moved.</remarks>
    public void Switch(string name)
    {
        if (_profiles.All(held => held.Name != name))
        {
            throw new UnknownProfileException(name, [.. _profiles.Select(held => held.Name)]);
        }

        Switched.Add(name);

        for (var at = 0; at < _profiles.Count; at++)
        {
            _profiles[at] = _profiles[at] with { IsCurrent = _profiles[at].Name == name };
        }
    }

    /// <inheritdoc />
    /// <remarks>Refuses the default, as the real one does.</remarks>
    public ProfileRemoval Remove(string name)
    {
        var removed = _profiles.SingleOrDefault(held => held.Name == name)
                      ?? throw new UnknownProfileException(name, [.. _profiles.Select(held => held.Name)]);

        if (removed.IsCurrent)
        {
            throw new DefaultProfileRemovalException(name);
        }

        Removed.Add(name);
        _profiles.Remove(removed);

        return new ProfileRemoval(new ProfileConfig { Instance = removed.Instance, Account = removed.Account });
    }

    /// <inheritdoc />
    /// <remarks>
    ///     With <c>token-</c> and its name for its token, so a test can tell whose token a call went out with. No name is
    ///     the default, and no default is nobody to act as — refused as the real one refuses it.
    /// </remarks>
    public ActiveProfile Resolve(string? requestedName)
    {
        var name = requestedName
                   ?? _profiles.SingleOrDefault(held => held.IsCurrent)?.Name
                   ?? throw new AuthenticationException("No profiles have been set up yet.");
        var profile = _profiles.SingleOrDefault(held => held.Name == name)
                      ?? throw new UnknownProfileException(name, [.. _profiles.Select(held => held.Name)]);

        if (Tokenless.Contains(name))
        {
            throw new AuthenticationException($"Profile '{name}' has no access token stored. Authenticate it again.");
        }

        return new ActiveProfile
        {
            Name = name,
            Instance = profile.Instance,
            Account = profile.Account,
            AccessToken = $"token-{name}",
        };
    }
}
