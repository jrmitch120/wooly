using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console.Testing;
using Wooly.Cli;
using Wooly.Core;
using Wooly.Core.Credentials;
using Wooly.Core.Errors;
using Wooly.Core.Posts;
using Wooly.Core.Profiles;
using Wooly.Tests.Fakes;

namespace Wooly.Tests.Cli;

/// <summary>
///     Post authoring driven the way a user drives it: whole commands through the real command app, over a real config
///     file and token store in a scratch directory, with the instance faked at <see cref="IPostAuthor" /> — ADR-0005's
///     primary seam, which is what a command test is meant to fake. What each composing option turns into on the wire is
///     <see cref="Core.PostAuthorTests" />'s business; what is proved here is what the command line composed.
/// </summary>
public class PostCommandTests : IDisposable
{
    private readonly TemporaryDirectory _directory = new();

    private FakePostAuthor _posts = FakePostAuthor.Answering();

    public void Dispose() => _directory.Dispose();

    [Fact]
    public void Create_PublishesAPostSayingWhatTheCommandLineSaid()
    {
        AddProfile();

        var run = Run(["post", "create", "Hello world"]);

        Assert.Equal((int)ExitCode.Success, run.ExitCode);
        Assert.Empty(run.ErrorOutput.Trim());

        var composed = Assert.Single(_posts.Published);
        Assert.Equal("personal", composed.Profile);
        Assert.Equal("Hello world", composed.Draft.Text);
        Assert.Null(composed.Draft.InReplyTo);
    }

    /// <summary>The id above all: it is how every later command names the post that was just published.</summary>
    [Fact]
    public void Create_ReportsTheIdAndAddressOfWhatItPublished()
    {
        AddProfile();

        var run = Run(["post", "create", "Hello world"]);

        Assert.Contains("110", run.Output);
        Assert.Contains("https://mastodon.social/@jeff/110", run.Output);
    }

    [Fact]
    public void Create_AttachesAContentWarning()
    {
        AddProfile();

        var run = Run(["post", "create", "Hello world", "--cw", "spoilers"]);

        Assert.Equal((int)ExitCode.Success, run.ExitCode);
        Assert.Equal("spoilers", Assert.Single(_posts.Published).Draft.ContentWarning);
    }

    /// <summary>
    ///     A warning made of spaces hides a post behind nothing. Read the same way here as <c>post edit</c> reads it, so
    ///     the one flag cannot come to mean two things depending on which command it was passed to.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_ReadsAWarningWithNothingWrittenInItAsNoWarningAtAll(string blank)
    {
        AddProfile();

        var run = Run(["post", "create", "Hello world", "--cw", blank]);

        Assert.Equal((int)ExitCode.Success, run.ExitCode);
        Assert.Null(Assert.Single(_posts.Published).Draft.ContentWarning);
    }

    [Theory]
    [InlineData("public", PostVisibility.Public)]
    [InlineData("unlisted", PostVisibility.Unlisted)]
    [InlineData("private", PostVisibility.Private)]
    [InlineData("direct", PostVisibility.Direct)]
    [InlineData("PRIVATE", PostVisibility.Private)]
    public void Create_PublishesAtTheVisibilityAsked(string given, PostVisibility expected)
    {
        AddProfile();

        var run = Run(["post", "create", "Hello world", "--visibility", given]);

        Assert.Equal((int)ExitCode.Success, run.ExitCode);
        Assert.Equal(expected, Assert.Single(_posts.Published).Draft.Visibility);
    }

    [Fact]
    public void Create_ReportsAVisibilityThisClientDoesNotHaveAsAUsageError()
    {
        AddProfile();

        var run = Run(["post", "create", "Hello world", "--visibility", "followers"]);

        Assert.Equal((int)ExitCode.UsageError, run.ExitCode);
        Assert.Empty(_posts.Published);
    }

    /// <summary>
    ///     Said nowhere, the choice stays the account's own. Filling in "public" here would publish an account whose own
    ///     default is followers-only wider than it asked for.
    /// </summary>
    [Fact]
    public void Create_LeavesVisibilityUnsaidWhenNeitherTheCommandLineNorTheConfigFileSaysIt()
    {
        AddProfile();

        var run = Run(["post", "create", "Hello world"]);

        Assert.Equal((int)ExitCode.Success, run.ExitCode);
        Assert.Null(Assert.Single(_posts.Published).Draft.Visibility);
    }

    /// <summary>
    ///     The preference an author sets once rather than typing every time. Its whole reason for existing is that a
    ///     careful poster should not have to remember <c>--visibility</c> on every post.
    /// </summary>
    [Fact]
    public void Create_FallsBackToTheVisibilityTheConfigFilePrefers()
    {
        AddProfile();
        PreferVisibility("private");

        var run = Run(["post", "create", "Hello world"]);

        Assert.Equal((int)ExitCode.Success, run.ExitCode);
        Assert.Equal(PostVisibility.Private, Assert.Single(_posts.Published).Draft.Visibility);
    }

    [Fact]
    public void Create_LetsTheCommandLineOverrideTheVisibilityTheConfigFilePrefers()
    {
        AddProfile();
        PreferVisibility("private");

        var run = Run(["post", "create", "Hello world", "--visibility", "public"]);

        Assert.Equal(PostVisibility.Public, Assert.Single(_posts.Published).Draft.Visibility);
    }

    /// <summary>
    ///     The ticket's point about media: one flag per file, alt text included, and no separate upload step for the user
    ///     to run first.
    /// </summary>
    [Fact]
    public void Create_AttachesEveryFileGivenWithTheAltTextThatFollowsIt()
    {
        AddProfile();

        var cat = _directory.WriteFile("cat.png");
        var dog = _directory.WriteFile("dog.png");

        var run = Run(["post", "create", "Two pets", "--media", $"{cat}:a ginger cat", "--media", dog]);

        Assert.Equal((int)ExitCode.Success, run.ExitCode);

        var media = Assert.Single(_posts.Published).Draft.Media;
        Assert.Equal([cat, dog], media.Select(attachment => attachment.Path));
        Assert.Equal(["a ginger cat", null], media.Select(attachment => attachment.AltText));
    }

    /// <summary>A picture can be the whole of what somebody wanted to say.</summary>
    [Fact]
    public void Create_PublishesAPostThatIsNothingButAFile()
    {
        AddProfile();

        var run = Run(["post", "create", string.Empty, "--media", _directory.WriteFile("cat.png")]);

        Assert.Equal((int)ExitCode.Success, run.ExitCode);
        Assert.Equal(string.Empty, Assert.Single(_posts.Published).Draft.Text);
    }

    [Fact]
    public void Create_ReportsAPostWithNothingToSayAsAUsageError()
    {
        AddProfile();

        var run = Run(["post", "create", "   "]);

        Assert.Equal((int)ExitCode.UsageError, run.ExitCode);
        Assert.NotEmpty(run.ErrorOutput.Trim());
        Assert.Empty(_posts.Published);
    }

    /// <summary>An instance stores one or the other, and by the time it refuses, the files have been uploaded.</summary>
    [Fact]
    public void Create_ReportsAPostCarryingBothFilesAndAPollAsAUsageError()
    {
        AddProfile();

        var run = Run([
            "post", "create", "Cats or dogs?",
            "--media", _directory.WriteFile("cat.png"),
            "--poll", "Cats", "--poll", "Dogs",
        ]);

        Assert.Equal((int)ExitCode.UsageError, run.ExitCode);
        Assert.Empty(_posts.Published);
    }

    [Fact]
    public void Create_AttachesAPollWithTheAnswersGiven()
    {
        AddProfile();

        var run = Run(["post", "create", "Cats or dogs?", "--poll", "Cats", "--poll", "Dogs"]);

        Assert.Equal((int)ExitCode.Success, run.ExitCode);

        var poll = Assert.Single(_posts.Published).Draft.Poll;
        Assert.Equal(["Cats", "Dogs"], poll?.Answers);
        Assert.False(poll?.MultipleChoice);
    }

    [Fact]
    public void Create_AttachesAPollThatStaysOpenAsLongAsAsked()
    {
        AddProfile();

        var run = Run([
            "post", "create", "Cats or dogs?",
            "--poll", "Cats", "--poll", "Dogs",
            "--poll-open", "6h", "--poll-multiple",
        ]);

        Assert.Equal((int)ExitCode.Success, run.ExitCode);

        var poll = Assert.Single(_posts.Published).Draft.Poll;
        Assert.Equal(TimeSpan.FromHours(6), poll?.OpenFor);
        Assert.True(poll?.MultipleChoice);
    }

    /// <summary>A poll has to expire, so an unasked-for length has to be some length rather than none.</summary>
    [Fact]
    public void Create_KeepsAPollOpenForADayWhenNobodySaysHowLong()
    {
        AddProfile();

        var run = Run(["post", "create", "Cats or dogs?", "--poll", "Cats", "--poll", "Dogs"]);

        Assert.Equal(TimeSpan.FromHours(24), Assert.Single(_posts.Published).Draft.Poll?.OpenFor);
    }

    [Fact]
    public void Create_ReportsAPollWithOneAnswerAsAUsageError()
    {
        AddProfile();

        var run = Run(["post", "create", "Cats?", "--poll", "Cats"]);

        Assert.Equal((int)ExitCode.UsageError, run.ExitCode);
        Assert.Empty(_posts.Published);
    }

    /// <summary>
    ///     Options that describe a poll, passed without one. Publishing a post with no poll would answer the user's
    ///     mistake with silence, which is what ADR-0006 turned strict parsing on to stop.
    /// </summary>
    [Theory]
    [InlineData("--poll-open", "6h")]
    [InlineData("--poll-multiple", null)]
    public void Create_ReportsPollOptionsWithNoPollAsAUsageError(string option, string? value)
    {
        AddProfile();

        string[] args = value is null
            ? ["post", "create", "Hello world", option]
            : ["post", "create", "Hello world", option, value];

        var run = Run(args);

        Assert.Equal((int)ExitCode.UsageError, run.ExitCode);
        Assert.Empty(_posts.Published);
    }

    [Fact]
    public void Create_ReportsALengthOfTimeItCannotReadAsAUsageError()
    {
        AddProfile();

        var run = Run(["post", "create", "Cats?", "--poll", "Cats", "--poll", "Dogs", "--poll-open", "soon"]);

        Assert.Equal((int)ExitCode.UsageError, run.ExitCode);
        Assert.Empty(_posts.Published);
    }

    /// <summary>
    ///     A file that is not where the flag said. Reported as the wrong value it is, and nothing is published — the post
    ///     an author meant to attach it to is not one they want without it.
    /// </summary>
    [Fact]
    public void Create_ReportsAFileThatIsNotThereAsAUsageError()
    {
        AddProfile();
        _posts = FakePostAuthor.Refusing(new MediaNotFoundException("/tmp/not-here.png"));

        var run = Run(["post", "create", "A cat", "--media", "/tmp/not-here.png"]);

        Assert.Equal((int)ExitCode.UsageError, run.ExitCode);
        Assert.Contains("not-here.png", run.ErrorOutput);
        Assert.Empty(run.Output.Trim());
    }

    [Fact]
    public void Create_ReportsThatNothingIsSetUpYetWithTheAuthenticationExitCode()
    {
        var run = Run(["post", "create", "Hello world"]);

        Assert.Equal((int)ExitCode.AuthenticationError, run.ExitCode);
        Assert.Empty(_posts.Published);
    }

    [Fact]
    public void Create_PostsAsTheProfileNamedByTheOverrideWithoutChangingTheDefault()
    {
        AddProfile();
        AddProfile("work", "hachyderm.io");

        var run = Run(["post", "create", "Hello work", "--profile", "work"]);

        Assert.Equal((int)ExitCode.Success, run.ExitCode);
        Assert.Equal("work", Assert.Single(_posts.Published).Profile);
    }

    [Fact]
    public void Create_WritesThePublishedPostAsMachineReadableJson()
    {
        AddProfile();

        var run = Run(["post", "create", "Hello world", "--json"]);

        Assert.Equal((int)ExitCode.Success, run.ExitCode);

        var post = JsonDocument.Parse(run.Output).RootElement;

        Assert.Equal("110", post.GetProperty("id").GetString());
        Assert.Equal("jeff@mastodon.social", post.GetProperty("account").GetString());
        Assert.Equal("Hello world", post.GetProperty("content").GetString());
        Assert.Equal("public", post.GetProperty("visibility").GetString());
    }

    /// <summary>The ticket's point about replying: the same options as creating, because it is the same command.</summary>
    [Fact]
    public void Reply_NamesThePostItAnswersAndComposesEverythingElseTheSameWay()
    {
        AddProfile();

        var run = Run([
            "post", "reply", "99", "Quite so",
            "--cw", "spoilers", "--visibility", "private",
            "--media", _directory.WriteFile("cat.png"),
        ]);

        Assert.Equal((int)ExitCode.Success, run.ExitCode);

        var draft = Assert.Single(_posts.Published).Draft;
        Assert.Equal("99", draft.InReplyTo);
        Assert.Equal("Quite so", draft.Text);
        Assert.Equal("spoilers", draft.ContentWarning);
        Assert.Equal(PostVisibility.Private, draft.Visibility);
        Assert.Single(draft.Media);
    }

    /// <summary>
    ///     A visibility typed on the command line is carried as chosen, which is what lets a reply refuse to go out
    ///     wider than the post it answers rather than quietly narrowing what somebody asked for (ADR-0013). Whether it
    ///     is too wide is the instance's answer to give, so the port is what decides — this proves the command tells it
    ///     which kind of answer it has.
    /// </summary>
    [Fact]
    public void Reply_CarriesAVisibilityTypedOnTheCommandLineAsOneThatWasChosen()
    {
        AddProfile();

        Run(["post", "reply", "99", "Quite so", "--visibility", "private"]);

        var draft = Assert.Single(_posts.Published).Draft;
        Assert.Equal(PostVisibility.Private, draft.Visibility);
        Assert.True(draft.VisibilityChosen);
    }

    /// <summary>
    ///     The config file's preference is not a choice made about this reply, so it is narrowed to fit rather than
    ///     refused. Carried as chosen, a profile that prefers public could never answer a direct message.
    /// </summary>
    [Fact]
    public void Reply_CarriesTheConfigFilesPreferenceAsOneThatWasNotChosen()
    {
        AddProfile();
        PreferVisibility("public");

        Run(["post", "reply", "99", "Quite so"]);

        var draft = Assert.Single(_posts.Published).Draft;
        Assert.Equal(PostVisibility.Public, draft.Visibility);
        Assert.False(draft.VisibilityChosen);
    }

    [Fact]
    public void Reply_ReportsAMissingPostIdAsAUsageError()
    {
        AddProfile();

        var run = Run(["post", "reply", "Quite so"]);

        Assert.Equal((int)ExitCode.UsageError, run.ExitCode);
        Assert.Empty(_posts.Published);
    }

    [Fact]
    public void Edit_ChangesWhatThePostSays()
    {
        AddProfile();

        var run = Run(["post", "edit", "110", "Fixed the typo"]);

        Assert.Equal((int)ExitCode.Success, run.ExitCode);
        Assert.Contains("110", run.Output);

        var edit = Assert.Single(_posts.Edits);
        Assert.Equal("110", edit.PostId);
        Assert.Equal("Fixed the typo", edit.Edit.Text);
    }

    /// <summary>
    ///     Silence about the warning has to reach the domain as silence. Normalising it away here is what would make
    ///     fixing a typo a way to show a reader what they had asked not to be shown.
    /// </summary>
    [Fact]
    public void Edit_SaysNothingAboutTheContentWarningWhenTheCommandLineDidNot()
    {
        AddProfile();

        Run(["post", "edit", "110", "Fixed the typo"]);

        var edit = Assert.Single(_posts.Edits).Edit;
        Assert.Null(edit.ContentWarning);
        Assert.False(edit.ChangesContentWarning);
    }

    [Fact]
    public void Edit_ReplacesTheContentWarningWhenGivenANewOne()
    {
        AddProfile();

        Run(["post", "edit", "110", "Fixed the typo", "--cw", "later spoilers"]);

        Assert.Equal("later spoilers", Assert.Single(_posts.Edits).Edit.ContentWarning);
    }

    /// <summary>An empty warning is how an author says the post no longer needs one.</summary>
    [Fact]
    public void Edit_TakesTheContentWarningAwayWhenGivenAnEmptyOne()
    {
        AddProfile();

        Run(["post", "edit", "110", "Fixed the typo", "--cw", string.Empty]);

        var edit = Assert.Single(_posts.Edits).Edit;
        Assert.Equal(string.Empty, edit.ContentWarning);
        Assert.True(edit.ChangesContentWarning);
    }

    /// <summary>
    ///     The refusal this client makes rather than lose part of a post, reported as what it is: a post that cannot be
    ///     edited, with an exit code a script can tell from a client that broke.
    /// </summary>
    [Fact]
    public void Edit_ReportsAPostItWillNotEditWithoutDestroyingAsAUsageError()
    {
        AddProfile();
        _posts = FakePostAuthor.Refusing(new UneditablePostException("110"));

        var run = Run(["post", "edit", "110", "Fixed the typo"]);

        Assert.Equal((int)ExitCode.UsageError, run.ExitCode);
        Assert.Contains("poll", run.ErrorOutput);
        Assert.Empty(run.Output.Trim());
    }

    [Fact]
    public void Edit_WritesTheEditedPostAsMachineReadableJson()
    {
        AddProfile();

        var run = Run(["post", "edit", "110", "Fixed the typo", "--json"]);

        Assert.Equal("110", JsonDocument.Parse(run.Output).RootElement.GetProperty("id").GetString());
    }

    /// <summary>
    ///     A script has nobody to answer a prompt, and stopping to ask one would make this command unusable in the
    ///     automation the CLI exists for. Typing the id is that invocation's consent.
    /// </summary>
    [Fact]
    public void Delete_TakesThePostDownWithoutAskingWhereThereIsNoTerminal()
    {
        AddProfile();

        var run = Run(["post", "delete", "110"], atATerminal: false);

        Assert.Equal((int)ExitCode.Success, run.ExitCode);
        Assert.Equal("110", Assert.Single(_posts.Deletions).PostId);
        Assert.Contains("Deleted post", run.Output);
    }

    /// <summary>A mistyped id here is a post nobody can get back, so a person at a terminal is asked first.</summary>
    [Fact]
    public void Delete_AsksBeforeTakingAPostDownWhenThereIsSomebodyToAsk()
    {
        AddProfile();

        var run = Run(["post", "delete", "110"], atATerminal: true, typed: "y");

        Assert.Equal((int)ExitCode.Success, run.ExitCode);
        Assert.Equal("110", Assert.Single(_posts.Deletions).PostId);
    }

    [Fact]
    public void Delete_LeavesThePostAloneWhenTheAnswerIsNo()
    {
        AddProfile();

        var run = Run(["post", "delete", "110"], atATerminal: true, typed: "n");

        Assert.Equal((int)ExitCode.Success, run.ExitCode);
        Assert.Empty(_posts.Deletions);
        Assert.Contains("Left post", run.Output);
    }

    [Fact]
    public void Delete_DoesNotAskWhenTheCommandLineAlreadySaidYes()
    {
        AddProfile();

        var run = Run(["post", "delete", "110", "--yes"], atATerminal: true);

        Assert.Equal((int)ExitCode.Success, run.ExitCode);
        Assert.Equal("110", Assert.Single(_posts.Deletions).PostId);
    }

    [Fact]
    public void Delete_ReportsAMissingPostIdAsAUsageError()
    {
        AddProfile();

        var run = Run(["post", "delete"], atATerminal: false);

        Assert.Equal((int)ExitCode.UsageError, run.ExitCode);
        Assert.Empty(_posts.Deletions);
    }

    /// <summary>CONTEXT.md's vocabulary, at the one place a user reads it: nothing on screen says status or toot.</summary>
    [Fact]
    public void Post_NamesWhatItDoesInThisProjectsVocabulary()
    {
        AddProfile();

        var run = Run(["post", "create", "Hello world"]);

        Assert.Contains("Posted", run.Output);
        Assert.DoesNotContain("status", run.Output);
        Assert.DoesNotContain("toot", run.Output);
    }

    private void AddProfile(string name = "personal", string instance = "mastodon.social") =>
        Run(["profile", "add", name, "--instance", instance, "--token", $"token-{name}"], atATerminal: false);

    /// <summary>
    ///     Writes the preference an author would have set by hand, since nothing yet sets it from the command line.
    /// </summary>
    private void PreferVisibility(string visibility)
    {
        var paths = new WoolyPaths(_directory.Path);

        File.AppendAllText(paths.ConfigFile, $"{Environment.NewLine}[preferences]{Environment.NewLine}default_visibility = \"{visibility}\"{Environment.NewLine}");
    }

    private CommandRun Run(string[] args, bool atATerminal = false, string? typed = null)
    {
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
            services.AddSingleton<ICredentialStore>(new PlaintextFileCredentialStore(new WoolyPaths(_directory.Path)));
            services.AddSingleton<IAccessTokenVerifier>(FakeAccessTokenVerifier.Accepting());
            services.AddSingleton<IPostAuthor>(_posts);
        });

        var exitCode = app.Run(args, TestContext.Current.CancellationToken);

        return new CommandRun(exitCode, console.Output, errorConsole.Output);
    }

    private sealed record CommandRun(int ExitCode, string Output, string ErrorOutput);
}
