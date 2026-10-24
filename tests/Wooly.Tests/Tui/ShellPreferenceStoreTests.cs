using Wooly.Core;
using Wooly.Core.Configuration;
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
            Preferences = new Preferences { Hashtag = "dotnet", Bell = false },
        });

        new ShellPreferenceStore(config).SaveWidth(ShellWidth.Of(70));

        var saved = config.Load();
        Assert.Equal(70, saved.Preferences.ShellWidth);
        Assert.Equal("dotnet", saved.Preferences.Hashtag);
        Assert.False(saved.Preferences.Bell);
        Assert.Equal("personal", saved.CurrentProfile);
        Assert.Equal("jeff@mastodon.social", saved.Profiles["personal"].Account);
    }

    /// <summary>Widened back to the whole terminal, the line goes, leaving the file it would be had it never changed.</summary>
    [Fact]
    public void SavingTheWholeTerminalTakesTheLineAway()
    {
        using var directory = new TemporaryDirectory();
        var paths = new WoolyPaths(directory.Path);
        var store = new ShellPreferenceStore(new TomlConfigStore(paths));

        store.SaveWidth(ShellWidth.Of(90));

        Assert.Contains("shell_width = 90", File.ReadAllText(paths.ConfigFile));

        store.SaveWidth(ShellWidth.Full);

        Assert.Empty(File.ReadAllText(paths.ConfigFile));
    }

    /// <summary>
    ///     Collapsing the rail writes it beside the width, and expanding it again takes the line away, leaving the
    ///     width where it was.
    /// </summary>
    [Fact]
    public void SavingTheRailWritesItBesideTheWidth_AndExpandingItTakesTheLineAway()
    {
        using var directory = new TemporaryDirectory();
        var paths = new WoolyPaths(directory.Path);
        var config = new TomlConfigStore(paths);
        var store = new ShellPreferenceStore(config);

        store.SaveWidth(ShellWidth.Of(90));
        store.SaveRail(RailState.Collapsed);

        Assert.True(config.Load().Preferences.CollapsedRail);
        Assert.Equal(90, config.Load().Preferences.ShellWidth);

        store.SaveRail(RailState.Expanded);

        var toml = File.ReadAllText(paths.ConfigFile);
        Assert.DoesNotContain("collapsed_rail", toml);
        Assert.Contains("shell_width = 90", toml);
    }
}
