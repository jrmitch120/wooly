using Wooly.Core;
using Wooly.Core.Configuration;
using Wooly.Tests.Fakes;
using Wooly.Tui.Shell;

namespace Wooly.Tests.Tui;

/// <summary>
///     The shell width saved into a real config file, in a folder of its own (#415): the one preference set, and
///     everything else in the file left as it was.
/// </summary>
public class ShellWidthStoreTests
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

        new ShellWidthStore(config).Save(ShellWidth.Of(70));

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
        var store = new ShellWidthStore(new TomlConfigStore(paths));

        store.Save(ShellWidth.Of(90));

        Assert.Contains("shell_width = 90", File.ReadAllText(paths.ConfigFile));

        store.Save(ShellWidth.Full);

        Assert.Empty(File.ReadAllText(paths.ConfigFile));
    }
}
