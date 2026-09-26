using Wooly.Core.Configuration;
using Wooly.Core.Credentials;
using Wooly.Core.Profiles;

namespace Wooly.Tests.Fakes;

/// <summary>
///     This machine's profiles without a config file or a keyring: the list a test says is set up, and the store it
///     says the tokens are in. The TUI's side of the registry, where <c>ProfileRegistryTests</c> exercises the real one
///     over both stores.
/// </summary>
/// <remarks>
///     Only what the profiles screen reads so far. The writes arrive with the keys that make them (#238), and until
///     then a shell that reached for one would be doing something no ticket has asked it to.
/// </remarks>
internal sealed class FakeProfileRegistry(IReadOnlyList<ProfileSummary> profiles) : IProfileRegistry
{
    /// <inheritdoc />
    public CredentialStorage TokenStorage { get; set; } = CredentialStorage.OsKeyring;

    /// <summary>
    ///     <paramref name="profiles" />, with <paramref name="current" /> the one commands default to — or none, where
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
        [.. profiles.OrderBy(profile => profile.Name, StringComparer.Ordinal)];

    /// <inheritdoc />
    public ProfileAddition Add(string name, ProfileConfig profile, string accessToken) =>
        throw new NotSupportedException("Nothing in the TUI adds a profile yet.");

    /// <inheritdoc />
    public void Switch(string name) => throw new NotSupportedException("Nothing in the TUI makes a profile current yet.");

    /// <inheritdoc />
    public ProfileRemoval Remove(string name) =>
        throw new NotSupportedException("Nothing in the TUI removes a profile yet.");

    /// <inheritdoc />
    public ActiveProfile Resolve(string? requestedName) =>
        throw new NotSupportedException("Nothing in the TUI resolves a profile yet.");
}
