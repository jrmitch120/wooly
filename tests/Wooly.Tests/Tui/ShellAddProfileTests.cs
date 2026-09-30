using Wooly.Core;
using Wooly.Core.Profiles;
using Wooly.Tests.Fakes;
using Wooly.Tui.Rendering;
using Wooly.Tui.Screens;
using Wooly.Tui.Shell;

namespace Wooly.Tests.Tui;

/// <summary>
///     Adding a profile from inside the TUI: <c>a</c> on the profiles screen, then the instance, a sign-in through the
///     browser or a pasted token, the token checked, and a name — in that order, which is the one way it differs from
///     <c>profile add</c> (ADR-0004, ADR-0020, #245). What these assert is what is drawn, which port was asked what,
///     and what reached the registry.
/// </summary>
public class ShellAddProfileTests
{
    /// <summary>The instance a new profile is added for, and the one the fakes answer as.</summary>
    private const string Instance = "hachyderm.io";

    /// <summary>The profiles screen's <c>a</c> pushes the add screen, and the screen says so.</summary>
    [Fact]
    public async Task A_OnTheProfilesScreen_PushesTheAddScreen()
    {
        var opened = await new AShell().Opened();

        opened.Press(ShellKey.CtrlP);

        Assert.Contains(opened.Keys, key => key is { Key: "a", Does: "add" });

        opened.Press(ShellKey.A);

        Assert.IsType<AddProfileScreen>(opened.Screen);
        Assert.Equal("Home › Profiles › Add a profile", opened.Breadcrumb);
        Assert.True(opened.Screen.IsTyping);
        Assert.Contains(Text(opened), row => row.StartsWith("Instance:"));
    }

    /// <summary>
    ///     A typo never reaches the network: it is turned down on screen, in the instance rule's own words.
    /// </summary>
    [Fact]
    public async Task MalformedInstance_IsRejectedInline_AndNothingIsSent()
    {
        var shell = new AShell();
        var opened = await Adding(shell);

        Type(opened, "https://hachyderm.io");
        opened.Press(ShellKey.Enter);
        shell.Host.Drain();

        Assert.Contains(InstanceDomain.Rejection("https://hachyderm.io"), Joined(opened));
        Assert.Empty(shell.Authorizer.Instances);
        Assert.Empty(shell.Verifier.Tokens);
        Assert.Empty(shell.Browser.Opened);
        Assert.False(opened.Fetching);
        Assert.IsType<AddProfileScreen>(opened.Screen);
    }

    /// <summary>
    ///     The browser is the default: the sign-in is begun against the instance typed, the browser opened at it, and
    ///     the address drawn as well, with a mark saying the redirect is being waited on.
    /// </summary>
    [Fact]
    public async Task Instance_BeginsABrowserSignIn_AndDrawsTheAddressWhileWaiting()
    {
        var shell = new AShell { Authorizer = FakeBrowserAuthorizer.Holding() };
        var opened = await Adding(shell);

        Type(opened, Instance);
        opened.Press(ShellKey.Enter);
        shell.Host.Drain();

        Assert.Equal([Instance], shell.Authorizer.Instances);
        Assert.Equal([shell.Authorizer.AuthorizationUrl], shell.Browser.Opened);

        var joined = Joined(opened);
        Assert.Contains(BrowserSignIn.Opened(Instance), joined);
        Assert.Contains(WebAddress.Of(shell.Authorizer.AuthorizationUrl), Unwrapped(opened));
        Assert.Contains(BrowserSignIn.Waiting, joined);
        Assert.True(opened.Fetching);
        Assert.Contains(opened.Keys, key => key.Key == "t");
    }

    /// <summary>A browser that would not open leaves the address as the way on, drawn all the same.</summary>
    [Fact]
    public async Task Instance_DrawsTheAddress_WhereNoBrowserOpened()
    {
        var shell = new AShell
        {
            Authorizer = FakeBrowserAuthorizer.Holding(),
            Browser = FakeWebBrowser.WithNothingToOpen(),
        };
        var opened = await Adding(shell);

        Type(opened, Instance);
        opened.Press(ShellKey.Enter);
        shell.Host.Drain();

        Assert.Contains(BrowserSignIn.NotOpened(Instance), Joined(opened));
        Assert.Contains(WebAddress.Of(shell.Authorizer.AuthorizationUrl), Unwrapped(opened));
    }

    /// <summary>
    ///     <c>esc</c> while the browser is out cancels the wait and gives the loopback port back — and a browser that
    ///     comes back afterwards writes nothing.
    /// </summary>
    [Fact]
    public async Task Esc_WhileWaitingOnTheBrowser_CancelsIt_AndWritesNothing()
    {
        var shell = new AShell { Authorizer = FakeBrowserAuthorizer.Holding() };
        var opened = await Adding(shell);

        Type(opened, Instance);
        opened.Press(ShellKey.Enter);
        shell.Host.Drain();

        opened.Press(ShellKey.Escape);
        shell.Host.Drain();

        Assert.True(shell.Authorizer.Cancelled);
        Assert.True(shell.Authorizer.Disposed);
        Assert.IsType<ProfilesScreen>(opened.Screen);
        Assert.False(opened.Fetching);

        shell.Authorizer.ComeBack();
        shell.Host.Drain();

        Assert.Empty(shell.Verifier.Tokens);
        Assert.Empty(shell.Profiles.Added);
    }

    /// <summary>
    ///     A browser that comes back turned down says why on the add screen, writes nothing, and offers the browser
    ///     again or a pasted token — never the start over.
    /// </summary>
    [Fact]
    public async Task BrowserRefusal_IsSaidInline_AndCanBeTriedAgain()
    {
        var shell = new AShell { Authorizer = FakeBrowserAuthorizer.Refusing("the request was denied") };
        var opened = await Adding(shell);

        Type(opened, Instance);
        opened.Press(ShellKey.Enter);
        shell.Host.Drain();

        Assert.Contains("the request was denied", Joined(opened));
        Assert.Null(opened.Notice);
        Assert.True(shell.Authorizer.Disposed);
        Assert.False(opened.Fetching);
        Assert.Empty(shell.Verifier.Tokens);
        Assert.Empty(shell.Profiles.Added);
        Assert.Contains(opened.Keys, key => key is { Key: "⏎", Does: "try again" });
        Assert.Contains(opened.Keys, key => key.Key == "t");

        opened.Press(ShellKey.Enter);
        shell.Host.Drain();

        Assert.Equal([Instance, Instance], shell.Authorizer.Instances);
    }

    /// <summary>
    ///     The whole of the browser's way in: the token it came back with is checked, the name defaults to the handle
    ///     just learned, and the profile is written through the registry — then the list, with the new row picked.
    /// </summary>
    [Fact]
    public async Task BrowserSignIn_VerifiesTheToken_AsksForAName_AndStoresTheProfile()
    {
        var shell = new AShell();
        var opened = await Adding(shell);

        Type(opened, Instance);
        opened.Press(ShellKey.Enter);
        shell.Host.Drain();

        Assert.Equal(["token-from-browser"], shell.Verifier.Tokens);
        Assert.True(shell.Authorizer.Disposed);
        Assert.Contains(Text(opened), row => row == "Name: jeff▌");
        Assert.Empty(shell.Profiles.Added);

        opened.Press(ShellKey.Enter);
        shell.Host.Drain();

        var (name, profile, token) = Assert.Single(shell.Profiles.Added);
        Assert.Equal("jeff", name);
        Assert.Equal(Instance, profile.Instance);
        Assert.Equal($"jeff@{Instance}", profile.Account);
        Assert.Equal("token-from-browser", token);

        var list = Assert.IsType<ProfilesScreen>(opened.Screen);
        Assert.Equal("jeff", list.PickedProfile?.Name);
        Assert.Equal(2, opened.Depth);
    }

    /// <summary>
    ///     Added is not switched to: the session goes on acting as who it was, and the new row is one <c>⏎</c> away
    ///     rather than already taken. Adding alongside a current profile leaves that one current, as <c>Add</c>
    ///     decides.
    /// </summary>
    [Fact]
    public async Task Adding_DoesNotSwitchTheSession_OrMoveCurrent()
    {
        var shell = new AShell();
        var opened = await Adding(shell);

        Type(opened, Instance);
        opened.Press(ShellKey.Enter);
        shell.Host.Drain();
        opened.Press(ShellKey.Enter);
        shell.Host.Drain();

        var drawn = AShell.Drawn(opened.Screen);
        Assert.Contains(
            drawn,
            row => row.StartsWith("personal") && row.Contains("acting as") && row.Contains("default"));
        Assert.Contains(drawn, row => row.StartsWith("jeff") && !row.Contains("acting as") && !row.Contains("default"));

        opened.Press(ShellKey.Escape);

        Assert.IsType<FeedScreen>(opened.Screen);
        Assert.Equal(["personal"], shell.Timelines.Reads.Select(read => read.Profile).Distinct());
    }

    /// <summary>The name is the handle to begin with, and the reader's to change.</summary>
    [Fact]
    public async Task Name_CanBeEdited()
    {
        var shell = new AShell();
        var opened = await Adding(shell);

        Type(opened, Instance);
        opened.Press(ShellKey.Enter);
        shell.Host.Drain();

        opened.Backspace();
        opened.Backspace();
        opened.Backspace();
        opened.Backspace();
        Type(opened, "work");
        opened.Press(ShellKey.Enter);
        shell.Host.Drain();

        Assert.Equal("work", Assert.Single(shell.Profiles.Added).Name);
    }

    /// <summary>A name with nothing in it names nothing, and is said so rather than stored.</summary>
    [Fact]
    public async Task Name_ThatIsEmpty_IsRefusedInline()
    {
        var shell = new AShell();
        var opened = await Adding(shell);

        Type(opened, Instance);
        opened.Press(ShellKey.Enter);
        shell.Host.Drain();

        for (var at = 0; at < 4; at++)
        {
            opened.Backspace();
        }

        opened.Press(ShellKey.Enter);
        shell.Host.Drain();

        Assert.Empty(shell.Profiles.Added);
        Assert.Contains("Give the profile a name", Joined(opened));
    }

    /// <summary>
    ///     A name already in use asks before replacing that profile, because adding replaces (story 43) — and
    ///     declining writes nothing and leaves the reader where they were.
    /// </summary>
    [Fact]
    public async Task Name_AlreadyInUse_AsksFirst_AndDecliningWritesNothing()
    {
        var shell = new AShell
        {
            Profiles = FakeProfileRegistry.Holding(
                "personal",
                FakeProfileRegistry.Profile("personal", "mastodon.social", "jeff@mastodon.social"),
                FakeProfileRegistry.Profile("work", Instance, $"jeff@{Instance}")),
        };
        var opened = await Adding(shell);

        Type(opened, Instance);
        opened.Press(ShellKey.Enter);
        shell.Host.Drain();

        for (var at = 0; at < 4; at++)
        {
            opened.Backspace();
        }

        Type(opened, "work");
        opened.Press(ShellKey.Enter);

        var asking = Assert.IsType<Confirmation>(opened.Asking);
        Assert.Contains("work", asking.Ask);

        await opened.Answer(agreed: false);
        shell.Host.Drain();

        Assert.Empty(shell.Profiles.Added);
        Assert.IsType<AddProfileScreen>(opened.Screen);

        opened.Press(ShellKey.Enter);
        await opened.Answer(agreed: true);
        shell.Host.Drain();

        Assert.Equal("work", Assert.Single(shell.Profiles.Added).Name);
        Assert.Equal("work", Assert.IsType<ProfilesScreen>(opened.Screen).PickedProfile?.Name);
    }

    /// <summary>
    ///     One key swaps the browser for a pasted token: the wait is given up, and the field that takes the token draws
    ///     nothing of it in the clear. What is checked is the token trimmed, as the CLI trims one.
    /// </summary>
    [Fact]
    public async Task PastingAToken_IsMasked_AndTrimmed()
    {
        var shell = new AShell { Authorizer = FakeBrowserAuthorizer.Holding() };
        var opened = await Adding(shell);

        Type(opened, Instance);
        opened.Press(ShellKey.Enter);
        shell.Host.Drain();

        Assert.True(opened.Press(ShellKey.T));
        shell.Host.Drain();

        Assert.True(shell.Authorizer.Cancelled);
        Assert.True(shell.Authorizer.Disposed);
        Assert.True(opened.Screen.IsTyping);

        Type(opened, "  secret-token  ");

        Assert.DoesNotContain("secret", Joined(opened));
        Assert.Contains(Text(opened), row => row.StartsWith("Access token: ***"));

        opened.Press(ShellKey.Enter);
        shell.Host.Drain();

        Assert.Equal(["secret-token"], shell.Verifier.Tokens);
        Assert.DoesNotContain("secret", Joined(opened));
    }

    /// <summary>
    ///     A token the instance turns down is said so on screen and never stored — and the reader tries again from
    ///     the token, not from the start.
    /// </summary>
    [Fact]
    public async Task RefusedToken_IsSaidInline_NeverStored_AndCanBeTriedAgain()
    {
        var shell = new AShell
        {
            Authorizer = FakeBrowserAuthorizer.Holding(),
            Verifier = FakeAccessTokenVerifier.RefusingOnly("mistyped", "The access token is invalid"),
        };
        var opened = await Adding(shell);

        Type(opened, Instance);
        opened.Press(ShellKey.Enter);
        shell.Host.Drain();
        opened.Press(ShellKey.T);
        Type(opened, "mistyped");
        opened.Press(ShellKey.Enter);
        shell.Host.Drain();

        Assert.Contains("The access token is invalid", Joined(opened));
        Assert.Null(opened.Notice);
        Assert.Empty(shell.Profiles.Added);
        Assert.True(opened.Screen.IsTyping);
        Assert.Contains(Text(opened), row => row == "Access token: ▌");

        Type(opened, "right");
        opened.Press(ShellKey.Enter);
        shell.Host.Drain();
        opened.Press(ShellKey.Enter);
        shell.Host.Drain();

        Assert.Equal([Instance], shell.Authorizer.Instances);
        Assert.Equal("right", Assert.Single(shell.Profiles.Added).AccessToken);
    }

    /// <summary>
    ///     The check is an enquiry like any other: the fetch mark while it is out, and <c>esc</c> walks away from it
    ///     with nothing written when it lands.
    /// </summary>
    [Fact]
    public async Task Esc_WhileTheTokenIsChecked_WalksAway_AndWritesNothing()
    {
        var shell = new AShell { Authorizer = FakeBrowserAuthorizer.Holding() };
        var opened = await Adding(shell);

        Type(opened, Instance);
        opened.Press(ShellKey.Enter);
        shell.Host.Drain();
        opened.Press(ShellKey.T);
        Type(opened, "pasted");
        opened.Press(ShellKey.Enter);

        opened.Press(ShellKey.Escape);
        shell.Host.Drain();

        Assert.IsType<ProfilesScreen>(opened.Screen);
        Assert.Empty(shell.Profiles.Added);
        Assert.False(opened.Fetching);
    }

    /// <summary>
    ///     Every step at the contract's 61 columns, and none of it colour alone: the instance a very long one, and the
    ///     address the fake's escaped authorization request.
    /// </summary>
    [Fact]
    public async Task EveryStep_FitsIn61Columns()
    {
        var shell = new AShell { Authorizer = FakeBrowserAuthorizer.Holding() };
        var opened = await Adding(shell);
        var steps = new List<IReadOnlyList<Line>>();

        Type(opened, "an-instance-with-a-name-far-longer-than-anybody-would-choose.example");
        steps.Add(Lines(opened));

        opened.Press(ShellKey.Enter);
        shell.Host.Drain();
        steps.Add(Lines(opened));

        opened.Press(ShellKey.T);
        Type(opened, new string('x', 120));
        steps.Add(Lines(opened));

        opened.Press(ShellKey.Enter);
        steps.Add(Lines(opened));
        shell.Host.Drain();

        Type(opened, new string('y', 80));
        steps.Add(Lines(opened));

        Assert.All(steps.SelectMany(lines => lines), line => Assert.True(Glyphs.Columns(line.Text) <= 61, line.Text));
    }

    /// <summary>Waits on nobody: the instance field is where the add screen opens, typing.</summary>
    private static async Task<Shell> Adding(AShell shell)
    {
        var opened = await shell.Opened();

        opened.Press(ShellKey.CtrlP);
        opened.Press(ShellKey.A);

        return opened;
    }

    /// <summary>
    ///     Types <paramref name="text" /> a letter at a time, as the window hands a typing screen its letters.
    /// </summary>
    private static void Type(Shell shell, string text)
    {
        foreach (var letter in text)
        {
            shell.Type(letter);
        }
    }

    private static IReadOnlyList<Line> Lines(Shell shell) =>
        shell.Screen.Lines(new Drawing(61, AShell.Now));

    /// <summary>What the screen draws at 61 columns, a row at a time — no list, and so no gutter to take off.</summary>
    private static IReadOnlyList<string> Text(Shell shell) =>
        [.. Lines(shell).Select(line => line.Text)];

    /// <summary>The rows run back together at the spaces they were wrapped at, so a sentence reads as one.</summary>
    private static string Joined(Shell shell) => string.Join(" ", Text(shell));

    /// <summary>The rows run back together with nothing between, so an address cut across rows reads as one.</summary>
    private static string Unwrapped(Shell shell) => string.Concat(Text(shell));
}
