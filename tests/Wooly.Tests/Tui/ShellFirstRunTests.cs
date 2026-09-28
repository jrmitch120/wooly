using Terminal.Gui.Input;
using Wooly.Core;
using Wooly.Core.Configuration;
using Wooly.Core.Credentials;
using Wooly.Core.Errors;
using Wooly.Core.Paging;
using Wooly.Core.Posts;
using Wooly.Core.Profiles;
using Wooly.Tests.Fakes;
using Wooly.Tui.Rendering;
using Wooly.Tui.Screens;
using Wooly.Tui.Shell;
using Wooly.Tui.Theme;
using Wooly.Tui.Views;

namespace Wooly.Tests.Tui;

/// <summary>
///     A shell with nobody to act as: no profile set up, none the default, or the one it would act as holding a token
///     that is missing or refused. It opens onto adding a profile rather than failing before a screen exists, with no
///     rail and nowhere else to go, and a profile added there starts it as a launch would (ADR-0020, #247).
/// </summary>
public class ShellFirstRunTests
{
    /// <summary>The instance the fakes sign in at.</summary>
    private const string Instance = "hachyderm.io";

    /// <summary>No profile set up: the add screen is the only screen, there is no rail, and nothing is asked.</summary>
    [Fact]
    public async Task NoProfiles_OpensOnTheAddScreen_WithNoRail()
    {
        var shell = new AShell { Profiles = FakeProfileRegistry.Holding(current: null) };

        var opened = await shell.Launched();

        Assert.IsType<AddProfileScreen>(opened.Screen);
        Assert.Equal(1, opened.Depth);
        Assert.False(opened.ShowsRail);
        Assert.True(opened.Screen.IsTyping);
        Assert.Equal(0, shell.Requests);
    }

    /// <summary>Profiles set up with none the default is nobody to act as either.</summary>
    [Fact]
    public async Task NoneTheDefault_OpensOnTheAddScreen()
    {
        var shell = new AShell
        {
            Profiles = FakeProfileRegistry.Holding(
                current: null,
                FakeProfileRegistry.Profile("work", Instance, $"jeff@{Instance}")),
        };

        var opened = await shell.Launched();

        Assert.IsType<AddProfileScreen>(opened.Screen);
        Assert.False(opened.ShowsRail);
        Assert.Equal(0, shell.Requests);
    }

    /// <summary>
    ///     Nowhere to go but the one screen: <c>esc</c> is not offered where there is nothing under it, <c>ctrl-q</c>
    ///     is, and the frame's other keys — which all need somebody to act as — do nothing.
    /// </summary>
    [Fact]
    public async Task FrameKeys_GoNowhere_AndCtrlQIsOffered()
    {
        var opened = await new AShell { Profiles = FakeProfileRegistry.Holding(current: null) }.Launched();

        Assert.Contains(opened.Keys, key => key.Key == "ctrl-q");
        Assert.DoesNotContain(opened.Keys, key => key.Key == "esc");

        foreach (var key in new[]
                 {
                     ShellKey.Escape, ShellKey.Tab, ShellKey.ShiftTab, ShellKey.Slash, ShellKey.CtrlP,
                     ShellKey.Question,
                 })
        {
            opened.Press(key);

            Assert.IsType<AddProfileScreen>(opened.Screen);
            Assert.Equal(1, opened.Depth);
            Assert.False(opened.ShowsRail);
        }
    }

    /// <summary><c>ctrl-q</c> ends the run from the add screen, as it does from anywhere.</summary>
    [Fact]
    public async Task CtrlQ_EndsTheRun()
    {
        var quits = 0;
        var built = new AShell { Profiles = FakeProfileRegistry.Holding(current: null) };
        var shell = await built.Launched();

        using var window = new ShellWindow(
            shell,
            Themes.Plain,
            built.Clock,
            () => quits++,
            FakePictures.DrawingNothing());

        Assert.True(window.NewKeyDownEvent(Key.Q.WithCtrl));
        Assert.Equal(1, quits);
    }

    /// <summary>
    ///     The window draws no rail with nobody to act as — the content region takes the whole width — and puts it back
    ///     once a profile has been added.
    /// </summary>
    [Fact]
    public async Task Window_DrawsNoRail_UntilAProfileIsAdded()
    {
        var built = new AShell { Profiles = FakeProfileRegistry.Holding(current: null) };
        var shell = await built.Launched();

        using var window = new ShellWindow(shell, Themes.Plain, built.Clock, () => { }, FakePictures.DrawingNothing())
        {
            Width = 80,
            Height = 24,
        };

        window.Layout();

        Assert.Equal(0, Content(window).Frame.X);
        Assert.DoesNotContain(window.SubViews.OfType<PaintedView>(), view => view.Visible && view.Frame is { X: 0, Width: RailLines.Width });

        Type(shell, Instance);
        shell.Press(ShellKey.Enter);
        built.Host.Drain();
        shell.Press(ShellKey.Enter);
        built.Host.Drain();
        window.Layout();

        Assert.Equal(RailLines.Width + 1, Content(window).Frame.X);
        Assert.Contains(window.SubViews.OfType<PaintedView>(), view => view.Visible && view.Frame is { X: 0, Width: RailLines.Width });
    }

    private static PaintedView Content(ShellWindow window) =>
        window.SubViews.OfType<PaintedView>().Single(view => view.Id == ShellWindow.ContentId);

    /// <summary>
    ///     A profile added on first run starts the shell as a launch would: on Home, with the rail, reading as the
    ///     profile just added — which became the default, being the first.
    /// </summary>
    [Fact]
    public async Task AddingAProfile_StartsOnHome_ActingAsIt()
    {
        var shell = new AShell { Profiles = FakeProfileRegistry.Holding(current: null) };
        var opened = await shell.Launched();

        Type(opened, Instance);
        opened.Press(ShellKey.Enter);
        shell.Host.Drain();
        opened.Press(ShellKey.Enter);
        shell.Host.Drain();

        Assert.IsType<FeedScreen>(opened.Screen);
        Assert.Equal("Home", opened.Breadcrumb);
        Assert.True(opened.ShowsRail);
        Assert.Equal(DestinationKind.Home, opened.Rail.Showing.Kind);
        Assert.Contains(opened.Rail.Destinations, destination => destination.Label == "@jeff");
        Assert.Equal(["jeff"], shell.Profiles.Added.Select(added => added.Name));
        Assert.True(shell.Profiles.List().Single().IsCurrent);
        Assert.NotEmpty(shell.Timelines.Reads);
        Assert.All(shell.Tokens, token => Assert.Equal("token-jeff", token));
    }

    /// <summary>
    ///     <c>esc</c> while the browser is out gives the sign-in up and puts the reader back at the instance — the
    ///     loopback port given back and nothing written — rather than walking off a screen with nothing under it.
    /// </summary>
    [Fact]
    public async Task Esc_WhileWaitingOnTheBrowser_StartsOver_AndWritesNothing()
    {
        var shell = new AShell
        {
            Profiles = FakeProfileRegistry.Holding(current: null),
            Authorizer = FakeBrowserAuthorizer.Holding(),
        };
        var opened = await shell.Launched();

        Type(opened, Instance);
        opened.Press(ShellKey.Enter);
        shell.Host.Drain();

        Assert.Contains(opened.Keys, key => key.Key == "esc");

        opened.Press(ShellKey.Escape);
        shell.Host.Drain();

        var adding = Assert.IsType<AddProfileScreen>(opened.Screen);
        Assert.Equal(AddProfileScreen.Step.Instance, adding.At);
        Assert.Equal(Instance, adding.Instance);
        Assert.True(shell.Authorizer.Disposed);
        Assert.Empty(shell.Profiles.Added);
    }

    /// <summary>
    ///     A token missing from the store opens the add screen filled in for that profile — its instance and name —
    ///     so that signing in replaces its token rather than adding a second profile, and asks nothing about the name.
    /// </summary>
    [Fact]
    public async Task MissingToken_SignsThatProfileInAgain()
    {
        var registry = FakeProfileRegistry.Holding(
            "work",
            FakeProfileRegistry.Profile("work", Instance, $"jeff@{Instance}"));
        registry.Tokenless.Add("work");

        var shell = new AShell { Profiles = registry };
        var opened = await shell.Launched();

        var adding = Assert.IsType<AddProfileScreen>(opened.Screen);
        Assert.False(opened.ShowsRail);
        Assert.Equal(Instance, adding.Instance);
        Assert.Equal("work", adding.Name);
        Assert.False(adding.IsTyping);
        Assert.Contains(Text(opened), row => row == $"Instance: {Instance}");
        Assert.Contains("no access token stored", Joined(opened));

        registry.Tokenless.Remove("work");
        opened.Press(ShellKey.Enter);
        shell.Host.Drain();

        Assert.Equal([Instance], shell.Authorizer.Instances);
        Assert.Equal(("work", Instance, "token-from-browser"), Written(registry));
        Assert.Null(opened.Asking);
        Assert.IsType<FeedScreen>(opened.Screen);
        Assert.True(opened.ShowsRail);
        Assert.All(shell.Tokens, token => Assert.Equal("token-work", token));
    }

    /// <summary>
    ///     A token the instance refuses at launch — revoked since it was stored — does the same: the add screen, filled
    ///     in for that profile, in place of the Home that could not be read.
    /// </summary>
    [Fact]
    public async Task RefusedTokenAtLaunch_SignsThatProfileInAgain()
    {
        var refusing = true;
        var shell = new AShell { Timelines = Refusing(() => refusing) };

        var opened = await shell.Launched();

        var adding = Assert.IsType<AddProfileScreen>(opened.Screen);
        Assert.Equal(1, opened.Depth);
        Assert.False(opened.ShowsRail);
        Assert.Equal("mastodon.social", adding.Instance);
        Assert.Equal("personal", adding.Name);
        Assert.Contains("mastodon.social refused the token.", Joined(opened));

        refusing = false;
        opened.Press(ShellKey.Enter);
        shell.Host.Drain();

        Assert.Equal(("personal", "mastodon.social", "token-from-browser"), Written(shell.Profiles));
        Assert.IsType<FeedScreen>(opened.Screen);
        Assert.True(opened.ShowsRail);
        Assert.Null(opened.Notice);
    }

    /// <summary>
    ///     A token refused once something has been read as it is not a launch any more: the reader stays where they
    ///     are, told on the status row (#248), and is not taken anywhere.
    /// </summary>
    [Fact]
    public async Task RefusedTokenAfterAnAnswer_DoesNotNavigate()
    {
        var refusing = false;
        var shell = new AShell { Timelines = Refusing(() => refusing) };
        var opened = await shell.Launched();

        refusing = true;
        opened.Press(ShellKey.G);
        shell.Host.Drain();

        Assert.IsType<FeedScreen>(opened.Screen);
        Assert.True(opened.ShowsRail);
        Assert.Equal("mastodon.social refused the token.", opened.Notice);
    }

    /// <summary>A profile that can be acted as opens as it always has, on Home with the rail.</summary>
    [Fact]
    public async Task AProfileToActAs_OpensOnHome()
    {
        var shell = new AShell();

        var opened = await shell.Launched();

        Assert.IsType<FeedScreen>(opened.Screen);
        Assert.True(opened.ShowsRail);
        Assert.All(shell.Tokens, token => Assert.Equal("token-personal", token));
    }

    /// <summary>
    ///     A <c>--profile</c> naming nothing is a typo on the command line, and is still said there rather than opened
    ///     onto a form: the same failure, before any screen exists.
    /// </summary>
    [Fact]
    public void UnknownNamedProfile_StillFails()
    {
        var registry = FakeProfileRegistry.Holding(
            "personal",
            FakeProfileRegistry.Profile("personal", "mastodon.social", "jeff@mastodon.social"));

        var failure = Assert.Throws<UnknownProfileException>(() => Opening.Of(registry, "wrok"));

        Assert.Contains("wrok", failure.Message);
    }

    /// <summary>
    ///     A config naming a default that is not there is a problem in the file, and is still said on stderr where it
    ///     can be fixed — over the real registry, which is what reads the file.
    /// </summary>
    [Fact]
    public void ConfigNamingAMissingDefault_StillFails()
    {
        using var directory = new TemporaryDirectory();
        var paths = new WoolyPaths(directory.Path);
        File.WriteAllText(paths.ConfigFile, "current_profile = \"gone\"\n");
        var registry = new ProfileRegistry(new TomlConfigStore(paths), new PlaintextFileCredentialStore(paths), paths);

        Assert.Throws<ConfigurationException>(() => Opening.Of(registry, named: null));
    }

    /// <summary>Over the real registry, no config file at all is first run rather than a failure.</summary>
    [Fact]
    public void NoConfigFile_IsFirstRun()
    {
        using var directory = new TemporaryDirectory();
        var paths = new WoolyPaths(directory.Path);
        var registry = new ProfileRegistry(new TomlConfigStore(paths), new PlaintextFileCredentialStore(paths), paths);

        var opening = Opening.Of(registry, named: null);

        Assert.Null(opening.Profile);
        Assert.Null(opening.Again);
    }

    /// <summary>The add screen standing alone reads at 61 columns, the filled-in one included.</summary>
    [Fact]
    public async Task FirstRun_FitsIn61Columns()
    {
        var registry = FakeProfileRegistry.Holding(
            "work",
            FakeProfileRegistry.Profile("work", "an-instance-with-a-name-far-longer-than-anybody-would-choose.example", null));
        registry.Tokenless.Add("work");

        var opened = await new AShell { Profiles = registry }.Launched();

        Assert.All(
            opened.Screen.Lines(new Drawing(61, AShell.Now)),
            line => Assert.True(Glyphs.Columns(line.Text) <= 61, line.Text));
    }

    /// <summary>A timeline that refuses the token it is read with for as long as <paramref name="refusing" /> says.</summary>
    private static FakeTimelineReader Refusing(Func<bool> refusing) =>
        FakeTimelineReader.Answering(_ => refusing()
            ? throw new AuthenticationException("mastodon.social refused the token.")
            : Fetch<Post>.Complete([APost.With()]));

    private static (string Name, string Instance, string Token) Written(FakeProfileRegistry registry)
    {
        var added = Assert.Single(registry.Added);

        return (added.Name, added.Profile.Instance, added.AccessToken);
    }

    /// <summary>Types <paramref name="text" /> a letter at a time, as the window hands a typing screen its letters.</summary>
    private static void Type(Shell shell, string text)
    {
        foreach (var letter in text)
        {
            shell.Type(letter);
        }
    }

    private static IReadOnlyList<string> Text(Shell shell) =>
        [.. shell.Screen.Lines(new Drawing(61, AShell.Now)).Select(line => line.Text)];

    private static string Joined(Shell shell) => string.Join(" ", Text(shell));
}
