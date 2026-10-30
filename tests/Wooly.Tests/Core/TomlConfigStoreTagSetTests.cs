using Wooly.Core;
using Wooly.Core.Configuration;
using Wooly.Core.Errors;
using Wooly.Core.Timelines;
using Wooly.Tests.Fakes;

namespace Wooly.Tests.Core;

/// <summary>
///     The config file's <c>[[tag_sets]]</c> (ADR-0033, #511): read in the order written, held to a set's rules, and
///     written back in that same order by every save, against a temporary file as the store's other tests are.
/// </summary>
public class TomlConfigStoreTagSetTests : IDisposable
{
    private readonly TemporaryDirectory _directory = new();

    public void Dispose() => _directory.Dispose();

    private string ConfigFile => Path.Combine(_directory.Path, "config.toml");

    [Fact]
    public void Load_ReadsNoSetsWithNoFile_AndWithAFileNamingNone()
    {
        Assert.Empty(NewStore().Load().Preferences.TagSets);

        WriteConfigFile(
            """
            [preferences]
            bell = false
            """);

        Assert.Empty(NewStore().Load().Preferences.TagSets);
    }

    [Fact]
    public void Load_ReadsTheSetsInTheOrderTheFileWritesThem()
    {
        WriteConfigFile(
            """
            [preferences]
            bell = true

            [[tag_sets]]
            name = "dotnet"
            tags = ["dotnet", "csharp", "fsharp"]

            [[tag_sets]]
            name = "cats"
            tags = ["cats"]
            """);

        Assert.Equal(
            [TagSet.Of("dotnet", ["dotnet", "csharp", "fsharp"]), TagSet.Of("cats", ["cats"])],
            NewStore().Load().Preferences.TagSets);
    }

    [Fact]
    public void Load_MakesEachTagBare_AndDropsARepeat()
    {
        WriteConfigFile(
            """
            [[tag_sets]]
            name = "dotnet"
            tags = ["#dotnet", "csharp", "dotnet", "#csharp"]
            """);

        Assert.Equal(["dotnet", "csharp"], Assert.Single(NewStore().Load().Preferences.TagSets).Tags);
    }

    /// <summary>
    ///     Each refused against the file, as a bad <c>shell_width</c> is, with a message naming what is wrong — so that
    ///     whoever wrote it can find it.
    /// </summary>
    [Theory]
    [InlineData("tags = [\"cats\"]", "name")]
    [InlineData("name = \"\"\ntags = [\"cats\"]", "name")]
    [InlineData("name = \"cats\"\ntags = []", "no tags")]
    [InlineData("name = \"cats\"", "no tags")]
    [InlineData("name = \"cats\"\ntags = [\"big cats\"]", "big cats")]
    [InlineData("name = \"cats\"\ntags = [\"cats/../../accounts\"]", "cats/../../accounts")]
    [InlineData("name = \"pets\"\ntags = [\"cats\", \"dogs\", \"rabbits\", \"ferrets\", \"mice\"]", "at most 4")]
    [InlineData("name = 7\ntags = [\"cats\"]", "'name' must be text")]
    [InlineData("name = \"cats\"\ntags = \"cats\"", "'tags' must be a list")]
    [InlineData("name = \"cats\"\ntags = [7]", "'tags' must be a list")]
    public void Load_RefusesASetThatIsNotOne(string written, string said)
    {
        WriteConfigFile($"[[tag_sets]]\n{written}\n");

        var exception = Assert.Throws<ConfigurationException>(() => NewStore().Load());

        Assert.Contains(said, exception.Message);
        Assert.Contains("config.toml", exception.Message);
    }

    [Fact]
    public void Load_RefusesTwoSetsOfTheOneName()
    {
        WriteConfigFile(
            """
            [[tag_sets]]
            name = "pets"
            tags = ["cats"]

            [[tag_sets]]
            name = "pets"
            tags = ["dogs"]
            """);

        var exception = Assert.Throws<ConfigurationException>(() => NewStore().Load());

        Assert.Contains("'pets'", exception.Message);
        Assert.Contains("more than one", exception.Message);
    }

    [Fact]
    public void Load_RefusesTagSetsWrittenAsATable()
    {
        WriteConfigFile(
            """
            [tag_sets]
            name = "pets"
            """);

        var exception = Assert.Throws<ConfigurationException>(() => NewStore().Load());

        Assert.Contains("tag_sets", exception.Message);
    }

    /// <summary>
    ///     The old single <c>hashtag</c> is not refused, does nothing — not even a set — and is gone after a save,
    ///     which rewrites the whole file.
    /// </summary>
    [Fact]
    public void Load_IgnoresAnOldHashtag_WhichIsGoneAfterTheNextSave()
    {
        WriteConfigFile(
            """
            [preferences]
            hashtag = "dotnet"
            bell = false
            """);

        var store = NewStore();
        var config = store.Load();

        Assert.Empty(config.Preferences.TagSets);
        Assert.False(config.Preferences.Bell);

        store.Save(config);

        Assert.DoesNotContain("hashtag", File.ReadAllText(ConfigFile));
    }

    /// <summary>A hashtag that would once have been refused is ignored as readily as one that would not.</summary>
    [Fact]
    public void Load_IgnoresAnOldHashtagThatIsNotOneWord()
    {
        WriteConfigFile(
            """
            [preferences]
            hashtag = "big cats"
            """);

        Assert.Empty(NewStore().Load().Preferences.TagSets);
    }

    [Fact]
    public void Save_WritesNoTagSetsWhereThereAreNone()
    {
        NewStore().Save(WoolyConfig.Empty);

        Assert.DoesNotContain("tag_sets", File.ReadAllText(ConfigFile));
    }

    [Fact]
    public void Save_WritesTheSetsInOrder_AfterThePreferencesAndBeforeTheProfiles()
    {
        NewStore().Save(new WoolyConfig
        {
            CurrentProfile = "personal",
            Profiles = new Dictionary<string, ProfileConfig>
            {
                ["personal"] = new() { Instance = "mastodon.social" },
            },
            Preferences = new Preferences
            {
                TagSets = [TagSet.Of("zebras", ["zebras"]), TagSet.Of("dotnet", ["dotnet", "csharp", "fsharp"])],
            },
        });

        Assert.Equal(
            """
            current_profile = "personal"
            [preferences]
            bell = true
            hidden_rail = false
            hide_drawn_caption = false
            scrollbar = "block"
            shell_width = 100

            [[tag_sets]]
            name = "zebras"
            tags = ["zebras"]

            [[tag_sets]]
            name = "dotnet"
            tags = ["dotnet", "csharp", "fsharp"]

            [profiles]
            [profiles.personal]
            instance = "mastodon.social"

            """,
            File.ReadAllText(ConfigFile).ReplaceLineEndings("\n"));
    }

    /// <summary>
    ///     A save of another preference — the shell width, the rail, a profile made the default — reads the file and
    ///     writes it whole, and keeps every set and its order exactly.
    /// </summary>
    [Fact]
    public void Save_OfAnotherPreference_KeepsEverySetInItsOrder()
    {
        WriteConfigFile(
            """
            [preferences]
            shell_width = 80

            [[tag_sets]]
            name = "zebras"
            tags = ["#zebras", "zebra"]

            [[tag_sets]]
            name = "aardvarks"
            tags = ["aardvarks"]
            """);

        var store = NewStore();

        store.Save(store.Load() with { Preferences = store.Load().Preferences with { ShellWidth = 70 } });
        store.Save(store.Load() with { Preferences = store.Load().Preferences with { HiddenRail = true } });

        var preferences = store.Load().Preferences;

        Assert.Equal(70, preferences.ShellWidth);
        Assert.Equal(
            [TagSet.Of("zebras", ["zebras", "zebra"]), TagSet.Of("aardvarks", ["aardvarks"])],
            preferences.TagSets);
    }

    private TomlConfigStore NewStore() => new(new WoolyPaths(_directory.Path));

    private void WriteConfigFile(string toml) => File.WriteAllText(ConfigFile, toml);
}
