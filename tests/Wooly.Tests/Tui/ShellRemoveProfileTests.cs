using Wooly.Core;
using Wooly.Core.Configuration;
using Wooly.Core.Credentials;
using Wooly.Core.Profiles;
using Wooly.Tests.Fakes;
using Wooly.Tui.Screens;
using Wooly.Tui.Shell;

namespace Wooly.Tests.Tui;

/// <summary>
///     <c>x</c> on the profiles screen: the picked profile is removed through the registry, config entry and token
///     together, once the reader has said so twice — and never the one this session is acting as (ADR-0020, #246).
/// </summary>
public class ShellRemoveProfileTests
{
    /// <summary>
    ///     Acting as personal, with archive and work set up beside it — and <paramref name="current" /> the default.
    /// </summary>
    private static AShell ThreeProfiles(string current = "personal") => new()
    {
        Profiles = FakeProfileRegistry.Holding(
            current,
            FakeProfileRegistry.Profile("archive", "fosstodon.org", "jeff@fosstodon.org"),
            FakeProfileRegistry.Profile("personal", "mastodon.social", "jeff@mastodon.social"),
            FakeProfileRegistry.Profile("work", "hachyderm.io", "jeff@hachyderm.io")),
    };

    /// <summary>The profiles screen, with <paramref name="name" /> picked.</summary>
    private static void OnProfiles(Shell shell, string name)
    {
        shell.Press(ShellKey.CtrlP);

        Assert.IsType<ProfilesScreen>(shell.Screen).Pick(name);
    }

    /// <summary>Asked in the same form as deleting a post, and nothing is written until it is answered.</summary>
    [Fact]
    public async Task X_AsksFirst_InTheFormDeletingAPostDoes()
    {
        var shell = ThreeProfiles();
        var opened = await shell.Opened();

        OnProfiles(opened, "work");

        Assert.True(opened.Press(ShellKey.X));

        var asking = Assert.IsType<Confirmation>(opened.Asking);
        Assert.Equal("Remove this profile?", asking.Ask);
        Assert.Equal("remove", asking.Going);
        Assert.Equal("y", asking.Confirm);
        Assert.Equal(Confirmation.CannotBeUndone, asking.Warning);

        Assert.Empty(shell.Profiles.Removed);
    }

    [Fact]
    public async Task X_Declined_ChangesNothing()
    {
        var shell = ThreeProfiles();
        var opened = await shell.Opened();

        OnProfiles(opened, "work");
        opened.Press(ShellKey.X);
        await opened.Answer(agreed: false);

        Assert.Null(opened.Asking);
        Assert.Empty(shell.Profiles.Removed);
        Assert.Equal(3, shell.Profiles.List().Count);

        var profiles = Assert.IsType<ProfilesScreen>(opened.Screen);
        Assert.Equal("work", profiles.PickedProfile?.Name);
        Assert.Contains(AShell.Drawn(profiles), row => row.StartsWith("work"));
    }

    /// <summary>
    ///     Agreed, the profile goes through the registry, its row goes, and the pick moves to the row that was under
    ///     it — nothing reaching an instance on the way.
    /// </summary>
    [Fact]
    public async Task X_Agreed_RemovesTheProfile_AndPicksTheNextRow()
    {
        var shell = ThreeProfiles();
        var opened = await shell.Opened();
        var requests = shell.Requests;

        OnProfiles(opened, "archive");
        opened.Press(ShellKey.X);
        await opened.Answer(agreed: true);
        shell.Host.Drain();

        Assert.Equal("archive", Assert.Single(shell.Profiles.Removed));

        var profiles = Assert.IsType<ProfilesScreen>(opened.Screen);
        Assert.Equal("personal", profiles.PickedProfile?.Name);
        Assert.Equal(2, opened.Depth);
        Assert.DoesNotContain(AShell.Drawn(profiles), row => row.StartsWith("archive"));

        Assert.Equal("Removed profile archive.", opened.Notice);
        Assert.False(opened.NoticeIsError);
        Assert.Equal(requests, shell.Requests);
    }

    /// <summary>The last row has nothing under it, so the pick moves up to the one above.</summary>
    [Fact]
    public async Task X_OnTheLastRow_PicksTheRowAbove()
    {
        var shell = ThreeProfiles();
        var opened = await shell.Opened();

        OnProfiles(opened, "work");
        opened.Press(ShellKey.X);
        await opened.Answer(agreed: true);

        Assert.Equal("personal", Assert.IsType<ProfilesScreen>(opened.Screen).PickedProfile?.Name);
    }

    /// <summary>
    ///     The profile this session is acting as is refused, in words saying what to do instead — and the key stays on
    ///     the row, since refusing is what it does there.
    /// </summary>
    [Fact]
    public async Task X_OnTheProfileActedAs_IsRefused_AndNothingIsWritten()
    {
        var shell = ThreeProfiles();
        var opened = await shell.Opened();

        OnProfiles(opened, "personal");

        Assert.Contains(opened.Keys, key => key is { Key: "x", Does: "remove" });

        Assert.True(opened.Press(ShellKey.X));

        Assert.Null(opened.Asking);
        Assert.Empty(shell.Profiles.Removed);
        Assert.Equal("Switch to another profile before removing this one.", opened.Notice);
        Assert.True(opened.NoticeIsError);
    }

    /// <summary>
    ///     The default profile is refused too, before anything is asked, in words saying what to do instead — and the
    ///     key stays on the row there for the same reason.
    /// </summary>
    [Fact]
    public async Task X_OnTheDefaultProfile_IsRefused_AndNothingIsWritten()
    {
        var shell = ThreeProfiles(current: "work");
        var opened = await shell.Opened();

        OnProfiles(opened, "work");

        Assert.Contains(opened.Keys, key => key is { Key: "x", Does: "remove" });

        Assert.True(opened.Press(ShellKey.X));

        Assert.Null(opened.Asking);
        Assert.Empty(shell.Profiles.Removed);
        Assert.True(shell.Profiles.List().Single(profile => profile.Name == "work").IsCurrent);
        Assert.Equal("Make another profile the default before removing this one.", opened.Notice);
        Assert.True(opened.NoticeIsError);
    }

    /// <summary>
    ///     A list read before the default moved elsewhere — <c>profile switch</c> in another terminal — still lands on
    ///     the registry's refusal, and is told it in this screen's words rather than the CLI's.
    /// </summary>
    [Fact]
    public async Task X_OnAProfileMadeTheDefaultSinceTheListWasRead_IsRefusedInTheScreensWords()
    {
        var shell = ThreeProfiles();
        var opened = await shell.Opened();

        OnProfiles(opened, "archive");
        shell.Profiles.Switch("archive");
        opened.Press(ShellKey.X);
        await opened.Answer(agreed: true);

        Assert.Empty(shell.Profiles.Removed);
        Assert.Equal("Make another profile the default before removing this one.", opened.Notice);
        Assert.True(opened.NoticeIsError);
    }

    /// <summary>Down to one profile, the rail's foot no longer names the instance, there being nobody to tell apart.</summary>
    [Fact]
    public async Task X_DownToOneProfile_TakesTheInstanceOffTheRail()
    {
        var shell = new AShell
        {
            Profiles = FakeProfileRegistry.Holding(
                "personal",
                FakeProfileRegistry.Profile("personal", "mastodon.social", "jeff@mastodon.social"),
                FakeProfileRegistry.Profile("work", "hachyderm.io", "jeff@hachyderm.io")),
        };
        var opened = await shell.Opened();

        Assert.Equal("mastodon.social", opened.Instance);

        OnProfiles(opened, "work");
        opened.Press(ShellKey.X);
        await opened.Answer(agreed: true);

        Assert.Null(opened.Instance);
    }

    /// <summary>
    ///     Over the real registry: the config entry and the token both go from their files, and the default is left
    ///     where it was.
    /// </summary>
    [Fact]
    public async Task X_TakesTheConfigEntryAndTheTokenOutOfTheirFiles()
    {
        using var directory = new TemporaryDirectory();
        var paths = new WoolyPaths(directory.Path);
        var registry = new ProfileRegistry(new TomlConfigStore(paths), new PlaintextFileCredentialStore(paths), paths);
        registry.Add(
            "work",
            new ProfileConfig { Instance = "hachyderm.io", Account = "jeff@hachyderm.io" },
            "token-work");
        registry.Add(
            "personal",
            new ProfileConfig { Instance = "mastodon.social", Account = "jeff@mastodon.social" },
            "token-personal");

        var shell = new AShell();
        var opened = await shell.OpenedOver(registry, paths);

        // Acting as work, which is also the default — the two profiles x refuses — so personal is the one it can take.
        OnProfiles(opened, "personal");
        opened.Press(ShellKey.X);
        await opened.Answer(agreed: true);

        var reread = new ProfileRegistry(new TomlConfigStore(paths), new PlaintextFileCredentialStore(paths), paths);
        Assert.Equal(["work"], reread.List().Select(profile => profile.Name));
        Assert.Equal("work", reread.Resolve(null).Name);
        Assert.Null(new PlaintextFileCredentialStore(paths).FindAccessToken("personal"));
        Assert.Equal("token-work", new PlaintextFileCredentialStore(paths).FindAccessToken("work"));
    }
}
