using System.Text.Json;
using Mastonet;
using Mastonet.Entities;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console.Testing;
using Wooly.Cli;
using Wooly.Core;
using Wooly.Core.Credentials;
using Wooly.Core.Posts;
using Wooly.Core.Profiles;
using Wooly.Tests.Fakes;

namespace Wooly.Tests.Cli;

/// <summary>
///     Boosting, favoriting, pinning and showing a post, driven the way a user drives them: whole commands through the
///     real command app, over a real config file and token store in a scratch directory, with the instance faked at
///     <see cref="IPostEngagement" /> — ADR-0005's primary seam. Which endpoint each mark becomes is
///     <see cref="Core.PostEngagementTests" />'s business; what is proved here is what the command line asked for.
/// </summary>
public class PostEngagementCommandTests : IDisposable
{
    private readonly TemporaryDirectory _directory = new();

    private FakePostEngagement _posts = FakePostEngagement.Answering();

    public void Dispose() => _directory.Dispose();

    /// <summary>
    ///     The six verbs the ticket asks for, and the three marks behind them: <c>unboost</c> is <c>boost</c> undone
    ///     rather than a seventh thing to do.
    /// </summary>
    [Theory]
    [InlineData("boost", PostMark.Boost, true)]
    [InlineData("unboost", PostMark.Boost, false)]
    [InlineData("favorite", PostMark.Favorite, true)]
    [InlineData("unfavorite", PostMark.Favorite, false)]
    [InlineData("pin", PostMark.Pin, true)]
    [InlineData("unpin", PostMark.Pin, false)]
    public void Mark_PutsTheMarkTheVerbNamesOnThePost(string verb, PostMark mark, bool wanted)
    {
        AddProfile();

        var run = Run(["post", verb, "110"]);

        Assert.Equal((int)ExitCode.Success, run.ExitCode);
        Assert.Empty(run.ErrorOutput.Trim());

        var marked = Assert.Single(_posts.Marks);
        Assert.Equal("personal", marked.Profile);
        Assert.Equal("110", marked.PostId);
        Assert.Equal(mark, marked.Mark);
        Assert.Equal(wanted, marked.Wanted);
    }

    /// <summary>
    ///     Said in the words of what just happened, and naming the post the user named — never the boost that carries
    ///     it, which has an id nothing else knows that post by.
    /// </summary>
    [Theory]
    [InlineData("boost", "Boosted")]
    [InlineData("unboost", "Unboosted")]
    [InlineData("favorite", "Favorited")]
    [InlineData("unfavorite", "Unfavorited")]
    [InlineData("pin", "Pinned")]
    [InlineData("unpin", "Unpinned")]
    public void Mark_ReportsWhatItDidAndToWhichPost(string verb, string said)
    {
        AddProfile();

        var run = Run(["post", verb, "110"]);

        Assert.Contains(said, run.Output);
        Assert.Contains("110", run.Output);
        Assert.Contains("https://mastodon.social/@jeff/110", run.Output);
    }

    [Fact]
    public void Mark_ActsAsTheProfileNamedByTheOverrideWithoutChangingTheDefault()
    {
        AddProfile();
        AddProfile("work", "hachyderm.io");

        var run = Run(["post", "boost", "110", "--profile", "work"]);

        Assert.Equal((int)ExitCode.Success, run.ExitCode);
        Assert.Equal("work", Assert.Single(_posts.Marks).Profile);
    }

    [Fact]
    public void Mark_WritesTheMarkedPostAsMachineReadableJson()
    {
        AddProfile();

        var run = Run(["post", "favorite", "110", "--json"]);

        Assert.Equal((int)ExitCode.Success, run.ExitCode);

        var post = JsonDocument.Parse(run.Output).RootElement;

        Assert.Equal("110", post.GetProperty("id").GetString());
        Assert.Equal(5, post.GetProperty("favorites").GetInt64());
    }

    [Fact]
    public void Mark_ReportsAMissingPostIdAsAUsageError()
    {
        AddProfile();

        var run = Run(["post", "boost"]);

        Assert.Equal((int)ExitCode.UsageError, run.ExitCode);
        Assert.Empty(_posts.Marks);
    }

    [Fact]
    public void Mark_ReportsThatNothingIsSetUpYetWithTheAuthenticationExitCode()
    {
        var run = Run(["post", "boost", "110"]);

        Assert.Equal((int)ExitCode.AuthenticationError, run.ExitCode);
        Assert.Empty(_posts.Marks);
    }

    /// <summary>
    ///     Whose post it is, and whether it already carries the mark, are the instance's to answer — this client does
    ///     not ask first and does not second-guess the refusal, it reports what the instance said.
    /// </summary>
    [Fact]
    public void Mark_ReportsWhatTheInstanceRefusedInTheInstancesOwnWords()
    {
        AddProfile();
        _posts = FakePostEngagement.Refusing(
            new ServerErrorException(new Error { Description = "Validation failed: cannot be pinned" }));

        var run = Run(["post", "pin", "110"]);

        Assert.NotEqual((int)ExitCode.Success, run.ExitCode);
        Assert.Empty(run.Output.Trim());
        Assert.Contains("cannot be pinned", run.ErrorOutput);
    }

    [Fact]
    public void Show_ShowsThePostTheIdNames()
    {
        AddProfile();

        var run = Run(["post", "show", "110"]);

        Assert.Equal((int)ExitCode.Success, run.ExitCode);
        Assert.Empty(run.ErrorOutput.Trim());

        Assert.Contains("jeff@mastodon.social", run.Output);
        Assert.Contains("Hello world", run.Output);
        Assert.Contains("3 boosts, 5 favorites, 1 reply", run.Output);

        var read = Assert.Single(_posts.Reads);
        Assert.Equal("personal", read.Profile);
        Assert.Equal("110", read.PostId);
    }

    /// <summary>
    ///     The one thing a post asked for by id gets that a timeline's posts do not: where to read it on the web, which
    ///     is the part that cannot be worked out from anything else on screen.
    /// </summary>
    [Fact]
    public void Show_SaysWhereToReadThePostOnTheWeb()
    {
        AddProfile();

        var run = Run(["post", "show", "110"]);

        Assert.Contains("https://mastodon.social/@jeff/110", run.Output);
    }

    /// <summary>A post shown on its own reads the way the same post reads on a timeline, because it is written once.</summary>
    [Fact]
    public void Show_ShowsAContentWarningRatherThanPrintingPastIt()
    {
        AddProfile();
        _posts = FakePostEngagement.Answering(APost.With(contentWarning: "spoilers"));

        var run = Run(["post", "show", "110"]);

        Assert.Contains("content warning", run.Output);
        Assert.Contains("spoilers", run.Output);
    }

    [Fact]
    public void Show_ShowsABoostAsABoostOfThePostItCarries()
    {
        AddProfile();
        _posts = FakePostEngagement.Answering(APost.With(
            id: "555",
            account: "sam@hachyderm.io",
            boosted: APost.With(content: "The boosted post")));

        var run = Run(["post", "show", "555"]);

        Assert.Contains("sam@hachyderm.io boosted jeff@mastodon.social", run.Output);
        Assert.Contains("The boosted post", run.Output);
    }

    [Fact]
    public void Show_WritesThePostAsMachineReadableJson()
    {
        AddProfile();

        var run = Run(["post", "show", "110", "--json"]);

        Assert.Equal((int)ExitCode.Success, run.ExitCode);

        var post = JsonDocument.Parse(run.Output).RootElement;

        Assert.Equal("110", post.GetProperty("id").GetString());
        Assert.Equal("Hello world", post.GetProperty("content").GetString());
        Assert.Equal("public", post.GetProperty("visibility").GetString());
    }

    [Fact]
    public void Show_ReportsAMissingPostIdAsAUsageError()
    {
        AddProfile();

        var run = Run(["post", "show"]);

        Assert.Equal((int)ExitCode.UsageError, run.ExitCode);
        Assert.Empty(_posts.Reads);
    }

    /// <summary>CONTEXT.md's vocabulary, at the one place a user reads it: nothing on screen says reblog or favourite.</summary>
    [Fact]
    public void Post_NamesWhatItDoesInThisProjectsVocabulary()
    {
        AddProfile();

        var boosted = Run(["post", "boost", "110"]).Output;
        var shown = Run(["post", "show", "110"]).Output;

        foreach (var output in new[] { boosted, shown })
        {
            Assert.DoesNotContain("reblog", output);
            Assert.DoesNotContain("favourite", output);
            Assert.DoesNotContain("status", output);
            Assert.DoesNotContain("toot", output);
        }
    }

    private void AddProfile(string name = "personal", string instance = "mastodon.social") =>
        Run(["profile", "add", name, "--instance", instance, "--token", $"token-{name}"]);

    private CommandRun Run(string[] args)
    {
        var console = new TestConsole().Width(200);
        var errorConsole = new TestConsole().Width(200);

        var app = WoolyCommandApp.Create(console, errorConsole, services =>
        {
            services.AddSingleton(new WoolyPaths(_directory.Path));
            services.AddSingleton<ICredentialStore>(new PlaintextFileCredentialStore(new WoolyPaths(_directory.Path)));
            services.AddSingleton<IAccessTokenVerifier>(FakeAccessTokenVerifier.Accepting());
            services.AddSingleton<IPostEngagement>(_posts);
        });

        var exitCode = app.Run(args, TestContext.Current.CancellationToken);

        return new CommandRun(exitCode, console.Output, errorConsole.Output);
    }

    private sealed record CommandRun(int ExitCode, string Output, string ErrorOutput);
}
