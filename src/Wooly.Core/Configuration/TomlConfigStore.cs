using Tomlyn;
using Tomlyn.Model;
using Wooly.Core.Errors;
using Wooly.Core.Posts;

namespace Wooly.Core.Configuration;

/// <summary>
///     The config file, read and written as TOML (ADR-0003). Keys are mapped by hand rather than by reflection so the
///     on-disk names stay a deliberate, stable contract with whoever opens the file to edit it.
/// </summary>
public sealed class TomlConfigStore(WoolyPaths paths) : IConfigStore
{
    private const string CurrentProfileKey = "current_profile";
    private const string ProfilesKey = "profiles";
    private const string PreferencesKey = "preferences";
    private const string InstanceKey = "instance";
    private const string AccountKey = "account";
    private const string DefaultVisibilityKey = "default_visibility";

    /// <inheritdoc />
    public WoolyConfig Load()
    {
        var root = TomlFile.Read(paths.ConfigFile);

        return new WoolyConfig
        {
            CurrentProfile = ReadString(root, CurrentProfileKey),
            Profiles = ReadProfiles(root),
            Preferences = ReadPreferences(root),
        };
    }

    /// <inheritdoc />
    public void Save(WoolyConfig config)
    {
        var root = new TomlTable();

        if (config.CurrentProfile is not null)
        {
            root[CurrentProfileKey] = config.CurrentProfile;
        }

        // Inserted before the profiles so the writer emits the short, general section above the long, per-profile one.
        if (config.Preferences.DefaultVisibility is { } visibility)
        {
            root[PreferencesKey] = new TomlTable { [DefaultVisibilityKey] = PostVisibilityName.Of(visibility) };
        }

        if (config.Profiles.Count > 0)
        {
            var profiles = new TomlTable();

            foreach (var (name, profile) in config.Profiles)
            {
                var entry = new TomlTable { [InstanceKey] = profile.Instance };

                if (profile.Account is not null)
                {
                    entry[AccountKey] = profile.Account;
                }

                profiles[name] = entry;
            }

            root[ProfilesKey] = profiles;
        }

        TomlFile.Write(paths.ConfigFile, TomlSerializer.Serialize(root));
    }

    private Dictionary<string, ProfileConfig> ReadProfiles(TomlTable root)
    {
        var profiles = new Dictionary<string, ProfileConfig>(StringComparer.Ordinal);

        if (ReadTable(root, ProfilesKey) is not { } stored)
        {
            return profiles;
        }

        foreach (var name in stored.Keys)
        {
            var entry = ReadTable(stored, name)
                        ?? throw new ConfigurationException(paths.ConfigFile, $"profile '{name}' is not a section.");

            profiles[name] = new ProfileConfig
            {
                Instance = ReadString(entry, InstanceKey)
                           ?? throw new ConfigurationException(
                               paths.ConfigFile,
                               $"profile '{name}' does not say which instance it belongs to. Add an 'instance' key."),
                Account = ReadString(entry, AccountKey),
            };
        }

        return profiles;
    }

    private Preferences ReadPreferences(TomlTable root)
    {
        if (ReadTable(root, PreferencesKey) is not { } stored)
        {
            return new Preferences();
        }

        return new Preferences { DefaultVisibility = ReadVisibility(stored) };
    }

    private PostVisibility? ReadVisibility(TomlTable preferences)
    {
        if (ReadString(preferences, DefaultVisibilityKey) is not { } raw)
        {
            return null;
        }

        // The same spellings the --visibility flag takes, asked of the same place, so that a word this client accepts
        // on the command line is never turned down in the file (or the other way about).
        return PostVisibilityName.Parse(raw)
               ?? throw new ConfigurationException(paths.ConfigFile, PostVisibilityName.Rejection(raw));
    }

    private string? ReadString(TomlTable table, string key) => table.TryGetValue(key, out var value)
        ? value as string ?? throw new ConfigurationException(paths.ConfigFile, $"'{key}' must be text.")
        : null;

    private TomlTable? ReadTable(TomlTable table, string key) => table.TryGetValue(key, out var value)
        ? value as TomlTable ?? throw new ConfigurationException(paths.ConfigFile, $"'{key}' must be a section.")
        : null;
}
