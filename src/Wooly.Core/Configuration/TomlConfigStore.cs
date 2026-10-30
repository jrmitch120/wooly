using System.Globalization;
using Tomlyn;
using Tomlyn.Model;
using Wooly.Core.Errors;
using Wooly.Core.Posts;
using Wooly.Core.Timelines;

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

    /// <summary>
    ///     The reader's tag sets (ADR-0033): an array of tables rather than a table keyed by name, because a set's
    ///     place on the rail is its place in the file, and a table's keys are what a save writes in its own order.
    /// </summary>
    private const string TagSetsKey = "tag_sets";

    private const string TagSetNameKey = "name";
    private const string TagSetTagsKey = "tags";

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
            Preferences = ReadPreferences(root) with { TagSets = ReadTagSets(root) },
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
        // Its keys go in alphabetical order, so a reader looking for one knows where it is. Every preference with a
        // default is written, at its default too, so that a reader opening a file nobody has tuned finds each setting
        // they can change in it (#506). The language and visibility have none — absent is the server's own, and TOML
        // has no null — so they are written only once set. An old 'hashtag' is not carried: the file is rewritten
        // whole, so the save after it is read is what drops it (ADR-0033).
        var preferences = new TomlTable { [BellKey] = config.Preferences.Bell };

        if (config.Preferences.DefaultLanguage is { } language)
        {
            preferences[DefaultLanguageKey] = language;
        }

        if (config.Preferences.DefaultVisibility is { } visibility)
        {
            preferences[DefaultVisibilityKey] = PostVisibilityName.Of(visibility);
        }

        preferences[HiddenRailKey] = config.Preferences.HiddenRail;
        preferences[HideDrawnCaptionKey] = config.Preferences.HideDrawnCaption;
        preferences[ScrollbarKey] = ScrollbarStyleName.Of(config.Preferences.Scrollbar);
        preferences[ShellWidthKey] = (long)config.Preferences.ShellWidth;

        root[PreferencesKey] = preferences;

        // Written apart and put straight after the preferences, in the order read: the writer puts an array of tables
        // after every table in the one it is given, which would leave a reader's sets under the longest thing in the
        // file. No sets is no [[tag_sets]] at all.
        var sets = new TomlTable();

        if (config.Preferences.TagSets.Count > 0)
        {
            var written = new TomlTableArray();

            foreach (var set in config.Preferences.TagSets)
            {
                var tags = new TomlArray();

                foreach (var tag in set.Tags)
                {
                    tags.Add(tag);
                }

                written.Add(new TomlTable { [TagSetNameKey] = set.Name, [TagSetTagsKey] = tags });
            }

            sets[TagSetsKey] = written;
        }

        var rest = new TomlTable();

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

            rest[ProfilesKey] = profiles;
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

            rest[ThemesKey] = themes;
        }

        var parts = new[] { root, sets, rest }.Where(part => part.Count > 0);

        TomlFile.Write(paths.ConfigFile, string.Join("\n", parts.Select(part => TomlSerializer.Serialize(part))));
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
            HideDrawnCaption = ReadBool(stored, HideDrawnCaptionKey),
            Bell = ReadBool(stored, BellKey, whenAbsent: true),
            ShellWidth = ReadShellWidth(stored),
            HiddenRail = ReadBool(stored, HiddenRailKey),
            Scrollbar = ReadScrollbar(stored),
        };
    }

    /// <summary>
    ///     The reader's tag sets, in file order. Each is held to a set's rules (<see cref="TagSet.Rejection" />) —
    ///     every tag one word, as the <c>timeline tag</c> command holds a typed one to, because a tag goes into a
    ///     request path — and the whole file refused for one that breaks them, or for a name two sets share, which
    ///     would put two entries on the rail nobody could tell apart. A tag written with its <c>#</c> or twice is
    ///     folded rather than refused, since there is no doubt what was meant.
    /// </summary>
    private IReadOnlyList<TagSet> ReadTagSets(TomlTable root)
    {
        if (!root.TryGetValue(TagSetsKey, out var value))
        {
            return [];
        }

        if (value is not TomlTableArray written)
        {
            throw new ConfigurationException(
                paths.ConfigFile,
                $"'{TagSetsKey}' must be a list of sets, each written under its own [[{TagSetsKey}]].");
        }

        var sets = new List<TagSet>();

        foreach (var entry in written)
        {
            var name = ReadString(entry, TagSetNameKey);
            var tags = ReadTags(entry);

            if (TagSet.Rejection(name, tags) is { } problem)
            {
                throw new ConfigurationException(paths.ConfigFile, problem);
            }

            if (sets.Any(set => set.Name == name))
            {
                throw new ConfigurationException(
                    paths.ConfigFile,
                    $"more than one tag set is called '{name}'. Give each a name of its own.");
            }

            sets.Add(TagSet.Of(name!, tags));
        }

        return sets;
    }

    /// <summary>A set's tags as written, or none where it names none.</summary>
    private List<string> ReadTags(TomlTable set)
    {
        if (!set.TryGetValue(TagSetTagsKey, out var value))
        {
            return [];
        }

        return value is TomlArray tags && tags.All(tag => tag is string)
            ? [.. tags.Cast<string>()]
            : throw new ConfigurationException(paths.ConfigFile, $"'{TagSetTagsKey}' must be a list of hashtags.");
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
