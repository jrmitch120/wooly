using Wooly.Core;
using Wooly.Core.Posts;
using Wooly.Core.Configuration;
using Wooly.Core.Errors;
using Wooly.Tests.Fakes;

namespace Wooly.Tests.Core;

public class TomlConfigStoreTests : IDisposable
{
    private readonly TemporaryDirectory _directory = new();

    public void Dispose() => _directory.Dispose();

    [Fact]
    public void Load_ReadsAsEmptyBeforeAnythingHasBeenWritten()
    {
        var config = NewStore().Load();

        Assert.Null(config.CurrentProfile);
        Assert.Empty(config.Profiles);
        Assert.Null(config.Preferences.DefaultVisibility);
    }

    [Fact]
    public void Save_ThenLoad_RoundTripsProfilesCurrentProfileAndPreferences()
    {
        var store = NewStore();

        store.Save(new WoolyConfig
        {
            CurrentProfile = "work",
            Profiles = new Dictionary<string, ProfileConfig>
            {
                ["personal"] = new() { Instance = "mastodon.social", Account = "jeff@mastodon.social" },
                ["work"] = new() { Instance = "hachyderm.io" },
            },
            Preferences = new Preferences { DefaultVisibility = PostVisibility.Unlisted },
        });

        var config = store.Load();

        Assert.Equal("work", config.CurrentProfile);
        Assert.Equal(PostVisibility.Unlisted, config.Preferences.DefaultVisibility);
        Assert.Equal(["personal", "work"], config.Profiles.Keys.Order());
        Assert.Equal("mastodon.social", config.Profiles["personal"].Instance);
        Assert.Equal("jeff@mastodon.social", config.Profiles["personal"].Account);
        Assert.Equal("hachyderm.io", config.Profiles["work"].Instance);
        Assert.Null(config.Profiles["work"].Account);
    }

    /// <summary>The point of TOML over an opaque format is that a user can open the file and understand it.</summary>
    [Fact]
    public void Save_WritesTomlAPersonCanReadAndHandEdit()
    {
        NewStore().Save(new WoolyConfig
        {
            CurrentProfile = "personal",
            Profiles = new Dictionary<string, ProfileConfig>
            {
                ["personal"] = new() { Instance = "mastodon.social", Account = "jeff@mastodon.social" },
            },
            Preferences = new Preferences { DefaultVisibility = PostVisibility.Private },
        });

        var toml = File.ReadAllText(Path.Combine(_directory.Path, "config.toml"));

        Assert.Contains("current_profile = \"personal\"", toml);
        Assert.Contains("[preferences]", toml);
        Assert.Contains("default_visibility = \"private\"", toml);
        Assert.Contains("[profiles.personal]", toml);
        Assert.Contains("instance = \"mastodon.social\"", toml);
        Assert.Contains("account = \"jeff@mastodon.social\"", toml);
    }

    [Fact]
    public void Load_ReadsAFileWrittenByHand()
    {
        WriteConfigFile(
            """
            current_profile = "personal"

            [preferences]
            default_visibility = "direct"

            [profiles.personal]
            instance = "mastodon.social"
            """);

        var config = NewStore().Load();

        Assert.Equal("personal", config.CurrentProfile);
        Assert.Equal(PostVisibility.Direct, config.Preferences.DefaultVisibility);
        Assert.Equal("mastodon.social", config.Profiles["personal"].Instance);
    }

    [Fact]
    public void Save_ReplacesTheWholeFileRatherThanMergingIntoIt()
    {
        var store = NewStore();
        store.Save(new WoolyConfig
        {
            CurrentProfile = "personal",
            Profiles = new Dictionary<string, ProfileConfig> { ["personal"] = new() { Instance = "mastodon.social" } },
        });

        store.Save(WoolyConfig.Empty);

        var config = store.Load();
        Assert.Null(config.CurrentProfile);
        Assert.Empty(config.Profiles);
    }

    [Fact]
    public void Save_CreatesTheConfigDirectoryTheFirstTimeItIsNeeded()
    {
        var nested = Path.Combine(_directory.Path, "not", "created", "yet");
        var store = new TomlConfigStore(new WoolyPaths(nested));

        store.Save(WoolyConfig.Empty);

        Assert.True(File.Exists(Path.Combine(nested, "config.toml")));
    }

    [Fact]
    public void Load_ReportsWhereTheProblemIsWhenTheFileIsNotValidToml()
    {
        WriteConfigFile("current_profile = ");

        var exception = Assert.Throws<ConfigurationException>(() => NewStore().Load());

        Assert.Contains("config.toml", exception.Message);
    }

    [Fact]
    public void Load_ReportsAProfileMissingTheOneThingAProfileMustHave()
    {
        WriteConfigFile(
            """
            [profiles.personal]
            account = "jeff@mastodon.social"
            """);

        var exception = Assert.Throws<ConfigurationException>(() => NewStore().Load());

        Assert.Contains("personal", exception.Message);
        Assert.Contains("instance", exception.Message);
    }

    /// <summary>
    ///     A hand-editable file is only worth having if it says when it does not understand something. These are the
    ///     two spellings .NET's own enum parsing would wave through, and neither is anything a user meant to write.
    /// </summary>
    [Theory]
    [InlineData("1")]
    [InlineData("public,direct")]
    public void Load_RefusesASpellingOfVisibilityNoUserWouldHaveMeant(string written)
    {
        WriteConfigFile(
            $"""
             [preferences]
             default_visibility = "{written}"
             """);

        var exception = Assert.Throws<ConfigurationException>(() => NewStore().Load());

        Assert.Contains(written, exception.Message);
    }

    [Fact]
    public void Load_ListsTheVisibilitiesItAcceptsWhenGivenOneItDoesNot()
    {
        WriteConfigFile(
            """
            [preferences]
            default_visibility = "friends-only"
            """);

        var exception = Assert.Throws<ConfigurationException>(() => NewStore().Load());

        Assert.Contains("friends-only", exception.Message);
        Assert.Contains("unlisted", exception.Message);
    }

    /// <summary>
    ///     A theme is a table in this same file (ADR-0003, #46). What the colours mean is the TUI's to say; what this
    ///     store owes is the shape — which theme was chosen, and what each one puts against each name it uses.
    /// </summary>
    [Fact]
    public void Load_ReadsTheChosenThemeAndTheThemesWrittenBesideIt()
    {
        WriteConfigFile(
            """
            theme = "midnight"

            [themes.midnight]
            background = "#12111a"
            body = "#d5d2e0"
            rail-unread = "bright-red"

            [themes.midnight.selection]
            foreground = "#f2f0f7"
            background = "#2a2942"
            """);

        var config = NewStore().Load();

        Assert.Equal("midnight", config.Theme);

        var midnight = config.Themes["midnight"];

        Assert.Equal("#12111a", midnight.Background);
        Assert.Equal(new ThemeRole("#d5d2e0"), midnight.Roles["body"]);
        Assert.Equal(new ThemeRole("bright-red"), midnight.Roles["rail-unread"]);
        Assert.Equal(new ThemeRole("#f2f0f7", "#2a2942"), midnight.Roles["selection"]);
    }

    /// <summary>
    ///     A role that names only a background is a role naming what it wants and leaving the rest to the theme, not a
    ///     half-written one.
    /// </summary>
    [Fact]
    public void Load_ReadsARoleThatNamesOnlyABackground()
    {
        WriteConfigFile(
            """
            [themes.midnight.selection]
            background = "#2a2942"
            """);

        Assert.Equal(new ThemeRole(null, "#2a2942"), NewStore().Load().Themes["midnight"].Roles["selection"]);
    }

    /// <summary>
    ///     Every command that writes this file writes the whole of it, so a theme somebody hand-wrote has to survive a
    ///     profile being added — otherwise the first <c>profile add</c> after theming quietly deletes their theme.
    /// </summary>
    [Fact]
    public void Save_ThenLoad_RoundTripsTheThemesSomebodyWroteByHand()
    {
        var store = NewStore();

        store.Save(new WoolyConfig
        {
            CurrentProfile = "personal",
            Theme = "midnight",
            Themes = new Dictionary<string, ThemeConfig>
            {
                ["midnight"] = new()
                {
                    Background = "#12111a",
                    Roles = new Dictionary<string, ThemeRole>
                    {
                        ["body"] = new("#d5d2e0"),
                        ["selection"] = new("#f2f0f7", "#2a2942"),
                        ["rail-unread"] = new(null, "bright-red"),
                    },
                },
            },
            Profiles = new Dictionary<string, ProfileConfig> { ["personal"] = new() { Instance = "hachyderm.io" } },
        });

        var config = store.Load();

        Assert.Equal("midnight", config.Theme);
        Assert.Equal("#12111a", config.Themes["midnight"].Background);
        Assert.Equal(new ThemeRole("#d5d2e0"), config.Themes["midnight"].Roles["body"]);
        Assert.Equal(new ThemeRole("#f2f0f7", "#2a2942"), config.Themes["midnight"].Roles["selection"]);
        Assert.Equal(new ThemeRole(null, "bright-red"), config.Themes["midnight"].Roles["rail-unread"]);
        Assert.Equal("hachyderm.io", config.Profiles["personal"].Instance);
    }

    /// <summary>The theme somebody wrote is theirs to read afterwards, so it goes back as the tables they wrote.</summary>
    [Fact]
    public void Save_WritesAThemeAsTheTableItWasWrittenAs()
    {
        NewStore().Save(new WoolyConfig
        {
            Theme = "midnight",
            Themes = new Dictionary<string, ThemeConfig>
            {
                ["midnight"] = new()
                {
                    Background = "#12111a",
                    Roles = new Dictionary<string, ThemeRole> { ["selection"] = new("#f2f0f7", "#2a2942") },
                },
            },
        });

        var toml = File.ReadAllText(Path.Combine(_directory.Path, "config.toml"));

        Assert.Contains("theme = \"midnight\"", toml);
        Assert.Contains("[themes.midnight]", toml);
        Assert.Contains("background = \"#12111a\"", toml);
        Assert.Contains("[themes.midnight.selection]", toml);
        Assert.Contains("foreground = \"#f2f0f7\"", toml);
    }

    /// <summary>
    ///     A theme is a table of colours and nothing else, so a value that is neither a colour nor a role's own table
    ///     is said out loud with the theme and the key named — the same rule the rest of this file is read by.
    /// </summary>
    [Fact]
    public void Load_ReportsAThemeGivingARoleSomethingThatIsNotAColour()
    {
        WriteConfigFile(
            """
            [themes.midnight]
            body = 12
            """);

        var exception = Assert.Throws<ConfigurationException>(() => NewStore().Load());

        Assert.Contains("midnight", exception.Message);
        Assert.Contains("body", exception.Message);
    }

    /// <summary>A role's own table holds a foreground and a background; a third key is a typo worth saying.</summary>
    [Fact]
    public void Load_ReportsAKeyARolesOwnTableHasNoMeaningFor()
    {
        WriteConfigFile(
            """
            [themes.midnight.selection]
            forground = "#f2f0f7"
            """);

        var exception = Assert.Throws<ConfigurationException>(() => NewStore().Load());

        Assert.Contains("selection", exception.Message);
        Assert.Contains("forground", exception.Message);
    }

    private TomlConfigStore NewStore() => new(new WoolyPaths(_directory.Path));

    private void WriteConfigFile(string toml) => File.WriteAllText(Path.Combine(_directory.Path, "config.toml"), toml);
}
