using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console.Testing;
using Wooly.Cli;
using Wooly.Core;
using Wooly.Core.Credentials;
using Wooly.Core.Profiles;
using Wooly.Core.Trends;
using Wooly.Tests.Fakes;

namespace Wooly.Tests.Cli;

/// <summary>
///     Reading trends driven the way a user drives it: whole commands through the real command app, over a real config
///     file and token store in a scratch directory, with the instance's trends faked at <see cref="ITrends" /> —
///     ADR-0005's primary seam, which is what a command test is meant to fake (#601).
/// </summary>
public class TrendsCommandTests : IDisposable
{
    private readonly TemporaryDirectory _directory = new();

    private FakeTrends _trends = FakeTrends.Trending();

    public void Dispose() => _directory.Dispose();

    /// <summary>
    ///     All three kinds, each under its own heading, in the order the spec gives: hashtags, posts, links — and each
    ///     read once, at the instance's own default size, as the profile.
    /// </summary>
    [Fact]
    public void Trends_PrintsTrendingHashtagsPostsAndLinksUnderTheirOwnHeadings()
    {
        AddProfile();

        var run = Run(["trends"]);

        Assert.Equal((int)ExitCode.Success, run.ExitCode);
        Assert.Empty(run.ErrorOutput.Trim());

        var output = run.Output;
        int[] order =
        [
            output.IndexOf("Trending hashtags", StringComparison.Ordinal),
            output.IndexOf("#cats", StringComparison.Ordinal),
            output.IndexOf("Trending posts", StringComparison.Ordinal),
            output.IndexOf("Hello world", StringComparison.Ordinal),
            output.IndexOf("Trending links", StringComparison.Ordinal),
            output.IndexOf("Cats learn to type", StringComparison.Ordinal),
        ];

        Assert.DoesNotContain(-1, order);
        Assert.Equal(order.Order(), order);

        Assert.Equal(
            [TrendKind.Hashtags, TrendKind.Posts, TrendKind.Links],
            _trends.Reads.Select(read => read.Kind));
        Assert.All(_trends.Reads, read => Assert.Equal(TrendPage.First, read.Page));
        Assert.All(_trends.Reads, read => Assert.Equal("personal", read.Profile));
    }

    /// <summary>
    ///     A trending link says where it goes — the address is what <c>timeline link</c> takes and a browser opens —
    ///     whose site it is on, and how many people are sharing it.
    /// </summary>
    [Fact]
    public void Trends_SaysWhereATrendingLinkGoesAndHowManyAreSharingIt()
    {
        AddProfile();

        var run = Run(["trends", "--type", "links"]);

        Assert.Contains("Cats learn to type", run.Output);
        Assert.Contains("https://news.example/cats-learn-to-type", run.Output);
        Assert.Contains("Example News", run.Output);
        Assert.Contains("65 people sharing", run.Output);
    }

    /// <summary>
    ///     <c>--type</c> asks the instance for that kind alone, rather than reading all three and printing one: each
    ///     kind is a call of its own, and one not wanted is one not spent.
    /// </summary>
    [Theory]
    [InlineData("tags", TrendKind.Hashtags, "#cats", "Hello world")]
    [InlineData("posts", TrendKind.Posts, "Hello world", "#cats")]
    [InlineData("LINKS", TrendKind.Links, "Cats learn to type", "Hello world")]
    public void Trends_NarrowsToTheOneKindItWasAskedFor(string type, TrendKind kind, string shown, string notShown)
    {
        AddProfile();

        var run = Run(["trends", "--type", type]);

        Assert.Equal((int)ExitCode.Success, run.ExitCode);
        Assert.Equal(kind, Assert.Single(_trends.Reads).Kind);
        Assert.Contains(shown, run.Output);
        Assert.DoesNotContain(notShown, run.Output);
        Assert.DoesNotContain("Trending", run.Output);
    }

    [Fact]
    public void Trends_ReportsATypeItDoesNotKnowAsAUsageError()
    {
        AddProfile();

        var run = Run(["trends", "--type", "hashtags"]);

        Assert.Equal((int)ExitCode.UsageError, run.ExitCode);
        Assert.Contains("tags, posts, links", run.ErrorOutput);
        Assert.Empty(_trends.Reads);
    }

    /// <summary>
    ///     Every trends endpoint pages by offset, so <c>--offset</c> needs no <c>--type</c> here, unlike search's: it is
    ///     sent with the limit to each kind asked.
    /// </summary>
    [Fact]
    public void Trends_SendsTheLimitAndOffsetToEachKindAsked()
    {
        AddProfile();

        Run(["trends", "--limit", "5", "--offset", "10"]);

        Assert.Equal(3, _trends.Reads.Count);
        Assert.All(_trends.Reads, read => Assert.Equal(TrendPage.Of(5, offset: 10), read.Page));
    }

    [Fact]
    public void Trends_SendsTheLimitAndOffsetToTheOneKindAsked()
    {
        AddProfile();

        Run(["trends", "--type", "posts", "--limit", "40", "--offset", "40"]);

        var read = Assert.Single(_trends.Reads);
        Assert.Equal(TrendKind.Posts, read.Kind);
        Assert.Equal(TrendPage.Of(40, offset: 40), read.Page);
    }

    /// <summary>
    ///     Mastodon quietly serves its maximum for a larger limit, which would leave a script reading a short page as the
    ///     end of the ranking — so a limit above it is turned down before anything is asked. With every kind asked, the
    ///     tightest maximum binds.
    /// </summary>
    [Theory]
    [InlineData(new[] { "--type", "tags", "--limit", "21" }, "20 trending tags")]
    [InlineData(new[] { "--type", "links", "--limit", "21" }, "20 trending links")]
    [InlineData(new[] { "--type", "posts", "--limit", "41" }, "40 trending posts")]
    [InlineData(new[] { "--limit", "30" }, "20 trending tags")]
    [InlineData(new[] { "--limit", "0" }, "at least one")]
    [InlineData(new[] { "--offset", "-1" }, "below zero")]
    public void Trends_ReportsAPageTheInstanceWillNotServeAsAUsageError(string[] options, string said)
    {
        AddProfile();

        var run = Run(["trends", .. options]);

        Assert.Equal((int)ExitCode.UsageError, run.ExitCode);
        Assert.Contains(said, run.ErrorOutput);
        Assert.Empty(_trends.Reads);
    }

    /// <summary>The maximum itself is a page the instance serves in full, so it is asked rather than refused.</summary>
    [Fact]
    public void Trends_AsksForAsManyAsTheInstanceServes()
    {
        AddProfile();

        var run = Run(["trends", "--limit", "20"]);

        Assert.Equal((int)ExitCode.Success, run.ExitCode);
        Assert.Equal(3, _trends.Reads.Count);
    }

    /// <summary>
    ///     An instance with trends turned off answers with nothing, and that is nothing trending — a report and a
    ///     success, not a failure a script would have to tell apart from a broken instance.
    /// </summary>
    [Fact]
    public void Trends_SaysNothingIsTrendingWhereTheInstanceHasNone()
    {
        AddProfile();
        _trends = FakeTrends.TrendingNothing();

        var run = Run(["trends"]);

        Assert.Equal((int)ExitCode.Success, run.ExitCode);
        Assert.Contains("Nothing trending on mastodon.social", run.Output);
        Assert.DoesNotContain("Trending hashtags", run.Output);
        Assert.Empty(run.ErrorOutput.Trim());
    }

    /// <summary>Said in the words of what was asked for, as an empty search is.</summary>
    [Fact]
    public void Trends_SaysWhichKindHasNothingTrendingWhenOnlyOneWasAskedFor()
    {
        AddProfile();
        _trends = FakeTrends.TrendingNothing();

        var run = Run(["trends", "--type", "links"]);

        Assert.Equal((int)ExitCode.Success, run.ExitCode);
        Assert.Contains("No trending links on mastodon.social", run.Output);
    }

    /// <summary>A kind with nothing trending among kinds with something is left out, heading and all.</summary>
    [Fact]
    public void Trends_LeavesOutTheHeadingOfAKindWithNothingTrending()
    {
        AddProfile();
        _trends = FakeTrends.Trending(links: []);

        var run = Run(["trends"]);

        Assert.Contains("Trending hashtags", run.Output);
        Assert.DoesNotContain("Trending links", run.Output);
    }

    /// <summary>
    ///     Each read is one call, so a rate limit leaves nothing to print before it is reported — an ordinary failure for
    ///     ADR-0006's one handler and its exit code, as a search's is.
    /// </summary>
    [Fact]
    public void Trends_ReportsARateLimitAsTheFailureItIs()
    {
        AddProfile();
        _trends = FakeTrends.RateLimited();

        var run = Run(["trends"]);

        Assert.Equal((int)ExitCode.RateLimited, run.ExitCode);
        Assert.Contains("Rate limited by mastodon.social", run.ErrorOutput);
        Assert.Empty(run.Output.Trim());
    }

    /// <summary>A refusal is the instance's to explain, in its own words, with the general failure's exit code.</summary>
    [Fact]
    public void Trends_ReportsARefusalInTheInstancesOwnWords()
    {
        AddProfile();
        _trends = FakeTrends.Refusing("This method requires an authenticated user");

        var run = Run(["trends"]);

        Assert.Equal((int)ExitCode.Error, run.ExitCode);
        Assert.Contains("This method requires an authenticated user", run.ErrorOutput);
    }

    [Fact]
    public void Trends_ReadsAsTheProfileNamedByTheOverride()
    {
        AddProfile();
        AddProfile("work", "hachyderm.io");

        Run(["trends", "--profile", "work"]);

        Assert.All(_trends.Reads, read => Assert.Equal("work", read.Profile));
        Assert.All(_trends.Tokens, token => Assert.Equal("token-work", token));
    }

    [Fact]
    public void Trends_WritesWhatIsTrendingAsMachineReadableJson()
    {
        AddProfile();

        var run = Run(["trends", "--json"]);

        Assert.Equal((int)ExitCode.Success, run.ExitCode);

        var trending = JsonDocument.Parse(run.Output).RootElement;

        var hashtag = Assert.Single(trending.GetProperty("hashtags").EnumerateArray().ToList());
        Assert.Equal("cats", hashtag.GetProperty("hashtag").GetString());
        Assert.Equal(42, hashtag.GetProperty("recentPosts").GetInt64());
        Assert.Equal(30, hashtag.GetProperty("recentAccounts").GetInt64());

        // The same document every command writes for a post.
        var post = Assert.Single(trending.GetProperty("posts").EnumerateArray().ToList());
        Assert.Equal("110", post.GetProperty("id").GetString());
        Assert.Equal("Hello world", post.GetProperty("content").GetString());

        var link = Assert.Single(trending.GetProperty("links").EnumerateArray().ToList());
        Assert.Equal("https://news.example/cats-learn-to-type", link.GetProperty("url").GetString());
        Assert.Equal("Cats learn to type", link.GetProperty("title").GetString());
        Assert.Equal("A study of keyboards and the cats who sit on them.", link.GetProperty("description").GetString());
        Assert.Equal("Example News", link.GetProperty("site").GetString());
        Assert.Equal(65, link.GetProperty("peopleSharing").GetInt64());
    }

    /// <summary>
    ///     ADR-0011's rule, as <c>search --json</c> keeps it: a kind not asked for is absent, so a script can tell it
    ///     from a kind that has nothing trending.
    /// </summary>
    [Fact]
    public void Trends_LeavesOutTheKindsItWasNotAskedFor()
    {
        AddProfile();

        var run = Run(["trends", "--type", "links", "--json"]);

        var trending = JsonDocument.Parse(run.Output).RootElement;

        Assert.NotEmpty(trending.GetProperty("links").EnumerateArray().ToList());
        Assert.False(trending.TryGetProperty("hashtags", out _));
        Assert.False(trending.TryGetProperty("posts", out _));
    }

    /// <summary>And a kind asked for with nothing trending is an empty list rather than an absent one.</summary>
    [Fact]
    public void Trends_WritesAKindWithNothingTrendingAsAnEmptyList()
    {
        AddProfile();
        _trends = FakeTrends.TrendingNothing();

        var run = Run(["trends", "--json"]);

        Assert.Equal((int)ExitCode.Success, run.ExitCode);

        var trending = JsonDocument.Parse(run.Output).RootElement;

        Assert.Empty(trending.GetProperty("hashtags").EnumerateArray().ToList());
        Assert.Empty(trending.GetProperty("posts").EnumerateArray().ToList());
        Assert.Empty(trending.GetProperty("links").EnumerateArray().ToList());
    }

    [Fact]
    public void Trends_ReportsThatNothingIsSetUpYetWithTheAuthenticationExitCode()
    {
        var run = Run(["trends"]);

        Assert.Equal((int)ExitCode.AuthenticationError, run.ExitCode);
        Assert.Empty(_trends.Reads);
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
            services.AddSingleton<ITrends>(_trends);
        });

        var exitCode = app.Run(args, TestContext.Current.CancellationToken);

        return new CommandRun(exitCode, console.Output, errorConsole.Output);
    }

    private sealed record CommandRun(int ExitCode, string Output, string ErrorOutput);
}
