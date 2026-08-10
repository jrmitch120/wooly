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

    /// <summary>The hashtag the TUI's rail keeps a destination for.</summary>
    private const string HashtagKey = "hashtag";

    /// <summary>Whether a drawn picture's caption hides once the picture is actually drawn (#71).</summary>
    private const string HideDrawnCaptionKey = "hide_drawn_caption";

    /// <summary>The theme the TUI draws in, and the themes written in this same file (#46).</summary>
    private const string ThemeKey = "theme";

    private const string ThemesKey = "themes";
    private const string BackgroundKey = "background";
    private const string ForegroundKey = "foreground";

    /// <inheritdoc />
    public WoolyConfig Load()
    {
        var root = TomlFile.Read(paths.ConfigFile);

        return new WoolyConfig
        {
            CurrentProfile = ReadString(root, CurrentProfileKey),
            Profiles = ReadProfiles(root),
            Preferences = ReadPreferences(root),
            Theme = ReadString(root, ThemeKey),
            Themes = ReadThemes(root),
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

        // With the other loose key, and before every table, because TOML reads a key written after one as belonging
        // to it.
        if (config.Theme is not null)
        {
            root[ThemeKey] = config.Theme;
        }

        // Inserted before the profiles so the writer emits the short, general section above the long, per-profile one.
        if (config.Preferences is { DefaultVisibility: not null } or { Hashtag: not null } or { HideDrawnCaption: true })
        {
            var preferences = new TomlTable();

            if (config.Preferences.DefaultVisibility is { } visibility)
            {
                preferences[DefaultVisibilityKey] = PostVisibilityName.Of(visibility);
            }

            if (config.Preferences.Hashtag is { } hashtag)
            {
                preferences[HashtagKey] = hashtag;
            }

            // Absent rather than written as false: false is what an absent key already reads back as, and the reader
            // who never asked for this should not find a new line in a file nothing else touched.
            if (config.Preferences.HideDrawnCaption)
            {
                preferences[HideDrawnCaptionKey] = true;
            }

            root[PreferencesKey] = preferences;
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

        // Last, because a theme is the longest thing in the file and the shortest way to find anything else is for it
        // not to be above them.
        if (config.Themes.Count > 0)
        {
            var themes = new TomlTable();

            foreach (var (name, theme) in config.Themes)
            {
                themes[name] = Written(theme);
            }

            root[ThemesKey] = themes;
        }

        TomlFile.Write(paths.ConfigFile, TomlSerializer.Serialize(root));
    }

    /// <summary>
    ///     A theme as its table: the background, then every role that is one colour, then the roles that are a table
    ///     of their own.
    /// </summary>
    /// <remarks>
    ///     In that order because TOML reads a loose key written after a table as belonging to it — a role written
    ///     after <c>[themes.midnight.selection]</c> would come back as part of the selection.
    /// </remarks>
    private static TomlTable Written(ThemeConfig theme)
    {
        var written = new TomlTable();

        if (theme.Background is not null)
        {
            written[BackgroundKey] = theme.Background;
        }

        // A role saying neither is a role saying nothing, and is left out rather than written back as an empty colour.
        foreach (var (role, colour) in theme.Roles.Where(role => role.Value is { Foreground: not null, Background: null }))
        {
            written[role] = colour.Foreground!;
        }

        foreach (var (role, colour) in theme.Roles.Where(role => role.Value.Background is not null))
        {
            var pair = new TomlTable();

            if (colour.Foreground is not null)
            {
                pair[ForegroundKey] = colour.Foreground;
            }

            pair[BackgroundKey] = colour.Background!;
            written[role] = pair;
        }

        return written;
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

    /// <summary>
    ///     The themes written in the file. Every name a theme uses is carried through as written: this store knows
    ///     that a theme is names against colours, and the TUI knows which names are roles (#46).
    /// </summary>
    private Dictionary<string, ThemeConfig> ReadThemes(TomlTable root)
    {
        var themes = new Dictionary<string, ThemeConfig>(StringComparer.Ordinal);

        if (ReadTable(root, ThemesKey) is not { } stored)
        {
            return themes;
        }

        foreach (var name in stored.Keys)
        {
            var entry = ReadTable(stored, name)
                        ?? throw new ConfigurationException(paths.ConfigFile, $"theme '{name}' is not a section.");

            var roles = new Dictionary<string, ThemeRole>(StringComparer.Ordinal);

            foreach (var key in entry.Keys.Where(key => key != BackgroundKey))
            {
                roles[key] = ReadRole(entry[key], name, key);
            }

            themes[name] = new ThemeConfig
            {
                Background = ReadString(entry, BackgroundKey),
                Roles = roles,
            };
        }

        return themes;
    }

    /// <summary>
    ///     What a theme puts against one role: a colour, or a table naming a foreground, a background, or both.
    /// </summary>
    private ThemeRole ReadRole(object? written, string theme, string role)
    {
        if (written is string colour)
        {
            return new ThemeRole(colour);
        }

        if (written is not TomlTable pair)
        {
            // What it is not, rather than what a colour looks like: which words name a colour is the TUI's to say,
            // and saying it twice is how the two come to say different things.
            throw new ConfigurationException(
                paths.ConfigFile,
                $"theme '{theme}' gives '{role}' something that is neither a colour nor a table of "
                + $"'{ForegroundKey}' and '{BackgroundKey}'.");
        }

        // A stray key is said rather than ignored: somebody who wrote 'forground' meant something by it, and a theme
        // that drops what it cannot read leaves its author looking for a colour that never arrives.
        if (pair.Keys.FirstOrDefault(key => key != ForegroundKey && key != BackgroundKey) is { } stray)
        {
            throw new ConfigurationException(
                paths.ConfigFile,
                $"theme '{theme}' gives '{role}' a '{stray}', which is neither a foreground nor a background.");
        }

        return new ThemeRole(ReadString(pair, ForegroundKey), ReadString(pair, BackgroundKey));
    }

    private Preferences ReadPreferences(TomlTable root)
    {
        if (ReadTable(root, PreferencesKey) is not { } stored)
        {
            return new Preferences();
        }

        return new Preferences
        {
            DefaultVisibility = ReadVisibility(stored),
            Hashtag = ReadHashtag(stored),
            HideDrawnCaption = ReadBool(stored, HideDrawnCaptionKey),
        };
    }

    /// <summary>
    ///     The tag the rail keeps a place for. Held to the same one-word rule the <c>timeline tag</c> command holds a
    ///     typed one to, because a tag goes into a request path — see <see cref="Timelines.Hashtag" />.
    /// </summary>
    private string? ReadHashtag(TomlTable preferences)
    {
        if (ReadString(preferences, HashtagKey) is not { } raw)
        {
            return null;
        }

        return Timelines.Hashtag.IsWellFormed(raw)
            ? Timelines.Hashtag.Bare(raw)
            : throw new ConfigurationException(paths.ConfigFile, Timelines.Hashtag.Rejection(raw));
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

    private bool ReadBool(TomlTable table, string key) => table.TryGetValue(key, out var value)
        ? value as bool? ?? throw new ConfigurationException(paths.ConfigFile, $"'{key}' must be true or false.")
        : false;

    private TomlTable? ReadTable(TomlTable table, string key) => table.TryGetValue(key, out var value)
        ? value as TomlTable ?? throw new ConfigurationException(paths.ConfigFile, $"'{key}' must be a section.")
        : null;
}
