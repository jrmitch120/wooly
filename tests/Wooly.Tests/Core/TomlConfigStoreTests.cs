using Wooly.Core;
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

    private TomlConfigStore NewStore() => new(new WoolyPaths(_directory.Path));

    private void WriteConfigFile(string toml) => File.WriteAllText(Path.Combine(_directory.Path, "config.toml"), toml);
}
