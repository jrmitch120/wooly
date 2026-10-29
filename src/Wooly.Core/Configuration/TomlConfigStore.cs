using System.Globalization;
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
    private const string DefaultLanguageKey = "default_language";

    /// <summary>The hashtag the TUI's rail keeps a destination for.</summary>
    private const string HashtagKey = "hashtag";

    /// <summary>Whether a drawn picture's caption hides once the picture is actually drawn (#71).</summary>
    private const string HideDrawnCaptionKey = "hide_drawn_caption";

    /// <summary>Whether a rising Inbox badge rings the terminal's bell (#399).</summary>
    private const string BellKey = "bell";

    /// <summary>How much of the terminal's width the TUI's shell takes, as a percentage (#413).</summary>
    private const string ShellWidthKey = "shell_width";

    /// <summary>Whether the TUI's rail is the hidden rail rather than the shown one (#442, #445).</summary>
    private const string HiddenRailKey = "hidden_rail";

    /// <summary>
    ///     How the TUI's content panel draws its scrollbar's thumb where a screen's rows don't fit, or that it draws
    ///     none (#500, #501, #505).
    /// </summary>
    private const string ScrollbarKey = "scrollbar";

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
        if (config.Preferences is { DefaultVisibility: not null } or { DefaultLanguage: not null } or { Hashtag: not null }
            or { HideDrawnCaption: true } or { Bell: false } or { ShellWidth: not Preferences.FullShellWidth }
            or { HiddenRail: true } or { Scrollbar: not ScrollbarStyle.Block })
        {
            var preferences = new TomlTable();

            if (config.Preferences.DefaultVisibility is { } visibility)
            {
                preferences[DefaultVisibilityKey] = PostVisibilityName.Of(visibility);
            }

            if (config.Preferences.DefaultLanguage is { } language)
            {
                preferences[DefaultLanguageKey] = language;
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

            // Absent rather than written as true, for the same reason the other way round: on is what an absent key
            // reads back as (#399).
            if (!config.Preferences.Bell)
            {
                preferences[BellKey] = false;
            }

            // Absent rather than written as 100, the whole terminal an absent key reads back as — so widening back to it
            // takes the line away again (#415).
            if (config.Preferences.ShellWidth != Preferences.FullShellWidth)
            {
                preferences[ShellWidthKey] = (long)config.Preferences.ShellWidth;
            }

            // Absent rather than written as false, the shown rail an absent key reads back as — so showing it again
            // takes the line away, as widening back to the whole terminal does (#442, #445).
            if (config.Preferences.HiddenRail)
            {
                preferences[HiddenRailKey] = true;
            }

            // Absent rather than written as "block", the thumb an absent key reads back as, as the bell is (#501, #505).
            if (config.Preferences.Scrollbar is not ScrollbarStyle.Block)
            {
                preferences[ScrollbarKey] = ScrollbarStyleName.Of(config.Preferences.Scrollbar);
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
            DefaultLanguage = ReadLanguage(stored),
            Hashtag = ReadHashtag(stored),
            HideDrawnCaption = ReadBool(stored, HideDrawnCaptionKey),
            Bell = ReadBool(stored, BellKey, whenAbsent: true),
            ShellWidth = ReadShellWidth(stored),
            HiddenRail = ReadBool(stored, HiddenRailKey),
            Scrollbar = ReadScrollbar(stored),
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

    /// <summary>
    ///     The code of the language a new post is in, read through the same <see cref="PostLanguageName" /> the
    ///     <c>--language</c> flag asks, so a name written by hand reads as the code it names.
    /// </summary>
    private string? ReadLanguage(TomlTable preferences)
    {
        if (ReadString(preferences, DefaultLanguageKey) is not { } raw)
        {
            return null;
        }

        return PostLanguageName.Parse(raw)?.Code
               ?? throw new ConfigurationException(paths.ConfigFile, PostLanguageName.Rejection(raw));
    }

    /// <summary>
    ///     The scrollbar's style (#505), one of <see cref="ScrollbarStyleName" />'s five spellings and nothing else — the
    ///     booleans #501 took before either was released refused with the rest.
    /// </summary>
    private ScrollbarStyle ReadScrollbar(TomlTable preferences)
    {
        if (!preferences.TryGetValue(ScrollbarKey, out var value))
        {
            return ScrollbarStyle.Block;
        }

        // A boolean or a number is quoted as the file spells it, and is never one of the five words.
        var raw = value switch
        {
            string name => name,
            bool flag => flag ? "true" : "false",
            _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty,
        };

        return ScrollbarStyleName.Parse(raw)
               ?? throw new ConfigurationException(paths.ConfigFile, ScrollbarStyleName.Rejection(raw));
    }

    private string? ReadString(TomlTable table, string key) => table.TryGetValue(key, out var value)
        ? value as string ?? throw new ConfigurationException(paths.ConfigFile, $"'{key}' must be text.")
        : null;

    private bool ReadBool(TomlTable table, string key, bool whenAbsent = false) => table.TryGetValue(key, out var value)
        ? value as bool? ?? throw new ConfigurationException(paths.ConfigFile, $"'{key}' must be true or false.")
        : whenAbsent;

    /// <summary>
    ///     The shell width as written, an in-between value such as 73 included, but brought into range rather than
    ///     refused: a typo there is not worth the TUI refusing to start over (#415).
    /// </summary>
    private int ReadShellWidth(TomlTable preferences) => preferences.TryGetValue(ShellWidthKey, out var value)
        ? value as long? is { } percent
            ? (int)Math.Clamp(percent, Preferences.NarrowestShellWidth, Preferences.FullShellWidth)
            : throw new ConfigurationException(paths.ConfigFile, $"'{ShellWidthKey}' must be a whole number.")
        : Preferences.FullShellWidth;

    private TomlTable? ReadTable(TomlTable table, string key) => table.TryGetValue(key, out var value)
        ? value as TomlTable ?? throw new ConfigurationException(paths.ConfigFile, $"'{key}' must be a section.")
        : null;
}
