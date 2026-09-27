using Wooly.Core;
using Wooly.Core.Configuration;
using Wooly.Core.Credentials;
using Wooly.Core.Profiles;
using Wooly.Tests.Fakes;
using Wooly.Tui.Screens;
using Wooly.Tui.Shell;

namespace Wooly.Tests.Tui;

/// <summary>
///     <c>D</c> on the profiles screen: the picked profile becomes the default, the one the CLI and the next launch act
///     as — and who this session is acting as does not move (ADR-0020, #244).
/// </summary>
public class ShellDefaultProfileTests
{
    /// <summary>Acting as personal, which is the default, with work set up beside it on another instance.</summary>
    private static AShell PersonalAndWork() => new()
    {
        Profiles = FakeProfileRegistry.Holding(
            "personal",
            FakeProfileRegistry.Profile("personal", "mastodon.social", "jeff@mastodon.social"),
            FakeProfileRegistry.Profile("work", "hachyderm.io", "jeff@hachyderm.io")),
    };

    /// <summary>The profiles screen, with <paramref name="name" /> picked.</summary>
    private static void OnProfiles(Shell shell, string name)
    {
        shell.Press(ShellKey.CtrlP);

        Assert.IsType<ProfilesScreen>(shell.Screen).Pick(name);
    }

    [Fact]
    public async Task D_MakesThePickedProfileTheDefault_ThroughTheRegistry()
    {
        var shell = PersonalAndWork();
        var opened = await shell.Opened();

        OnProfiles(opened, "work");

        Assert.True(opened.Press(ShellKey.CapitalD));

        Assert.Equal("work", Assert.Single(shell.Profiles.Switched));
        Assert.True(shell.Profiles.List().Single(profile => profile.Name == "work").IsCurrent);
    }

    /// <summary>The marker moves, the row stays picked, and the status row says so in <c>profile switch</c>'s words.</summary>
    [Fact]
    public async Task D_MovesTheDefaultMarker_AndSaysWhoCommandsNowActAs()
    {
        var shell = PersonalAndWork();
        var opened = await shell.Opened();

        OnProfiles(opened, "work");
        opened.Press(ShellKey.CapitalD);

        var profiles = Assert.IsType<ProfilesScreen>(opened.Screen);
        Assert.Equal("work", profiles.PickedProfile?.Name);
        Assert.Equal(2, opened.Depth);

        var drawn = AShell.Drawn(opened.Screen);
        Assert.Contains(drawn, row => row.StartsWith("work") && row.Contains("default") && !row.Contains("acting as"));
        Assert.Contains(drawn, row => row.StartsWith("personal") && row.Contains("acting as") && !row.Contains("default"));

        Assert.Equal(ProfileWords.ActsAs("work"), opened.Notice);
        Assert.False(opened.NoticeIsError);
    }

    /// <summary>Who this session acts as is untouched: nothing is fetched, and every later request is the old profile's.</summary>
    [Fact]
    public async Task D_LeavesWhoTheSessionActsAs_AndEveryLaterRequest_AsTheyWere()
    {
        var shell = PersonalAndWork();
        var opened = await shell.Opened();
        var requests = shell.Requests;

        OnProfiles(opened, "work");
        opened.Press(ShellKey.CapitalD);
        shell.Host.Drain();

        Assert.Equal(requests, shell.Requests);
        Assert.Equal("mastodon.social", opened.Instance);

        opened.Press(ShellKey.Escape);
        await opened.Refresh();
        shell.Host.Drain();

        Assert.NotEmpty(shell.Tokens);
        Assert.All(shell.Tokens, token => Assert.Equal("token-personal", token));
        Assert.Equal("personal", shell.Timelines.Reads[^1].Profile);
    }

    /// <summary>Not offered on the row that is already the default, where it would do nothing.</summary>
    [Fact]
    public async Task D_IsNotOffered_OnTheDefaultProfile_AndDoesNothingThere()
    {
        var shell = PersonalAndWork();
        var opened = await shell.Opened();

        OnProfiles(opened, "personal");

        Assert.DoesNotContain(opened.Keys, key => key.Key == "D");

        opened.Press(ShellKey.CapitalD);

        Assert.Empty(shell.Profiles.Switched);
        Assert.Null(opened.Notice);
    }

    /// <summary>And on any other row it is offered, as what it does — which is also how it is undone.</summary>
    [Fact]
    public async Task D_IsOffered_OnAnyOtherProfile_AndPressedOnTheOldDefault_PutsItBack()
    {
        var shell = PersonalAndWork();
        var opened = await shell.Opened();

        OnProfiles(opened, "work");

        Assert.Contains(opened.Keys, key => key is { Key: "D", Does: "make default" });

        opened.Press(ShellKey.CapitalD);

        Assert.DoesNotContain(opened.Keys, key => key.Key == "D");

        Assert.IsType<ProfilesScreen>(opened.Screen).Pick("personal");
        opened.Press(ShellKey.CapitalD);

        Assert.Equal(["work", "personal"], shell.Profiles.Switched);
        Assert.True(shell.Profiles.List().Single(profile => profile.Name == "personal").IsCurrent);
    }

    /// <summary>
    ///     Written to the config file, so the CLI reads it afterwards — what <c>profile list</c> and a command with no
    ///     <c>--profile</c> go by — and the token store is not touched.
    /// </summary>
    [Fact]
    public async Task D_WritesTheDefaultToTheConfigFile()
    {
        using var directory = new TemporaryDirectory();
        var paths = new WoolyPaths(directory.Path);
        var registry = new ProfileRegistry(new TomlConfigStore(paths), new PlaintextFileCredentialStore(paths), paths);
        registry.Add(
            "personal",
            new ProfileConfig { Instance = "mastodon.social", Account = "jeff@mastodon.social" },
            "token-personal");
        registry.Add(
            "work",
            new ProfileConfig { Instance = "hachyderm.io", Account = "jeff@hachyderm.io" },
            "token-work");

        var credentials = await File.ReadAllBytesAsync(paths.CredentialFile, TestContext.Current.CancellationToken);

        var shell = new AShell();
        var opened = await shell.OpenedOver(registry, paths);

        OnProfiles(opened, "work");
        opened.Press(ShellKey.CapitalD);

        var reread = new ProfileRegistry(new TomlConfigStore(paths), new PlaintextFileCredentialStore(paths), paths);
        Assert.True(reread.List().Single(profile => profile.Name == "work").IsCurrent);
        Assert.Equal("work", reread.Resolve(null).Name);
        Assert.Equal(credentials, await File.ReadAllBytesAsync(paths.CredentialFile, TestContext.Current.CancellationToken));
    }
}
