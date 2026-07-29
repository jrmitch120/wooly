using Wooly.Core.Configuration;
using Wooly.Core.Credentials;
using Wooly.Core.Errors;

namespace Wooly.Core.Profiles;

/// <summary>
///     Keeps the two halves of a profile in step. Nothing here caches: the config file is small, and a user is invited
///     to hand-edit it between runs, so every question is answered from what is on disk now.
/// </summary>
/// <param name="paths">Only so a problem with the config file can name the file the user has to go and fix.</param>
public sealed class ProfileRegistry(IConfigStore configStore, ICredentialStore credentialStore, WoolyPaths paths)
    : IProfileRegistry
{
    /// <inheritdoc />
    public CredentialStorage TokenStorage => credentialStore.Storage;

    /// <inheritdoc />
    public IReadOnlyList<ProfileSummary> List()
    {
        var config = configStore.Load();

        // Ordered by name rather than by however the file happens to be written, so a script reading this output
        // twice gets the same answer twice.
        return config.Profiles
                     .OrderBy(entry => entry.Key, StringComparer.Ordinal)
                     .Select(entry => new ProfileSummary
                     {
                         Name = entry.Key,
                         Instance = entry.Value.Instance,
                         Account = entry.Value.Account,
                         IsCurrent = entry.Key == config.CurrentProfile,
                     })
                     .ToList();
    }

    /// <inheritdoc />
    public ProfileAddition Add(string name, ProfileConfig profile, string accessToken)
    {
        // The rule is enforced here, at the only door into the config file, rather than trusted to each caller. A
        // caller that got this far with a URL skipped its own validation, which is a defect in this client and not
        // something a user could have done — so it is an argument failure, and ADR-0006 prints it with a stack trace.
        if (!InstanceDomain.IsWellFormed(profile.Instance))
        {
            throw new ArgumentException(InstanceDomain.Rejection(profile.Instance), nameof(profile));
        }

        var config = configStore.Load();
        var profiles = new Dictionary<string, ProfileConfig>(config.Profiles, StringComparer.Ordinal);
        var replacedExisting = profiles.ContainsKey(name);

        profiles[name] = profile;

        // The token goes first. If storing it fails, the profile is not written either — better than a profile the
        // user can see and switch to that has nothing behind it. A token whose profile never got written is the
        // harmless way round: nothing points at it, and adding the profile again overwrites it.
        credentialStore.SaveAccessToken(name, accessToken);

        var isCurrent = config.CurrentProfile is null || config.CurrentProfile == name;

        configStore.Save(config with
        {
            CurrentProfile = isCurrent ? name : config.CurrentProfile,
            Profiles = profiles,
        });

        return new ProfileAddition(replacedExisting, isCurrent);
    }

    /// <inheritdoc />
    public void Switch(string name)
    {
        var config = configStore.Load();

        if (!config.Profiles.ContainsKey(name))
        {
            throw new UnknownProfileException(name, config.Profiles.Keys);
        }

        configStore.Save(config with { CurrentProfile = name });
    }

    /// <inheritdoc />
    public ActiveProfile Resolve(string? requestedName)
    {
        var config = configStore.Load();
        var name = requestedName ?? config.CurrentProfile ?? throw NothingToActAs(config);

        if (!config.Profiles.TryGetValue(name, out var profile))
        {
            // Which failure this is turns on where the name came from. A name the user just typed is a typo they can
            // fix; a name the config file gave is a problem in the file, and saying so is the only way they would
            // know to go and look at it.
            if (requestedName is not null)
            {
                throw new UnknownProfileException(name, config.Profiles.Keys);
            }

            throw new ConfigurationException(
                paths.ConfigFile,
                $"current_profile is '{name}', but no profile by that name is set up.");
        }

        // The file is meant to be hand-edited (ADR-0003), so a value that never passed through Add can still turn up
        // here. Caught now, it names the file and the line to fix; left alone, it surfaces later as a network error
        // against an address that was never reachable.
        if (!InstanceDomain.IsWellFormed(profile.Instance))
        {
            throw new ConfigurationException(
                paths.ConfigFile,
                $"profile '{name}' has instance '{profile.Instance}'. {InstanceDomain.Rejection(profile.Instance)}");
        }

        var accessToken = credentialStore.FindAccessToken(name)
                          ?? throw new AuthenticationException(
                              $"Profile '{name}' has no access token stored. Authenticate it again.");

        return new ActiveProfile
        {
            Name = name,
            Instance = profile.Instance,
            Account = profile.Account,
            AccessToken = accessToken,
        };
    }

    private static AuthenticationException NothingToActAs(WoolyConfig config) => new(
        config.Profiles.Count == 0
            ? "No profiles have been set up yet. Add one to connect this client to a Mastodon account."
            : $"No profile is current. Switch to one of: {string.Join(", ", config.Profiles.Keys.Order(StringComparer.Ordinal))} — or name one with --profile.");
}
