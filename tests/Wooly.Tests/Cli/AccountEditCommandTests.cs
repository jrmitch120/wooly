using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console.Testing;
using Wooly.Cli;
using Wooly.Cli.Infrastructure;
using Wooly.Core;
using Wooly.Core.Accounts;
using Wooly.Core.Credentials;
using Wooly.Core.Errors;
using Wooly.Core.Profiles;
using Wooly.Tests.Fakes;

namespace Wooly.Tests.Cli;

/// <summary>
///     Editing the own account's public profile the way a user does it (#404): whole commands through the real command
///     app, with the instance faked at <see cref="IPublicProfileEditor" />.
/// </summary>
public class AccountEditCommandTests : IDisposable
{
    private readonly TemporaryDirectory _directory = new();

    private FakePublicProfileEditor _editor = FakePublicProfileEditor.Holding(
        APublicProfile.With(
            displayName: "Jeff",
            bio: "Writes Wooly.",
            fields: [new("Site", "https://wooly.example")],
            isLocked: false,
            isBot: false,
            isDiscoverable: true));

    private string _standardInput = "";

    public void Dispose() => _directory.Dispose();

    [Fact]
    public void Edit_SendsTheDisplayNameAndBio()
    {
        AddProfile();

        var run = Run(["account", "edit", "--display-name", "Jeff M", "--bio", "Writes Wooly, mostly."]);

        Assert.Equal((int)ExitCode.Success, run.ExitCode);
        Assert.Equal("personal", Assert.Single(_editor.Reads));
        Assert.Equal(
            new PublicProfileChange { DisplayName = "Jeff M", Bio = "Writes Wooly, mostly." },
            Assert.Single(_editor.Saves));
    }

    /// <summary>A bio of several lines is easier piped than quoted, and the newline a pipe ends on is not part of it.</summary>
    [Fact]
    public void Edit_ReadsTheBioFromStandardInputForADash()
    {
        AddProfile();
        _standardInput = "Line one\nLine two\n";

        var run = Run(["account", "edit", "--bio=-"]);

        Assert.Equal((int)ExitCode.Success, run.ExitCode);
        Assert.Equal("Line one\nLine two", Assert.Single(_editor.Saves).Bio);
    }

    /// <summary>
    ///     Nothing piped in is a pipe never connected far more often than a wish to remove the bio, and a bio removed by
    ///     mistake cannot be got back — so it is refused, and nothing is read or sent.
    /// </summary>
    [Fact]
    public void Edit_RefusesABioFromStandardInputWhereNothingWasPiped()
    {
        AddProfile();

        var run = Run(["account", "edit", "--bio=-"]);

        Assert.Equal((int)ExitCode.UsageError, run.ExitCode);
        Assert.Contains("--bio \"\"", run.ErrorOutput);
        Assert.Empty(_editor.Reads);
        Assert.Empty(_editor.Saves);
    }

    /// <summary>Removing the bio is said outright.</summary>
    [Fact]
    public void Edit_RemovesTheBioForAnEmptyOne()
    {
        AddProfile();

        Run(["account", "edit", "--bio", ""]);

        Assert.Equal(new PublicProfileChange { Bio = "" }, Assert.Single(_editor.Saves));
    }

    [Theory]
    [InlineData("--locked", "true")]
    [InlineData("--bot", "true")]
    [InlineData("--discoverable", "false")]
    public void Edit_SendsEachFlag(string option, string value)
    {
        AddProfile();

        Assert.Equal((int)ExitCode.Success, Run(["account", "edit", option, value]).ExitCode);

        var wanted = bool.Parse(value);
        Assert.Equal(
            option switch
            {
                "--locked" => new PublicProfileChange { IsLocked = wanted },
                "--bot" => new PublicProfileChange { IsBot = wanted },
                _ => new PublicProfileChange { IsDiscoverable = wanted },
            },
            Assert.Single(_editor.Saves));
    }

    /// <summary>A flag is turned off as plainly as on, so a bot that stops being one can say so.</summary>
    [Fact]
    public void Edit_TurnsAFlagOff()
    {
        AddProfile();
        _editor = FakePublicProfileEditor.Holding(APublicProfile.With(isBot: true));

        Run(["account", "edit", "--bot", "false"]);

        Assert.Equal(new PublicProfileChange { IsBot = false }, Assert.Single(_editor.Saves));
    }

    [Fact]
    public void Edit_ReportsAFlagThatIsNeitherTrueNorFalseAsAUsageError()
    {
        AddProfile();

        var run = Run(["account", "edit", "--locked", "maybe"]);

        Assert.Equal((int)ExitCode.UsageError, run.ExitCode);
        Assert.Empty(_editor.Saves);
    }

    /// <summary>Any <c>--field</c> replaces all four, as Mastodon does.</summary>
    [Fact]
    public void Edit_ReplacesEveryFieldWithTheOnesNamed()
    {
        AddProfile();

        Run(["account", "edit", "--field", "Site=https://wooly.example", "--field", "Pronouns=they/them"]);

        Assert.Equal(
            [new PublicProfileField("Site", "https://wooly.example"), new PublicProfileField("Pronouns", "they/them")],
            Assert.Single(_editor.Saves).Fields);
    }

    /// <summary>Only the first <c>=</c> divides a field, since a value may well be an address carrying its own.</summary>
    [Fact]
    public void Edit_DividesAFieldAtItsFirstEquals()
    {
        AddProfile();

        Run(["account", "edit", "--field", "Search=https://example.com/?q=wooly"]);

        Assert.Equal(
            [new PublicProfileField("Search", "https://example.com/?q=wooly")],
            Assert.Single(_editor.Saves).Fields);
    }

    [Fact]
    public void Edit_ClearsEveryField()
    {
        AddProfile();

        Run(["account", "edit", "--clear-fields"]);

        Assert.Equal([], Assert.Single(_editor.Saves).Fields!);
    }

    /// <summary>Each case is its options with a <c>|</c> between them.</summary>
    [Theory]
    [InlineData("--field|no equals sign")]
    [InlineData("--field|=no name")]
    [InlineData("--field|A=1|--field|B=2|--field|C=3|--field|D=4|--field|E=5")]
    [InlineData("--field|A=1|--clear-fields")]
    public void Edit_ReportsFieldsItCannotSendAsAUsageError(string options)
    {
        AddProfile();

        var run = Run(["account", "edit", .. options.Split('|')]);

        Assert.Equal((int)ExitCode.UsageError, run.ExitCode);
        Assert.Empty(_editor.Reads);
        Assert.Empty(_editor.Saves);
    }

    /// <summary>Only what differs from what was read is sent, so a script can say everything and change one thing.</summary>
    [Fact]
    public void Edit_SendsOnlyWhatDiffersFromWhatWasRead()
    {
        AddProfile();

        Run(["account", "edit", "--display-name", "Jeff", "--bio", "Writes Wooly, mostly.", "--locked", "false",
            "--field", "Site=https://wooly.example"]);

        Assert.Equal(new PublicProfileChange { Bio = "Writes Wooly, mostly." }, Assert.Single(_editor.Saves));
    }

    /// <summary>Asking for what is already so changes nothing, and still says what the account is.</summary>
    [Fact]
    public void Edit_SendsNothingWhereNothingDiffers()
    {
        AddProfile();

        var run = Run(["account", "edit", "--display-name", "Jeff"]);

        Assert.Equal((int)ExitCode.Success, run.ExitCode);
        Assert.Empty(_editor.Saves);
        Assert.Contains("jeff@mastodon.social", run.Output);
    }

    /// <summary>A bare command never sends an empty save.</summary>
    [Fact]
    public void Edit_ReportsNoFlagsAsAUsageErrorAndSendsNothing()
    {
        AddProfile();

        var run = Run(["account", "edit"]);

        Assert.Equal((int)ExitCode.UsageError, run.ExitCode);
        Assert.Contains("--display-name", run.ErrorOutput);
        Assert.Empty(_editor.Reads);
        Assert.Empty(_editor.Saves);
    }

    [Fact]
    public void Edit_PrintsTheAccountAsAccountShowDoes()
    {
        AddProfile();
        _editor = FakePublicProfileEditor.Holding(
            saved: AnAccount.With(address: "jeff@mastodon.social", author: "Jeff M", bio: "Writes Wooly, mostly."));

        var run = Run(["account", "edit", "--display-name", "Jeff M"]);

        Assert.Contains("jeff@mastodon.social  Jeff M", run.Output);
        Assert.Contains("Writes Wooly, mostly.", run.Output);
        Assert.Empty(run.ErrorOutput.Trim());
    }

    [Fact]
    public void Edit_WritesTheAccountAsJsonTheWayAccountShowDoes()
    {
        AddProfile();
        _editor = FakePublicProfileEditor.Holding(
            saved: AnAccount.With(address: "jeff@mastodon.social", author: "Jeff M", isBot: true));

        var run = Run(["account", "edit", "--bot", "true", "--json"]);

        using var document = JsonDocument.Parse(run.Output);
        Assert.Equal("jeff@mastodon.social", document.RootElement.GetProperty("account").GetString());
        Assert.Equal("Jeff M", document.RootElement.GetProperty("author").GetString());
        Assert.True(document.RootElement.GetProperty("bot").GetBoolean());
        Assert.False(document.RootElement.TryGetProperty("standing", out _));
    }

    /// <summary>A refusal is the instance's to word, on stderr, and a general error rather than the user's typing.</summary>
    [Fact]
    public void Edit_ReportsARefusalInTheInstancesOwnWords()
    {
        AddProfile();
        _editor = FakePublicProfileEditor.Refusing(
            PublicProfileRefusedException.Saving("Validation failed: Display name is too long (maximum is 30 characters)"));

        var run = Run(["account", "edit", "--display-name", new string('x', 31)]);

        Assert.Equal((int)ExitCode.Error, run.ExitCode);
        Assert.Contains("Display name is too long (maximum is 30 characters)", run.ErrorOutput);
        Assert.Empty(run.Output.Trim());
    }

    [Fact]
    public void Edit_ActsAsTheProfileNamed()
    {
        AddProfile();
        AddProfile("work", "hachyderm.io");

        Run(["account", "edit", "--bot", "true", "--profile", "work"]);

        Assert.Equal("work", Assert.Single(_editor.Reads));
    }

    private void AddProfile(string name = "personal", string instance = "mastodon.social") =>
        Assert.Equal(0, Run(["profile", "add", name, "--instance", instance, "--token", $"token-{name}"]).ExitCode);

    private CommandRun Run(string[] args)
    {
        var console = new TestConsole().Width(200);
        var errorConsole = new TestConsole().Width(200);

        var app = WoolyCommandApp.Create(console, errorConsole, services =>
        {
            services.AddSingleton(new WoolyPaths(_directory.Path));
            services.AddSingleton<ICredentialStore>(new PlaintextFileCredentialStore(new WoolyPaths(_directory.Path)));
            services.AddSingleton<IAccessTokenVerifier>(FakeAccessTokenVerifier.Accepting());
            services.AddSingleton<IPublicProfileEditor>(_editor);
            services.AddSingleton(new StandardInput(new StringReader(_standardInput)));
        });

        var exitCode = app.Run(args, TestContext.Current.CancellationToken);

        return new CommandRun(exitCode, console.Output, errorConsole.Output);
    }

    private sealed record CommandRun(int ExitCode, string Output, string ErrorOutput);
}
