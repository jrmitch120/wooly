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
    ///     A token on the command line is a secret in the shell's history file, so the command asks for it instead
    ///     when it is not given one.
    /// </summary>
    [Fact]
    public void Add_AsksForTheTokenWhenTheCommandLineDoesNotCarryIt()
    {
        var run = Run(["profile", "add", "personal", "--instance", "mastodon.social"], typed: "token-typed");

        Assert.Equal((int)ExitCode.Success, run.ExitCode);
        Assert.Equal(["token-typed"], _verifier.Tokens);
        Assert.DoesNotContain("token-typed", run.Output);
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

    private CommandRun Run(string[] args, string? typed = null, FakeAccessTokenVerifier? verifier = null)
    {
        // Wide enough that no assertion is defeated by a wrapped line.
        var console = new TestConsole().Width(200);
        var errorConsole = new TestConsole().Width(200);

        if (typed is not null)
        {
            console.Interactive();
            console.Input.PushTextWithEnter(typed);
        }

        var app = WoolyCommandApp.Create(console, errorConsole, services =>
        {
            services.AddSingleton(new WoolyPaths(_directory.Path));
            services.AddSingleton(_credentialStore);
            services.AddSingleton<IAccessTokenVerifier>(verifier ?? _verifier);
        });

        var exitCode = app.Run(args, TestContext.Current.CancellationToken);

        return new CommandRun(exitCode, console.Output, errorConsole.Output);
    }

    private sealed record CommandRun(int ExitCode, string Output, string ErrorOutput);
}
