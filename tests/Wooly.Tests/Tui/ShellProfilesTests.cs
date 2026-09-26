using Wooly.Core.Credentials;
using Wooly.Core.Profiles;
using Wooly.Core.Timelines;
using Wooly.Tests.Fakes;
using Wooly.Tui.Rendering;
using Wooly.Tui.Screens;
using Wooly.Tui.Shell;

namespace Wooly.Tests.Tui;

/// <summary>
///     The profiles screen: every profile on this machine, which one this session is acting as and which one is
///     current, reached with <c>ctrl-p</c> from anywhere but compose (ADR-0020, #240). It reads the local config and
///     nothing else, so what these assert is what is drawn and that no instance was asked.
/// </summary>
public class ShellProfilesTests
{
    /// <summary>
    ///     A session launched with <c>--profile work</c> on a machine whose current profile is personal: the case
    ///     where the two markers point at different rows, which is the one that tells them apart.
    /// </summary>
    private static AShell ActingAsWorkWithPersonalCurrent() => new()
    {
        Profile = new ActiveProfile
        {
            Name = "work",
            Instance = "hachyderm.io",
            Account = "jeff@hachyderm.io",
            AccessToken = "token-work",
        },
        Profiles = FakeProfileRegistry.Holding(
            "personal",
            FakeProfileRegistry.Profile("personal", "mastodon.social", "jeff@mastodon.social"),
            FakeProfileRegistry.Profile("work", "hachyderm.io", "jeff@hachyderm.io"),
            FakeProfileRegistry.Profile("spare", "fosstodon.org", null)),
    };

    /// <summary>A screen on the stack like any other: <c>ctrl-p</c> goes there and <c>esc</c> comes back.</summary>
    [Fact]
    public async Task CtrlP_PushesTheProfilesScreen_AndEscWalksBackOutOfIt()
    {
        var opened = await new AShell().Opened();

        Assert.True(opened.Press(ShellKey.CtrlP));

        Assert.IsType<ProfilesScreen>(opened.Screen);
        Assert.Equal("Home › Profiles", opened.Breadcrumb);

        opened.Press(ShellKey.Escape);

        Assert.IsType<FeedScreen>(opened.Screen);
        Assert.Equal(1, opened.Depth);
    }

    /// <summary>A frame key, so it goes where the reader is standing — down a drill as much as at a destination.</summary>
    [Fact]
    public async Task CtrlP_OpensFromAScreenDrilledInto()
    {
        var shell = new AShell { Engagement = FakePostEngagement.Answered(APost.With(id: "110")) };
        var opened = await shell.Opened();

        await opened.Enter();
        shell.Host.Drain();

        opened.Press(ShellKey.CtrlP);

        Assert.IsType<ProfilesScreen>(opened.Screen);
        Assert.Equal(3, opened.Depth);
    }

    /// <summary>
    ///     Every screen but compose. Asked of the keymap across the screens there are, since a frame key that one
    ///     screen took back is the thing the frame exists to rule out.
    /// </summary>
    [Fact]
    public void CtrlP_MeansProfilesOnEveryScreenButCompose()
    {
        Screen[] screens =
        [
            new FeedScreen(new Destination(DestinationKind.Home, "Home", Timeline.Home), []),
            new NotificationsScreen([]),
            new DirectMessagesScreen([]),
            new FollowRequestsScreen([]),
            new SearchScreen(),
            new HelpScreen(new SearchScreen()),
            new NoticeScreen("Hashtag", "No hashtag set."),
            new ProfilesScreen([], "personal", null),
        ];

        Assert.All(screens, screen => Assert.Equal(Verb.Profiles, Keymap.Means(ShellKey.CtrlP, screen)));
        Assert.Equal(Verb.None, Keymap.Means(ShellKey.CtrlP, new ComposeScreen(ComposeFor.Post)));
    }

    /// <summary>
    ///     The keymap opened over compose is still over the draft, and lists no <c>ctrl-p</c> — so the key does
    ///     nothing there either, rather than being answered where it is not announced.
    /// </summary>
    [Fact]
    public async Task CtrlP_OnTheKeymapOverCompose_DoesNothing()
    {
        var opened = await new AShell().Opened();

        opened.Press(ShellKey.C);
        opened.Press(ShellKey.Question);

        Assert.False(opened.Press(ShellKey.CtrlP));
        Assert.IsType<HelpScreen>(opened.Screen);
    }

    /// <summary>Switching would drop a draft, and drafts do not survive one — so on compose the key does nothing.</summary>
    [Fact]
    public async Task CtrlP_OnCompose_DoesNothing_AndIsNotOnTheStatusRow()
    {
        var opened = await new AShell().Opened();

        opened.Press(ShellKey.C);

        Assert.False(opened.Press(ShellKey.CtrlP));
        Assert.IsType<ComposeScreen>(opened.Screen);
        Assert.DoesNotContain(opened.Keys, key => key.Key == "ctrl-p");
    }

    /// <summary>Pressed again on the screen itself, it is where the reader already is — not a second one on top.</summary>
    [Fact]
    public async Task CtrlP_OnTheProfilesScreen_PushesNoSecond()
    {
        var opened = await new AShell().Opened();

        opened.Press(ShellKey.CtrlP);
        opened.Press(ShellKey.CtrlP);

        Assert.Equal(2, opened.Depth);
    }

    /// <summary>The whole of what the screen is for: a row a profile, and which is which.</summary>
    [Fact]
    public async Task Rows_MarkActingAsAndCurrentApart_WhenTheyAreDifferentProfiles()
    {
        var opened = await ActingAsWorkWithPersonalCurrent().Opened();

        opened.Press(ShellKey.CtrlP);

        var drawn = AShell.Drawn(opened.Screen);

        Assert.Contains(drawn, row => row.StartsWith("personal") && row.Contains("current") && !row.Contains("acting as"));
        Assert.Contains(drawn, row => row.StartsWith("work") && row.Contains("acting as") && !row.Contains("current"));
        Assert.Contains(drawn, row => row.StartsWith("spare") && !row.Contains("acting as") && !row.Contains("current"));
    }

    /// <summary>
    ///     The full address, since the handle alone is two people on two instances — and the instance alone where no
    ///     account was ever established, which is all there is to say.
    /// </summary>
    [Fact]
    public async Task Rows_ShowTheFullAddress_OrTheInstanceWhereNoAccountIsEstablished()
    {
        var opened = await ActingAsWorkWithPersonalCurrent().Opened();

        opened.Press(ShellKey.CtrlP);

        var drawn = AShell.Drawn(opened.Screen);

        Assert.Contains("@jeff@mastodon.social", drawn.Select(row => row.Trim()));
        Assert.Contains("@jeff@hachyderm.io", drawn.Select(row => row.Trim()));
        Assert.Contains("fosstodon.org", drawn.Select(row => row.Trim()));
    }

    /// <summary>One profile both acting as and current carries both words.</summary>
    [Fact]
    public async Task Rows_MarkOneProfileWithBoth_WhereItIsActingAsAndCurrent()
    {
        var opened = await new AShell().Opened();

        opened.Press(ShellKey.CtrlP);

        var row = Assert.Single(AShell.Drawn(opened.Screen), row => row.StartsWith("personal"));
        Assert.Contains("acting as", row);
        Assert.Contains("current", row);
    }

    /// <summary>Walked with <c>j</c>/<c>k</c>, like every other list — the pick is what the later keys act on.</summary>
    [Fact]
    public async Task Rows_AreWalked()
    {
        var opened = await ActingAsWorkWithPersonalCurrent().Opened();

        opened.Press(ShellKey.CtrlP);
        opened.Move(1);

        var screen = Assert.IsType<ProfilesScreen>(opened.Screen);
        Assert.Equal("spare", screen.PickedProfile?.Name);
        Assert.Contains(opened.Keys, key => key.Key == "j/k");
    }

    /// <summary>At the contract's 61 columns, with long names and addresses, and no row runs off the side.</summary>
    [Fact]
    public async Task Screen_FitsIn61Columns()
    {
        var shell = new AShell
        {
            Profiles = FakeProfileRegistry.Holding(
                "personal",
                FakeProfileRegistry.Profile(
                    "a-profile-name-somebody-chose-to-make-very-long-indeed",
                    "an-instance-with-a-long-name.example",
                    "somebody-with-a-long-handle@an-instance-with-a-long-name.example"),
                FakeProfileRegistry.Profile("personal", "mastodon.social", "jeff@mastodon.social")),
        };
        shell.Profiles.TokenStorage = CredentialStorage.PlaintextFile;

        var opened = await shell.Opened();

        opened.Press(ShellKey.CtrlP);

        var lines = opened.Screen.Lines(new Drawing(61, AShell.Now));

        Assert.All(lines, line => Assert.True(Glyphs.Columns(line.Text) <= 61, line.Text));
    }

    /// <summary>
    ///     ADR-0003's tradeoff, never silent: said wherever tokens are in the clear, naming the file, in the words the
    ///     CLI uses.
    /// </summary>
    [Fact]
    public async Task PlaintextWarning_IsDrawn_WhereTokensAreKeptInTheClear()
    {
        var shell = new AShell();
        shell.Profiles.TokenStorage = CredentialStorage.PlaintextFile;

        var opened = await shell.Opened();

        opened.Press(ShellKey.CtrlP);

        var text = string.Join(" ", AShell.Drawn(opened.Screen).Select(row => row.Trim()));
        Assert.Contains(TokenStorageDescription.For(CredentialStorage.PlaintextFile, shell.Paths), text);
    }

    /// <summary>And only there: a keyring is nothing to warn anybody about.</summary>
    [Fact]
    public async Task PlaintextWarning_IsNotDrawn_WhereTokensAreInTheKeyring()
    {
        var shell = new AShell();
        var opened = await shell.Opened();

        opened.Press(ShellKey.CtrlP);

        var text = string.Join(" ", AShell.Drawn(opened.Screen));
        Assert.DoesNotContain("in the clear", text);
        Assert.DoesNotContain(shell.Paths.CredentialFile, text);
    }

    /// <summary>The local config and nothing else: no request, and so no fetch mark on the breadcrumb.</summary>
    [Fact]
    public async Task Opening_SendsNoRequest()
    {
        var shell = ActingAsWorkWithPersonalCurrent();
        var opened = await shell.Opened();
        var before = shell.Requests;

        opened.Press(ShellKey.CtrlP);
        shell.Host.Drain();
        shell.Host.SettleAll();

        Assert.Equal(before, shell.Requests);
        Assert.False(opened.Fetching);
    }

    /// <summary>The help screen lists it with the frame's keys, except over compose, where it does nothing.</summary>
    [Fact]
    public async Task Help_ListsCtrlP_ExceptOverCompose()
    {
        var opened = await new AShell().Opened();

        opened.Press(ShellKey.Question);

        Assert.Contains(Rows(opened.Screen), row => row.StartsWith("ctrl-p"));

        opened.Press(ShellKey.Escape);
        opened.Press(ShellKey.C);
        opened.Press(ShellKey.Question);

        Assert.DoesNotContain(Rows(opened.Screen), row => row.StartsWith("ctrl-p"));
    }

    /// <summary>With one profile there is nothing to tell apart, so the rail's foot names no instance (#241).</summary>
    [Fact]
    public void Instance_IsNothingWithOneProfile()
    {
        var shell = new AShell().Build();

        Assert.Null(shell.Instance);
    }

    /// <summary>
    ///     With two or more, the rail's foot names the instance the session is acting as — not the profile's name,
    ///     which is the reader's own label, and not the current profile's, which this session may not be (#241).
    /// </summary>
    [Fact]
    public void Instance_IsTheOneActedAsWithTwoOrMoreProfiles()
    {
        var shell = ActingAsWorkWithPersonalCurrent().Build();

        Assert.Equal("hachyderm.io", shell.Instance);
    }

    /// <summary>What a screen with no list on it draws, which has no gutter for <see cref="AShell.Drawn" /> to take off.</summary>
    private static IEnumerable<string> Rows(Screen screen) =>
        screen.Lines(new Drawing(61, AShell.Now)).Select(line => line.Text);
}
