using Microsoft.Extensions.DependencyInjection;
using Spectre.Console.Testing;
using Wooly.Cli;
using Wooly.Core;
using Wooly.Core.Credentials;
using Wooly.Core.Profiles;
using Wooly.Tests.Fakes;

namespace Wooly.Tests.Cli;

/// <summary>
///     Profile management driven the way a user drives it: whole commands through the real command app, over a real
///     config file and a real token store in a scratch directory, with only the instance faked. Runs within a test
///     share that directory, so what one command persisted is what the next one finds — which is the ticket's point.
/// </summary>
public class ProfileCommandTests : IDisposable
{
    private readonly TemporaryDirectory _directory = new();
    private readonly FakeAccessTokenVerifier _verifier = FakeAccessTokenVerifier.Accepting();
    private readonly FakeBrowserAuthorizer _authorizer = FakeBrowserAuthorizer.Authorizing();

    /// <summary>A machine with a browser, as most are. Replaced where having none is the subject.</summary>
    private FakeWebBrowser _browser = new();

    /// <summary>A keyring that answers, as on a developer's machine. Replaced where the fallback is the subject.</summary>
    private ICredentialStore _credentialStore =
        OsKeyringCredentialStore.Open(() => new GcmKeyring(GcmKeyring.BackingStoreForThisMachine, new FakeOsKeyring()));

    public void Dispose() => _directory.Dispose();

    [Fact]
    public void Add_SetsUpAProfileFromAPastedTokenAndMakesItTheOneCommandsUse()
    {
        var run = Add("personal", "mastodon.social", "token-personal");

        Assert.Equal((int)ExitCode.Success, run.ExitCode);
        Assert.Contains("personal", run.Output);
        Assert.Contains("jeff@mastodon.social", run.Output);
        Assert.Empty(run.ErrorOutput.Trim());

        Assert.Contains("personal", Run(["profile", "show"]).Output);
    }

    /// <summary>
    ///     ADR-0004's primary path, and so the one an invocation that asks for nothing else gets: the user is sent to
    ///     their instance in a browser, and what comes back is a profile like any other.
    /// </summary>
    [Fact]
    public void Add_ConnectsThroughTheBrowserWhenNothingAsksForAnythingElse()
    {
        var run = Run(["profile", "add", "personal", "--instance", "mastodon.social"]);

        Assert.Equal((int)ExitCode.Success, run.ExitCode);
        Assert.Equal(["mastodon.social"], _authorizer.Instances);
        Assert.Equal([_authorizer.AuthorizationUrl], _browser.Opened);
        Assert.Contains("jeff@mastodon.social", run.Output);
        Assert.Contains("personal", Run(["profile", "show"]).Output);

        // The port was the machine's, borrowed for the length of the sign-in — a command that kept it would leave one
        // held for as long as the process lives.
        Assert.True(_authorizer.Disposed);
    }

    /// <summary>
    ///     The whole point of the ticket, in one assertion: a token that arrived through the browser is in the same
    ///     store, under the same profile, as one the user pasted — nothing downstream can tell which flow made it.
    /// </summary>
    [Fact]
    public void Add_StoresATokenFromTheBrowserWhereAPastedOneGoes()
    {
        var run = Run(["profile", "add", "personal", "--instance", "mastodon.social"]);

        Assert.Equal((int)ExitCode.Success, run.ExitCode);
        Assert.Equal("token-from-browser", _credentialStore.FindAccessToken("personal"));
        Assert.Equal(["token-from-browser"], _verifier.Tokens);
        Assert.DoesNotContain("token-from-browser", run.Output);
    }

    /// <summary>
    ///     A browser that will not open is not the end of the flow — the address is the part that matters, and a user
    ///     who can read it can finish the sign-in from another machine.
    /// </summary>
    [Fact]
    public void Add_ShowsTheAddressToAuthorizeAtWhenNoBrowserCanBeOpened()
    {
        _browser = FakeWebBrowser.WithNothingToOpen();

        var run = Run(["profile", "add", "personal", "--instance", "mastodon.social"]);

        Assert.Equal((int)ExitCode.Success, run.ExitCode);
        Assert.Contains(_authorizer.AuthorizationUrl.ToString(), run.Output);
    }

    /// <summary>A sign-in the user turns down leaves nothing behind, exactly as a refused token does.</summary>
    [Fact]
    public void Add_ReportsASignInTheUserTurnedDownAndWritesNoProfile()
    {
        var run = Run(
            ["profile", "add", "personal", "--instance", "mastodon.social"],
            authorizer: FakeBrowserAuthorizer.Refusing("the request was denied"));

        Assert.Equal((int)ExitCode.AuthenticationError, run.ExitCode);
        Assert.Contains("the request was denied", run.ErrorOutput);
        Assert.False(File.Exists(Path.Combine(_directory.Path, "config.toml")));
    }

    /// <summary>
    ///     ADR-0004's headless fallback, asked for explicitly. A token on the command line is a secret in the shell's
    ///     history file, so this path asks for it rather than being given it.
    /// </summary>
    [Fact]
    public void Add_AsksForATokenToPasteInsteadOfOpeningABrowserWhenToldTo()
    {
        var run = Run(
            ["profile", "add", "personal", "--instance", "mastodon.social", "--manual"],
            typed: "token-typed");

        Assert.Equal((int)ExitCode.Success, run.ExitCode);
        Assert.Equal(["token-typed"], _verifier.Tokens);
        Assert.DoesNotContain("token-typed", run.Output);
        Assert.Empty(_browser.Opened);
        Assert.Empty(_authorizer.Instances);
    }

    /// <summary>A token given outright is the fallback too, and must not open a browser either.</summary>
    [Fact]
    public void Add_OpensNoBrowserWhenHandedATokenOutright()
    {
        Add("personal", "mastodon.social", "token-personal");

        Assert.Empty(_browser.Opened);
        Assert.Empty(_authorizer.Instances);
    }

    /// <summary>
    ///     ADR-0004's fallback exists for the machine with no browser, and a machine with no terminal has no way
    ///     through the browser path either — nobody would see the address, and the wait would end minutes later in a
    ///     failure the user was never offered a way around. So it is refused at once, naming the way through.
    /// </summary>
    [Fact]
    public void Add_RefusesABrowserSignInWithNoTerminalToConductItAt()
    {
        var run = Run(["profile", "add", "personal", "--instance", "mastodon.social"], atATerminal: false);

        Assert.Equal((int)ExitCode.UsageError, run.ExitCode);
        Assert.Contains("--token", run.ErrorOutput);
        Assert.Empty(_authorizer.Instances);
        Assert.Empty(_browser.Opened);
    }

    /// <summary>A token given outright still works with no terminal — it is what the fallback is for.</summary>
    [Fact]
    public void Add_ConnectsWithATokenGivenOutrightEvenWithNoTerminal()
    {
        var run = Run(
            ["profile", "add", "personal", "--instance", "mastodon.social", "--token", "token-personal"],
            atATerminal: false);

        Assert.Equal((int)ExitCode.Success, run.ExitCode);
        Assert.Equal("token-personal", _credentialStore.FindAccessToken("personal"));
    }

    /// <summary>
    ///     Both flags name the same fallback and only one can be honoured, so passing both meant something this
    ///     command cannot do. Letting the token quietly win would be the silence strict parsing was turned on to stop.
    /// </summary>
    [Fact]
    public void Add_RefusesToBeToldTwiceHowToDoTheManualPath()
    {
        var run = Run(
            ["profile", "add", "personal", "--instance", "mastodon.social", "--token", "token-personal", "--manual"]);

        Assert.Equal((int)ExitCode.UsageError, run.ExitCode);
        Assert.Contains("--manual", run.ErrorOutput);
        Assert.Empty(_verifier.Tokens);
        Assert.False(File.Exists(Path.Combine(_directory.Path, "config.toml")));
    }

    /// <summary>
    ///     ADR-0004 rules out password-grant authentication outright, so there is no option to give one to. The point
    ///     of testing it is that "no such option" is a thing a later change could quietly stop being true.
    /// </summary>
    [Fact]
    public void Add_HasNoWayToBeHandedAPassword()
    {
        var run = Run(
            ["profile", "add", "personal", "--instance", "mastodon.social", "--password", "hunter2"]);

        Assert.Equal((int)ExitCode.UsageError, run.ExitCode);
        Assert.Contains("password", run.ErrorOutput);
        Assert.Empty(run.Output.Trim());
    }

    [Fact]
    public void Add_KeepsProfilesForDifferentAccountsSideBySide()
    {
        Add("personal", "mastodon.social", "token-personal");
        Add("work", "hachyderm.io", "token-work");

        var run = Run(["profile", "list"]);

        Assert.Equal((int)ExitCode.Success, run.ExitCode);
        Assert.Contains("mastodon.social", run.Output);
        Assert.Contains("hachyderm.io", run.Output);
        Assert.Matches(@"\*\s+personal", run.Output);
    }

    /// <summary>
    ///     The plaintext fallback is a tradeoff ADR-0003 insists is visible, and the moment a token is handed over is
    ///     the moment the user can still decide otherwise.
    /// </summary>
    [Fact]
    public void Add_SaysSoWhenTheOnlyPlaceForTheTokenIsAFileInTheClear()
    {
        _credentialStore = new PlaintextFileCredentialStore(new WoolyPaths(_directory.Path));

        var run = Add("personal", "mastodon.social", "token-personal");

        Assert.Equal((int)ExitCode.Success, run.ExitCode);
        Assert.Contains("credentials.toml", run.Output);
        Assert.Contains("keyring", run.Output);
    }

    /// <summary>A profile that cannot authenticate is worse than no profile, so a refused token leaves nothing behind.</summary>
    [Fact]
    public void Add_ReportsATokenTheInstanceRefusesAndWritesNoProfile()
    {
        var run = Run(
            ["profile", "add", "personal", "--instance", "mastodon.social", "--token", "token-wrong"],
            verifier: FakeAccessTokenVerifier.Refusing("The access token is invalid"));

        Assert.Equal((int)ExitCode.AuthenticationError, run.ExitCode);

        // The reason this test handed the fake, rather than either layer's phrasing of a refusal: what belongs here is
        // that a refusal reaches the user and stops the write, not how the verifier words one.
        Assert.Contains("The access token is invalid", run.ErrorOutput);
        Assert.Empty(run.Output.Trim());
        Assert.False(File.Exists(Path.Combine(_directory.Path, "config.toml")));
    }

    /// <summary>
    ///     Mastonet builds its own <c>https://</c> URLs from the domain, so a URL here would fail much later as a
    ///     puzzling network error rather than here, as the typo it is.
    /// </summary>
    [Fact]
    public void Add_RefusesAnInstanceGivenAsAUrl()
    {
        var run = Add("personal", "https://mastodon.social", "token-personal");

        Assert.Equal((int)ExitCode.UsageError, run.ExitCode);
        Assert.Contains("mastodon.social", run.ErrorOutput);
        Assert.Empty(run.Output.Trim());
    }

    [Fact]
    public void Switch_ChangesTheProfileLaterCommandsUseByDefault()
    {
        Add("personal", "mastodon.social", "token-personal");
        Add("work", "hachyderm.io", "token-work");

        var run = Run(["profile", "switch", "work"]);

        Assert.Equal((int)ExitCode.Success, run.ExitCode);
        Assert.Contains("hachyderm.io", Run(["profile", "show"]).Output);
    }

    [Fact]
    public void Switch_ReportsAProfileThatWasNeverSetUpAsAUsageError()
    {
        Add("personal", "mastodon.social", "token-personal");

        var run = Run(["profile", "switch", "wrok"]);

        Assert.Equal((int)ExitCode.UsageError, run.ExitCode);
        Assert.Contains("wrok", run.ErrorOutput);
        Assert.Contains("personal", run.ErrorOutput);
    }

    /// <summary>
    ///     The <c>--profile</c> override in full: this invocation acts as the named profile, and the next one is back
    ///     to the profile the user actually switched to.
    /// </summary>
    [Fact]
    public void Show_ActsAsTheProfileNamedByTheGlobalFlagWithoutChangingTheDefault()
    {
        Add("personal", "mastodon.social", "token-personal");
        Add("work", "hachyderm.io", "token-work");

        var overridden = Run(["profile", "show", "--profile", "work"]);

        Assert.Equal((int)ExitCode.Success, overridden.ExitCode);
        Assert.Contains("hachyderm.io", overridden.Output);
        Assert.DoesNotContain("mastodon.social", overridden.Output);

        Assert.Contains("mastodon.social", Run(["profile", "show"]).Output);
    }

    [Fact]
    public void Show_ReportsAProfileThatWasNeverSetUpAsAUsageError()
    {
        Add("personal", "mastodon.social", "token-personal");

        var run = Run(["profile", "show", "--profile", "wrok"]);

        Assert.Equal((int)ExitCode.UsageError, run.ExitCode);
        Assert.Contains("wrok", run.ErrorOutput);
    }

    /// <summary>Nothing prints an access token: not the profile it belongs to, not any other command.</summary>
    [Fact]
    public void Show_NeverPrintsTheAccessToken()
    {
        Add("personal", "mastodon.social", "token-personal");

        var run = Run(["profile", "show"]);

        Assert.DoesNotContain("token-personal", run.Output);
        Assert.Contains("keyring", run.Output);
    }

    [Fact]
    public void Show_ReportsThatNothingIsSetUpYetWithTheAuthenticationExitCode()
    {
        var run = Run(["profile", "show"]);

        Assert.Equal((int)ExitCode.AuthenticationError, run.ExitCode);
        Assert.Contains("No profiles", run.ErrorOutput);
    }

    [Fact]
    public void List_SaysSoOnAMachineWithNoProfilesRatherThanPrintingNothing()
    {
        var run = Run(["profile", "list"]);

        Assert.Equal((int)ExitCode.Success, run.ExitCode);
        Assert.Contains("No profiles", run.Output);
    }

    private CommandRun Add(string name, string instance, string accessToken) =>
        Run(["profile", "add", name, "--instance", instance, "--token", accessToken]);

    /// <param name="atATerminal">
    ///     Whether there is a person watching. True by default, because that is where this client is run from — set
    ///     false for the machine that has none, which is a different command surface rather than a quieter one.
    /// </param>
    private CommandRun Run(
        string[] args,
        string? typed = null,
        FakeAccessTokenVerifier? verifier = null,
        FakeBrowserAuthorizer? authorizer = null,
        bool atATerminal = true)
    {
        // Wide enough that no assertion is defeated by a wrapped line.
        var console = new TestConsole().Width(200);
        var errorConsole = new TestConsole().Width(200);

        if (atATerminal)
        {
            console.Interactive();
        }

        if (typed is not null)
        {
            console.Input.PushTextWithEnter(typed);
        }

        var app = WoolyCommandApp.Create(console, errorConsole, services =>
        {
            services.AddSingleton(new WoolyPaths(_directory.Path));
            services.AddSingleton(_credentialStore);
            services.AddSingleton<IAccessTokenVerifier>(verifier ?? _verifier);
            services.AddSingleton<IBrowserAuthorizer>(authorizer ?? _authorizer);
            services.AddSingleton<IWebBrowser>(_browser);
        });

        var exitCode = app.Run(args, TestContext.Current.CancellationToken);

        return new CommandRun(exitCode, console.Output, errorConsole.Output);
    }

    private sealed record CommandRun(int ExitCode, string Output, string ErrorOutput);
}
