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
        Assert.False(config.Preferences.HideDrawnCaption);
    }

    /// <summary>#71: absent reads as <see langword="false" />, today's behavior — a caption always shows.</summary>
    [Fact]
    public void Load_ReadsHideDrawnCaptionAsFalseWhenAbsent()
    {
        WriteConfigFile(
            """
            [preferences]
            hashtag = "dotnet"
            """);

        Assert.False(NewStore().Load().Preferences.HideDrawnCaption);
    }

    [Fact]
    public void Save_ThenLoad_RoundTripsHideDrawnCaption()
    {
        var store = NewStore();

        store.Save(new WoolyConfig { Preferences = new Preferences { HideDrawnCaption = true } });

        Assert.True(store.Load().Preferences.HideDrawnCaption);
    }

    /// <summary>
    ///     #506: every preference with a default is written at it, in a fixed order, so a reader opening a file nobody
    ///     has tuned sees each setting they can change — and only those: the three with no default stay absent.
    /// </summary>
    [Fact]
    public void Save_WritesEveryPreferenceWithADefaultAtItsDefault()
    {
        NewStore().Save(WoolyConfig.Empty);

        Assert.Equal(
            """
            [preferences]
            bell = true
            hide_drawn_caption = false
            shell_width = 100
            hidden_rail = false
            scrollbar = "block"

            """,
            File.ReadAllText(Path.Combine(_directory.Path, "config.toml")).ReplaceLineEndings("\n"));
    }

    /// <summary>
    ///     A file written by hand without those keys gains them on the next save, before the profiles, and means what
    ///     it meant: every value reads back as it did.
    /// </summary>
    [Fact]
    public void Save_GivesAFileWithoutThePreferencesThemAtTheirDefaults_ChangingNothingItMeans()
    {
        WriteConfigFile(
            """
            current_profile = "personal"

            [preferences]
            hashtag = "dotnet"
            bell = false

            [profiles.personal]
            instance = "mastodon.social"
            """);

        var store = NewStore();
        var before = store.Load();

        store.Save(before);

        var toml = File.ReadAllText(Path.Combine(_directory.Path, "config.toml"));
        var preferencesAt = toml.IndexOf("[preferences]", StringComparison.Ordinal);
        var after = store.Load();

        Assert.Contains("hidden_rail = false", toml);
        Assert.True(preferencesAt >= 0 && preferencesAt < toml.IndexOf("[profiles", StringComparison.Ordinal));
        Assert.Equal(before.Preferences, after.Preferences);
        Assert.Equal(before.CurrentProfile, after.CurrentProfile);
        Assert.Equal("mastodon.social", after.Profiles["personal"].Instance);
    }

    /// <summary>
    ///     The visibility, language and hashtag have no default to write — absent means the server's own, or none, and
    ///     TOML has no null — so they are written only once set.
    /// </summary>
    [Fact]
    public void Save_LeavesOutThePreferencesWithNoDefaultUntilSet()
    {
        NewStore().Save(WoolyConfig.Empty);

        var toml = File.ReadAllText(Path.Combine(_directory.Path, "config.toml"));

        Assert.DoesNotContain("default_visibility", toml);
        Assert.DoesNotContain("default_language", toml);
        Assert.DoesNotContain("hashtag", toml);
    }

    /// <summary>#399: the bell is on unless the file says otherwise, with no file and with a file that leaves it out.</summary>
    [Fact]
    public void Load_ReadsBellAsOnWhenAbsent()
    {
        Assert.True(NewStore().Load().Preferences.Bell);

        WriteConfigFile(
            """
            [preferences]
            hashtag = "dotnet"
            """);

        Assert.True(NewStore().Load().Preferences.Bell);
    }

    [Fact]
    public void Save_ThenLoad_RoundTripsTheBellTurnedOff()
    {
        var store = NewStore();

        store.Save(new WoolyConfig { Preferences = new Preferences { Bell = false } });

        Assert.Contains("bell = false", File.ReadAllText(Path.Combine(_directory.Path, "config.toml")));
        Assert.False(store.Load().Preferences.Bell);
    }

    /// <summary>On is written as on, though an absent key reads as it too (#506).</summary>
    [Fact]
    public void Save_WritesTheBellWhenOn()
    {
        NewStore().Save(new WoolyConfig { Preferences = new Preferences { Bell = true } });

        Assert.Contains("bell = true", File.ReadAllText(Path.Combine(_directory.Path, "config.toml")));
    }

    [Fact]
    public void Load_ReadsTheBellTurnedOnOutright()
    {
        WriteConfigFile(
            """
            [preferences]
            bell = true
            """);

        Assert.True(NewStore().Load().Preferences.Bell);
    }

    [Fact]
    public void Load_RefusesASpellingOfTheBellThatIsNotABoolean()
    {
        WriteConfigFile(
            """
            [preferences]
            bell = "off"
            """);

        var exception = Assert.Throws<ConfigurationException>(() => NewStore().Load());

        Assert.Contains("bell", exception.Message);
    }

    /// <summary>#505: the thumb is a block unless the file says otherwise, with no file and with a file that leaves it out.</summary>
    [Fact]
    public void Load_ReadsTheScrollbarAsABlockWhenAbsent()
    {
        Assert.Equal(ScrollbarStyle.Block, NewStore().Load().Preferences.Scrollbar);

        WriteConfigFile(
            """
            [preferences]
            hashtag = "dotnet"
            """);

        Assert.Equal(ScrollbarStyle.Block, NewStore().Load().Preferences.Scrollbar);
    }

    /// <summary>Each spelling but the default's is read as the style it names and written back as it was.</summary>
    [Theory]
    [InlineData("line", ScrollbarStyle.Line)]
    [InlineData("left-half", ScrollbarStyle.LeftHalf)]
    [InlineData("right-half", ScrollbarStyle.RightHalf)]
    [InlineData("none", ScrollbarStyle.None)]
    public void Save_ThenLoad_RoundTripsTheScrollbar(string written, ScrollbarStyle style)
    {
        var store = NewStore();

        WriteConfigFile(
            $"""
            [preferences]
            scrollbar = "{written}"
            """);

        Assert.Equal(style, store.Load().Preferences.Scrollbar);

        store.Save(new WoolyConfig { Preferences = new Preferences { Scrollbar = style } });

        Assert.Contains($"scrollbar = \"{written}\"", File.ReadAllText(Path.Combine(_directory.Path, "config.toml")));
        Assert.Equal(style, store.Load().Preferences.Scrollbar);
    }

    /// <summary>A block is written as a block, spelled out in the file or not (#506).</summary>
    [Fact]
    public void Save_WritesTheScrollbarWhenABlock()
    {
        WriteConfigFile(
            """
            [preferences]
            scrollbar = "block"
            """);

        var store = NewStore();

        Assert.Equal(ScrollbarStyle.Block, store.Load().Preferences.Scrollbar);

        store.Save(new WoolyConfig { Preferences = new Preferences { Scrollbar = ScrollbarStyle.Block } });

        Assert.Contains("scrollbar = \"block\"", File.ReadAllText(Path.Combine(_directory.Path, "config.toml")));
    }

    /// <summary>A style set, or the bar turned off, is kept through a save of something else, as the bell is.</summary>
    [Theory]
    [InlineData("none", ScrollbarStyle.None)]
    [InlineData("line", ScrollbarStyle.Line)]
    public void Save_KeepsTheScrollbarBesideTheOtherPreferences(string written, ScrollbarStyle style)
    {
        var store = NewStore();

        WriteConfigFile(
            $"""
            [preferences]
            scrollbar = "{written}"
            """);

        store.Save(store.Load() with { Preferences = store.Load().Preferences with { HiddenRail = true } });

        var preferences = store.Load().Preferences;

        Assert.Equal(style, preferences.Scrollbar);
        Assert.True(preferences.HiddenRail);
    }

    /// <summary>
    ///     Anything but the five spellings is refused, the booleans #501 once took among them: neither was released, so
    ///     neither is read as anything.
    /// </summary>
    [Theory]
    [InlineData("true")]
    [InlineData("false")]
    [InlineData("\"off\"")]
    [InlineData("\"Block\"")]
    [InlineData("\"\"")]
    [InlineData("1")]
    public void Load_RefusesAScrollbarThatIsNotOneOfTheStyles(string written)
    {
        WriteConfigFile(
            $"""
            [preferences]
            scrollbar = {written}
            """);

        var exception = Assert.Throws<ConfigurationException>(() => NewStore().Load());

        Assert.Contains("scrollbar", exception.Message);
        Assert.Contains(ScrollbarStyleName.Accepted, exception.Message);
    }

    [Fact]
    public void Load_RefusesASpellingOfHideDrawnCaptionThatIsNotABoolean()
    {
        WriteConfigFile(
            """
            [preferences]
            hide_drawn_caption = "yes"
            """);

        var exception = Assert.Throws<ConfigurationException>(() => NewStore().Load());

        Assert.Contains("hide_drawn_caption", exception.Message);
    }

    /// <summary>#415: a shell width written by hand is honoured as written, an in-between one included.</summary>
    [Fact]
    public void Load_ReadsAShellWidthWrittenByHand()
    {
        WriteConfigFile(
            """
            [preferences]
            shell_width = 73
            """);

        Assert.Equal(73, NewStore().Load().Preferences.ShellWidth);
    }

    /// <summary>All of the terminal is what an absent key means, with no file and with a file that leaves it out.</summary>
    [Fact]
    public void Load_ReadsTheShellWidthAsFullWhenAbsent()
    {
        Assert.Equal(100, NewStore().Load().Preferences.ShellWidth);

        WriteConfigFile(
            """
            [preferences]
            hashtag = "dotnet"
            """);

        Assert.Equal(100, NewStore().Load().Preferences.ShellWidth);
    }

    /// <summary>Out of range is clamped rather than refused, so a typo doesn't stop the TUI starting.</summary>
    [Theory]
    [InlineData(49, 50)]
    [InlineData(0, 50)]
    [InlineData(-20, 50)]
    [InlineData(101, 100)]
    [InlineData(500, 100)]
    [InlineData(9_999_999_999, 100)]
    public void Load_ClampsTheShellWidthToHalfOrMore_AndAllOrLess(long written, int read)
    {
        WriteConfigFile(
            $"""
             [preferences]
             shell_width = {written}
             """);

        Assert.Equal(read, NewStore().Load().Preferences.ShellWidth);
    }

    [Fact]
    public void Save_ThenLoad_RoundTripsTheShellWidthBesideTheOtherPreferences()
    {
        var store = NewStore();

        store.Save(new WoolyConfig
        {
            Preferences = new Preferences
            {
                Hashtag = "dotnet", HideDrawnCaption = true, Bell = false, ShellWidth = 70,
            },
        });

        Assert.Contains("shell_width = 70", File.ReadAllText(Path.Combine(_directory.Path, "config.toml")));

        var preferences = store.Load().Preferences;

        Assert.Equal(70, preferences.ShellWidth);
        Assert.Equal("dotnet", preferences.Hashtag);
        Assert.True(preferences.HideDrawnCaption);
        Assert.False(preferences.Bell);
    }

    /// <summary>Widening back to 100 writes 100, the line kept for the reader to find (#506).</summary>
    [Fact]
    public void Save_WritesTheShellWidthOnceItIsBackAtFull()
    {
        var store = NewStore();

        store.Save(new WoolyConfig { Preferences = new Preferences { Hashtag = "dotnet", ShellWidth = 90 } });
        store.Save(store.Load() with { Preferences = store.Load().Preferences with { ShellWidth = 100 } });

        var toml = File.ReadAllText(Path.Combine(_directory.Path, "config.toml"));

        Assert.Contains("shell_width = 100", toml);
        Assert.Contains("hashtag = \"dotnet\"", toml);
    }

    [Theory]
    [InlineData("\"80\"")]
    [InlineData("72.5")]
    [InlineData("true")]
    public void Load_RefusesAShellWidthThatIsNotAWholeNumber(string written)
    {
        WriteConfigFile(
            $"""
             [preferences]
             shell_width = {written}
             """);

        var exception = Assert.Throws<ConfigurationException>(() => NewStore().Load());

        Assert.Contains("shell_width", exception.Message);
    }

    /// <summary>
    ///     #445: the shown rail is what an absent key means, with no file and with a file that leaves it out — which
    ///     is what a fresh install opens on.
    /// </summary>
    [Fact]
    public void Load_ReadsTheRailAsShownWhenAbsent()
    {
        Assert.False(NewStore().Load().Preferences.HiddenRail);

        WriteConfigFile(
            """
            [preferences]
            hashtag = "dotnet"
            """);

        Assert.False(NewStore().Load().Preferences.HiddenRail);
    }

    [Fact]
    public void Save_ThenLoad_RoundTripsTheHiddenRailBesideTheShellWidth()
    {
        var store = NewStore();

        store.Save(new WoolyConfig { Preferences = new Preferences { ShellWidth = 70, HiddenRail = true } });

        Assert.Contains("hidden_rail = true", File.ReadAllText(Path.Combine(_directory.Path, "config.toml")));

        var preferences = store.Load().Preferences;

        Assert.True(preferences.HiddenRail);
        Assert.Equal(70, preferences.ShellWidth);
    }

    /// <summary>Shown is written as shown, and showing the rail again writes it so rather than dropping it (#506).</summary>
    [Fact]
    public void Save_WritesTheRailWhenShown_AndOnceShownAgain()
    {
        var store = NewStore();

        store.Save(new WoolyConfig { Preferences = new Preferences { HiddenRail = false } });

        Assert.Contains("hidden_rail = false", File.ReadAllText(Path.Combine(_directory.Path, "config.toml")));

        store.Save(new WoolyConfig { Preferences = new Preferences { Hashtag = "dotnet", HiddenRail = true } });
        store.Save(store.Load() with { Preferences = store.Load().Preferences with { HiddenRail = false } });

        var toml = File.ReadAllText(Path.Combine(_directory.Path, "config.toml"));

        Assert.Contains("hidden_rail = false", toml);
        Assert.Contains("hashtag = \"dotnet\"", toml);
    }

    /// <summary>
    ///     A bad value written by hand is refused against the config file, as a bad <c>shell_width</c> is: there is no
    ///     in-between state to bring it to, so it is said rather than guessed.
    /// </summary>
    [Theory]
    [InlineData("\"yes\"")]
    [InlineData("1")]
    [InlineData("\"hidden\"")]
    public void Load_RefusesAHiddenRailThatIsNotTrueOrFalse(string written)
    {
        WriteConfigFile(
            $"""
             [preferences]
             hidden_rail = {written}
             """);

        var exception = Assert.Throws<ConfigurationException>(() => NewStore().Load());

        Assert.Contains("config.toml", exception.Message);
        Assert.Contains("'hidden_rail' must be true or false.", exception.Message);
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
            Preferences = new Preferences { DefaultVisibility = PostVisibility.Followers },
        });

        var toml = File.ReadAllText(Path.Combine(_directory.Path, "config.toml"));

        Assert.Contains("current_profile = \"personal\"", toml);
        Assert.Contains("[preferences]", toml);
        Assert.Contains("default_visibility = \"followers\"", toml);
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

    /// <summary>
    ///     A file written before ADR-0024 says <c>private</c>, Mastodon's word for followers. It still loads, and means
    ///     the same thing the new word does.
    /// </summary>
    [Theory]
    [InlineData("followers")]
    [InlineData("private")]
    public void Load_ReadsFollowersUnderEitherSpelling(string written)
    {
        WriteConfigFile(
            $"""
             [preferences]
             default_visibility = "{written}"
             """);

        Assert.Equal(PostVisibility.Followers, NewStore().Load().Preferences.DefaultVisibility);
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

    [Fact]
    public void Load_ReadsAbsentDefaultLanguageAsNone()
    {
        WriteConfigFile(
            """
            [preferences]
            default_visibility = "public"
            """);

        Assert.Null(NewStore().Load().Preferences.DefaultLanguage);
    }

    [Fact]
    public void Save_ThenLoad_RoundTripsDefaultLanguage()
    {
        var store = NewStore();

        store.Save(new WoolyConfig { Preferences = new Preferences { DefaultLanguage = "de" } });

        Assert.Equal("de", store.Load().Preferences.DefaultLanguage);
        Assert.Contains(
            "default_language = \"de\"",
            File.ReadAllText(Path.Combine(_directory.Path, "config.toml")));
    }

    /// <summary>
    ///     The same words the <c>--language</c> flag takes, so a hand-written name reads as the code it names — and is
    ///     written back as that code, the one spelling the file keeps.
    /// </summary>
    [Theory]
    [InlineData("de")]
    [InlineData("German")]
    [InlineData("Deutsch")]
    [InlineData("DE")]
    public void Load_ReadsADefaultLanguageWrittenByHandAsTheCodeItNames(string written)
    {
        WriteConfigFile(
            $"""
             [preferences]
             default_language = "{written}"
             """);

        Assert.Equal("de", NewStore().Load().Preferences.DefaultLanguage);
    }

    /// <summary>A typo is refused when the file loads, naming the file, rather than quietly sending no language.</summary>
    [Fact]
    public void Load_RefusesADefaultLanguageItDoesNotKnowNamingTheFile()
    {
        WriteConfigFile(
            """
            [preferences]
            default_language = "Klingon"
            """);

        var exception = Assert.Throws<ConfigurationException>(() => NewStore().Load());

        Assert.Contains("Klingon", exception.Message);
        Assert.Contains("config.toml", exception.Message);
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
