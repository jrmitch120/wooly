using Wooly.Core;
using Wooly.Core.Configuration;
using Wooly.Core.Timelines;
using Wooly.Tests.Fakes;
using Wooly.Tui.Shell;

namespace Wooly.Tests.Tui;

/// <summary>
///     The shell's preferences saved into a real config file, in a folder of its own (#415, #442): the one preference
///     set, and everything else in the file left as it was.
/// </summary>
public class ShellPreferenceStoreTests
{
    [Fact]
    public void SavingAWidthWritesItToThePreferences_AndKeepsTheRestOfTheFile()
    {
        using var directory = new TemporaryDirectory();
        var config = new TomlConfigStore(new WoolyPaths(directory.Path));
        config.Save(new WoolyConfig
        {
            CurrentProfile = "personal",
            Profiles = new Dictionary<string, ProfileConfig>
            {
                ["personal"] = new() { Instance = "mastodon.social", Account = "jeff@mastodon.social" },
            },
            Preferences = new Preferences
            {
                TagSets = [TagSet.Of("dotnet", ["dotnet", "csharp"]), TagSet.Of("cats", ["cats"])],
                Bell = false,
                Scrollbar = ScrollbarStyle.RightHalf,
            },
        });

        new ShellPreferenceStore(config).SaveWidth(ShellWidth.Of(70));

        var saved = config.Load();
        Assert.Equal(70, saved.Preferences.ShellWidth);
        Assert.Equal(
            [TagSet.Of("dotnet", ["dotnet", "csharp"]), TagSet.Of("cats", ["cats"])],
            saved.Preferences.TagSets);
        Assert.False(saved.Preferences.Bell);
        Assert.Equal(ScrollbarStyle.RightHalf, saved.Preferences.Scrollbar);
        Assert.Equal("personal", saved.CurrentProfile);
        Assert.Equal("jeff@mastodon.social", saved.Profiles["personal"].Account);
    }

    /// <summary>Widened back to the whole terminal, the line stays, at 100 (#506).</summary>
    [Fact]
    public void SavingTheWholeTerminalWritesItAtFull()
    {
        using var directory = new TemporaryDirectory();
        var paths = new WoolyPaths(directory.Path);
        var store = new ShellPreferenceStore(new TomlConfigStore(paths));

        store.SaveWidth(ShellWidth.Of(90));

        Assert.Contains("shell_width = 90", File.ReadAllText(paths.ConfigFile));

        store.SaveWidth(ShellWidth.Full);

        Assert.Contains("shell_width = 100", File.ReadAllText(paths.ConfigFile));
    }

    /// <summary>
    ///     Hiding the rail writes it beside the width, and showing it again writes it shown (#506), leaving the width
    ///     where it was.
    /// </summary>
    [Fact]
    public void SavingTheRailWritesItBesideTheWidth_AndShowingItWritesItShown()
    {
        using var directory = new TemporaryDirectory();
        var paths = new WoolyPaths(directory.Path);
        var config = new TomlConfigStore(paths);
        var store = new ShellPreferenceStore(config);

        store.SaveWidth(ShellWidth.Of(90));
        store.SaveRail(RailState.Hidden);

        Assert.True(config.Load().Preferences.HiddenRail);
        Assert.Equal(90, config.Load().Preferences.ShellWidth);

        store.SaveRail(RailState.Shown);

        var toml = File.ReadAllText(paths.ConfigFile);
        Assert.Contains("hidden_rail = false", toml);
        Assert.Contains("shell_width = 90", toml);
    }
}
