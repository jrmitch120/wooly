using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console.Testing;
using Wooly.Cli;
using Wooly.Core;
using Wooly.Core.Credentials;
using Wooly.Core.Profiles;
using Wooly.Core.Timelines;
using Wooly.Tests.Fakes;

namespace Wooly.Tests.Cli;

/// <summary>
///     Timeline reading driven the way a user drives it: whole commands through the real command app, over a real
///     config file and token store in a scratch directory, with the instance's timelines faked at
///     <see cref="ITimelineReader" /> — ADR-0005's primary seam, which is what a command test is meant to fake.
/// </summary>
public class TimelineCommandTests : IDisposable
{
    private readonly TemporaryDirectory _directory = new();

    private FakeTimelineReader _timelines = FakeTimelineReader.Holding(FakeTimelineReader.APost());

    public void Dispose() => _directory.Dispose();

    [Fact]
    public void Home_ShowsThePostsOnTheHomeTimeline()
    {
        AddProfile();

        var run = Run(["timeline", "home"]);

        Assert.Equal((int)ExitCode.Success, run.ExitCode);
        Assert.Contains("jeff@mastodon.social", run.Output);
        Assert.Contains("Hello world", run.Output);
        Assert.Empty(run.ErrorOutput.Trim());

        var read = Assert.Single(_timelines.Reads);
        Assert.Equal(Timeline.Home, read.Timeline);
        Assert.Equal("personal", read.Profile);
    }

    [Theory]
    [InlineData("local", TimelineScope.Local)]
    [InlineData("federated", TimelineScope.Federated)]
    public void Timeline_ReadsTheTimelineTheSubcommandNames(string command, TimelineScope expected)
    {
        AddProfile();

        var run = Run(["timeline", command]);

        Assert.Equal((int)ExitCode.Success, run.ExitCode);
        Assert.Equal(expected, Assert.Single(_timelines.Reads).Timeline.Scope);
    }

    /// <summary>A user says "#cats" as readily as "cats", and a shell that ate the # leaves a third spelling.</summary>
    [Theory]
    [InlineData("cats")]
    [InlineData("#cats")]
    public void Tag_ReadsTheHashtagTimelineHoweverTheHashtagWasSpelled(string hashtag)
    {
        AddProfile();

        var run = Run(["timeline", "tag", hashtag]);

        Assert.Equal((int)ExitCode.Success, run.ExitCode);
        Assert.Equal(Timeline.Tag("cats"), Assert.Single(_timelines.Reads).Timeline);
    }

    [Fact]
    public void Tag_ReportsAMissingHashtagAsAUsageError()
    {
        AddProfile();

        var run = Run(["timeline", "tag"]);

        Assert.Equal((int)ExitCode.UsageError, run.ExitCode);
        Assert.NotEmpty(run.ErrorOutput.Trim());
        Assert.Empty(_timelines.Reads);
    }

    /// <summary>
    ///     A tag goes into the request path, so a value with slashes in it would fetch a different endpoint altogether
    ///     and have the answer rendered back as posts. It is turned down where the user can see it, as the usage error
    ///     it is, rather than reaching an instance.
    /// </summary>
    [Theory]
    [InlineData("../../v1/accounts")]
    [InlineData("two words")]
    [InlineData("#")]
    public void Tag_ReportsAHashtagThatIsNotOneWordAsAUsageError(string hashtag)
    {
        AddProfile();

        var run = Run(["timeline", "tag", hashtag]);

        Assert.Equal((int)ExitCode.UsageError, run.ExitCode);
        Assert.NotEmpty(run.ErrorOutput.Trim());
        Assert.Empty(_timelines.Reads);
    }

    /// <summary>CONTEXT.md's vocabulary, at the one place a user reads it: nothing on screen says reblog or toot.</summary>
    [Fact]
    public void Home_NamesWhatItShowsInThisProjectsVocabulary()
    {
        AddProfile();

        var run = Run(["timeline", "home"]);

        Assert.Contains("boost", run.Output);
        Assert.Contains("favorite", run.Output);
        Assert.DoesNotContain("reblog", run.Output);
        Assert.DoesNotContain("favourite", run.Output);
        Assert.DoesNotContain("toot", run.Output);
        Assert.DoesNotContain("status", run.Output);
    }

    /// <summary>A boost carries no text of its own, so what gets shown is the post it points at.</summary>
    [Fact]
    public void Home_ShowsABoostAsWhoBoostedItAndThePostTheyBoosted()
    {
        AddProfile();
        _timelines = FakeTimelineReader.Holding(FakeTimelineReader.APost(
            content: string.Empty,
            boosted: FakeTimelineReader.APost(id: "99", account: "alice@hachyderm.io", content: "The original")));

        var run = Run(["timeline", "home"]);

        Assert.Equal((int)ExitCode.Success, run.ExitCode);
        Assert.Contains("boosted", run.Output);
        Assert.Contains("alice@hachyderm.io", run.Output);
        Assert.Contains("The original", run.Output);
    }

    /// <summary>
    ///     A post's own paragraphs are kept, and the blank line between them is blank — indenting it to match the
    ///     text would put trailing whitespace on every paragraph break in the timeline.
    /// </summary>
    [Fact]
    public void Home_KeepsAPostsOwnLinesWithoutIndentingTheBlankOnes()
    {
        AddProfile();
        _timelines = FakeTimelineReader.Holding(FakeTimelineReader.APost(content: "First\n\nSecond"));

        var run = Run(["timeline", "home"]);

        Assert.Contains("  First\n\n  Second\n", run.Output.ReplaceLineEndings("\n"));
    }

    /// <summary>A post can be nothing but media, and blank space is not what "no text" should look like.</summary>
    [Fact]
    public void Home_ShowsAPostWithNoTextAtAllWithoutAnEmptyLineStandingInForIt()
    {
        AddProfile();
        _timelines = FakeTimelineReader.Holding(FakeTimelineReader.APost(content: string.Empty));

        var run = Run(["timeline", "home"]);

        Assert.Contains("jeff@mastodon.social", run.Output);
        Assert.DoesNotContain("\n\n  3 boosts", run.Output.ReplaceLineEndings("\n"));
    }

    [Fact]
    public void Home_ShowsAContentWarningApartFromThePostsText()
    {
        AddProfile();
        _timelines = FakeTimelineReader.Holding(FakeTimelineReader.APost(contentWarning: "spoilers"));

        var run = Run(["timeline", "home"]);

        Assert.Contains("spoilers", run.Output);
        Assert.Contains("Hello world", run.Output);
    }

    /// <summary>Printing nothing at all leaves a user unable to tell an empty timeline from a broken client.</summary>
    [Fact]
    public void Home_SaysSoWhenTheTimelineHasNothingOnIt()
    {
        AddProfile();
        _timelines = FakeTimelineReader.Holding();

        var run = Run(["timeline", "home"]);

        Assert.Equal((int)ExitCode.Success, run.ExitCode);
        Assert.Contains("No posts", run.Output);
        Assert.Empty(run.ErrorOutput.Trim());
    }

    [Fact]
    public void Home_AsksForAsManyPostsAsTheLimitSays()
    {
        AddProfile();

        var run = Run(["timeline", "home", "--limit", "60"]);

        Assert.Equal((int)ExitCode.Success, run.ExitCode);
        Assert.Equal(60, Assert.Single(_timelines.Reads).Limit);
    }

    [Fact]
    public void Home_AsksForAScreensWorthOfPostsWhenNoLimitIsGiven()
    {
        AddProfile();

        var run = Run(["timeline", "home"]);

        Assert.Equal((int)ExitCode.Success, run.ExitCode);
        Assert.Equal(20, Assert.Single(_timelines.Reads).Limit);
    }

    [Fact]
    public void Home_ReportsALimitOfNoPostsAsAUsageError()
    {
        AddProfile();

        var run = Run(["timeline", "home", "--limit", "0"]);

        Assert.Equal((int)ExitCode.UsageError, run.ExitCode);
        Assert.Empty(_timelines.Reads);
    }

    [Fact]
    public void Home_ReadsAsTheProfileNamedByTheOverrideWithoutChangingTheDefault()
    {
        AddProfile();
        AddProfile("work", "hachyderm.io");

        var run = Run(["timeline", "home", "--profile", "work"]);

        Assert.Equal((int)ExitCode.Success, run.ExitCode);
        Assert.Equal("work", Assert.Single(_timelines.Reads).Profile);
    }

    [Fact]
    public void Home_ReportsThatNothingIsSetUpYetWithTheAuthenticationExitCode()
    {
        var run = Run(["timeline", "home"]);

        Assert.Equal((int)ExitCode.AuthenticationError, run.ExitCode);
        Assert.Contains("No profiles", run.ErrorOutput);
        Assert.Empty(_timelines.Reads);
    }

    /// <summary>
    ///     The ticket's point, at the front end: the posts that did arrive are printed and kept, the limit is reported
    ///     as the failure it is, and the exit code says which failure — so a script can tell this apart from a
    ///     timeline that simply had nothing on it.
    /// </summary>
    [Fact]
    public void Home_ShowsThePostsItGotAndReportsTheRateLimitThatStoppedTheRest()
    {
        AddProfile();
        _timelines = FakeTimelineReader.RateLimitedAfter(FakeTimelineReader.APost());

        var run = Run(["timeline", "home"]);

        Assert.Equal((int)ExitCode.RateLimited, run.ExitCode);
        Assert.Contains("Hello world", run.Output);
        Assert.Contains("Rate limited by mastodon.social", run.ErrorOutput);
        Assert.DoesNotContain("Rate limited", run.Output);
    }

    /// <summary>
    ///     The distinction the ticket turns on, in the case where it is easiest to get wrong: a rate limit that stopped
    ///     the fetch before a single post arrived has no posts to show, and must not therefore be described as a
    ///     timeline with nothing on it. What the user is told is the limit, not the opposite of it.
    /// </summary>
    [Fact]
    public void Home_DoesNotCallARateLimitedFetchAnEmptyTimeline()
    {
        AddProfile();
        _timelines = FakeTimelineReader.RateLimitedAfter();

        var run = Run(["timeline", "home"]);

        Assert.Equal((int)ExitCode.RateLimited, run.ExitCode);
        Assert.DoesNotContain("No posts", run.Output);
        Assert.Contains("Rate limited by mastodon.social", run.ErrorOutput);
    }

    /// <summary>Every timeline is machine-readable, not just the one whose test happened to ask for JSON.</summary>
    [Theory]
    [InlineData("home")]
    [InlineData("local")]
    [InlineData("federated")]
    public void Timeline_WritesEveryTimelineAsJsonWhenAskedTo(string command)
    {
        AddProfile();

        var run = Run(["timeline", command, "--json"]);

        Assert.Equal((int)ExitCode.Success, run.ExitCode);

        var timeline = JsonDocument.Parse(run.Output).RootElement;

        Assert.Equal(command, timeline.GetProperty("timeline").GetString());
        Assert.Single(timeline.GetProperty("posts").EnumerateArray().ToList());
    }

    [Fact]
    public void Home_WritesTheTimelineAsMachineReadableJson()
    {
        AddProfile();

        var run = Run(["timeline", "home", "--json"]);

        Assert.Equal((int)ExitCode.Success, run.ExitCode);

        var timeline = JsonDocument.Parse(run.Output).RootElement;

        Assert.Equal("home", timeline.GetProperty("timeline").GetString());
        Assert.True(timeline.GetProperty("complete").GetBoolean());

        var post = Assert.Single(timeline.GetProperty("posts").EnumerateArray().ToList());
        Assert.Equal("110", post.GetProperty("id").GetString());
        Assert.Equal("jeff@mastodon.social", post.GetProperty("account").GetString());
        Assert.Equal("Hello world", post.GetProperty("content").GetString());
        Assert.Equal(3, post.GetProperty("boosts").GetInt64());
        Assert.Equal(5, post.GetProperty("favorites").GetInt64());
    }

    [Fact]
    public void Tag_NamesTheHashtagItReadInTheJsonItWrites()
    {
        AddProfile();

        var run = Run(["timeline", "tag", "#cats", "--json"]);

        var timeline = JsonDocument.Parse(run.Output).RootElement;

        Assert.Equal("tag", timeline.GetProperty("timeline").GetString());
        Assert.Equal("cats", timeline.GetProperty("hashtag").GetString());
    }

    /// <summary>
    ///     Under a pipe, a rate limit has to be readable from the output itself — the exit code is gone by the time
    ///     the JSON reaches whatever is parsing it, and an empty <c>posts</c> would otherwise read as a quiet timeline.
    /// </summary>
    [Fact]
    public void Home_MarksJsonIncompleteWhenARateLimitStoppedTheFetchShort()
    {
        AddProfile();
        _timelines = FakeTimelineReader.RateLimitedAfter();

        var run = Run(["timeline", "home", "--json"]);

        Assert.Equal((int)ExitCode.RateLimited, run.ExitCode);

        var timeline = JsonDocument.Parse(run.Output).RootElement;

        Assert.False(timeline.GetProperty("complete").GetBoolean());
        Assert.Empty(timeline.GetProperty("posts").EnumerateArray().ToList());
        Assert.Equal("mastodon.social", timeline.GetProperty("rateLimit").GetProperty("instance").GetString());
        Assert.Equal(
            new DateTimeOffset(2026, 7, 29, 13, 0, 0, TimeSpan.Zero),
            timeline.GetProperty("rateLimit").GetProperty("resetsAt").GetDateTimeOffset());
    }

    /// <summary>
    ///     A pipe is the whole reason <c>--json</c> exists, and a console 80 columns wide would fold a long post's
    ///     line in half. Rendering has to stay out of the way of output meant for a parser.
    /// </summary>
    [Fact]
    public void Home_WritesJsonUnwrappedByTheConsolesWidth()
    {
        AddProfile();
        _timelines = FakeTimelineReader.Holding(FakeTimelineReader.APost(content: new string('x', 400)));

        var run = Run(["timeline", "home", "--json"], consoleWidth: 80);

        Assert.Equal(new string('x', 400), JsonDocument.Parse(run.Output)
                                                       .RootElement.GetProperty("posts")[0]
                                                       .GetProperty("content")
                                                       .GetString());
    }

    private void AddProfile(string name = "personal", string instance = "mastodon.social") =>
        Run(["profile", "add", name, "--instance", instance, "--token", $"token-{name}"]);

    private CommandRun Run(string[] args, int consoleWidth = 200)
    {
        var console = new TestConsole().Width(consoleWidth);
        var errorConsole = new TestConsole().Width(consoleWidth);

        var app = WoolyCommandApp.Create(console, errorConsole, services =>
        {
            services.AddSingleton(new WoolyPaths(_directory.Path));
            services.AddSingleton<ICredentialStore>(new PlaintextFileCredentialStore(new WoolyPaths(_directory.Path)));
            services.AddSingleton<IAccessTokenVerifier>(FakeAccessTokenVerifier.Accepting());
            services.AddSingleton<ITimelineReader>(_timelines);
        });

        var exitCode = app.Run(args, TestContext.Current.CancellationToken);

        return new CommandRun(exitCode, console.Output, errorConsole.Output);
    }

    private sealed record CommandRun(int ExitCode, string Output, string ErrorOutput);
}
