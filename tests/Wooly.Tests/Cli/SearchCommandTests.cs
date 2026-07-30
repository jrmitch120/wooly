using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console.Testing;
using Wooly.Cli;
using Wooly.Core;
using Wooly.Core.Credentials;
using Wooly.Core.Profiles;
using Wooly.Core.Search;
using Wooly.Tests.Fakes;

namespace Wooly.Tests.Cli;

/// <summary>
///     Searching driven the way a user drives it: whole commands through the real command app, over a real config file
///     and token store in a scratch directory, with the instance's search faked at <see cref="IInstanceSearch" /> —
///     ADR-0005's primary seam, which is what a command test is meant to fake.
/// </summary>
public class SearchCommandTests : IDisposable
{
    private readonly TemporaryDirectory _directory = new();

    private FakeInstanceSearch _search = FakeInstanceSearch.Finding();

    public void Dispose() => _directory.Dispose();

    /// <summary>
    ///     The point of #25: one command covers all three kinds of result, rather than one command per kind.
    /// </summary>
    [Fact]
    public void Search_FindsAccountsHashtagsAndPostsAtOnce()
    {
        AddProfile();

        var run = Run(["search", "cats"]);

        Assert.Equal((int)ExitCode.Success, run.ExitCode);
        Assert.Contains("alice@hachyderm.io", run.Output);
        Assert.Contains("#cats", run.Output);
        Assert.Contains("Hello world", run.Output);
        Assert.Empty(run.ErrorOutput.Trim());

        var search = Assert.Single(_search.Searches);
        Assert.Equal("cats", search.Query.Text);
        Assert.Equal(SearchKind.Everything, search.Query.Kind);
        Assert.Equal("personal", search.Profile);
    }

    /// <summary>An account is worth finding for what it is: who it is, and how much of a presence it has.</summary>
    [Fact]
    public void Search_ShowsWhoEachAccountIsAndHowMuchOfAPresenceItHas()
    {
        AddProfile();

        var run = Run(["search", "cats"]);

        Assert.Contains("alice@hachyderm.io", run.Output);
        Assert.Contains("Alice", run.Output);
        Assert.Contains("1203 followers", run.Output);
        Assert.Contains("4210 posts", run.Output);
        Assert.Contains("https://hachyderm.io/@alice", run.Output);
    }

    /// <summary>
    ///     A search for a word finds several near-identical tags, and the usage is what says which one people are
    ///     actually posting to. The tag is shown as <c>timeline tag</c> takes it, so the next command is typeable.
    /// </summary>
    [Fact]
    public void Search_ShowsEachHashtagWithHowMuchUseItHasHad()
    {
        AddProfile();
        _search = FakeInstanceSearch.Finding(hashtags: [AHashtag.With("caturday", recentPosts: 12, recentAccounts: 9)]);

        var run = Run(["search", "cats"]);

        Assert.Contains("#caturday", run.Output);
        Assert.Contains("12 posts", run.Output);
        Assert.Contains("9 accounts", run.Output);
    }

    /// <summary>A post found by a search reads exactly as the same post read on a timeline.</summary>
    [Fact]
    public void Search_ShowsThePostsItFoundTheWayATimelineShowsThem()
    {
        AddProfile();
        _search = FakeInstanceSearch.Finding(posts: [APost.With(content: "Cats are good")]);

        var run = Run(["search", "cats"]);

        Assert.Contains("jeff@mastodon.social", run.Output);
        Assert.Contains("Cats are good", run.Output);
        Assert.Contains("3 boosts, 5 favorites, 1 reply", run.Output);
    }

    [Theory]
    [InlineData("accounts", SearchKind.Accounts)]
    [InlineData("hashtags", SearchKind.Hashtags)]
    [InlineData("posts", SearchKind.Posts)]
    public void Search_LooksForOnlyTheKindOfResultTheTypeNames(string type, SearchKind expected)
    {
        AddProfile();

        var run = Run(["search", "cats", "--type", type]);

        Assert.Equal((int)ExitCode.Success, run.ExitCode);
        Assert.Equal(expected, Assert.Single(_search.Searches).Query.Kind);
    }

    /// <summary>Narrowing is what <c>--type</c> is for: nothing of the other two kinds reaches the screen.</summary>
    [Fact]
    public void Search_ShowsNothingButTheKindOfResultTheTypeNames()
    {
        AddProfile();

        var run = Run(["search", "cats", "--type", "accounts"]);

        Assert.Contains("alice@hachyderm.io", run.Output);
        Assert.DoesNotContain("#cats", run.Output);
        Assert.DoesNotContain("Hello world", run.Output);
    }

    [Fact]
    public void Search_ReportsATypeItDoesNotKnowAsAUsageError()
    {
        AddProfile();

        var run = Run(["search", "cats", "--type", "people"]);

        Assert.Equal((int)ExitCode.UsageError, run.ExitCode);
        Assert.Contains("accounts, hashtags, posts", run.ErrorOutput);
        Assert.Empty(_search.Searches);
    }

    /// <summary>
    ///     An instance answers a blank search with a refusal, so it is turned down here — against the value the user
    ///     typed, rather than as a failure that looks like the instance's fault.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Search_ReportsAQueryWithNothingInItAsAUsageError(string query)
    {
        AddProfile();

        var run = Run(["search", query]);

        Assert.Equal((int)ExitCode.UsageError, run.ExitCode);
        Assert.Contains("search for", run.ErrorOutput);
        Assert.Empty(_search.Searches);
    }

    [Fact]
    public void Search_ReportsAMissingQueryAsAUsageError()
    {
        AddProfile();

        var run = Run(["search"]);

        Assert.Equal((int)ExitCode.UsageError, run.ExitCode);
        Assert.Empty(_search.Searches);
    }

    /// <summary>Printing nothing at all leaves a user unable to tell an empty answer from a broken client.</summary>
    [Fact]
    public void Search_SaysSoWhenNothingMatched()
    {
        AddProfile();
        _search = FakeInstanceSearch.FindingNothing();

        var run = Run(["search", "cats"]);

        Assert.Equal((int)ExitCode.Success, run.ExitCode);
        Assert.Contains("Nothing matching 'cats'", run.Output);
        Assert.Empty(run.ErrorOutput.Trim());
    }

    /// <summary>
    ///     Said in the words of what was asked for. "Nothing matching cats" after <c>--type accounts</c> would read as
    ///     if the hashtag and the posts had been looked for too.
    /// </summary>
    [Fact]
    public void Search_SaysWhichKindNothingMatchedWhenOnlyOneWasAskedFor()
    {
        AddProfile();
        _search = FakeInstanceSearch.FindingNothing();

        var run = Run(["search", "cats", "--type", "accounts"]);

        Assert.Equal((int)ExitCode.Success, run.ExitCode);
        Assert.Contains("No accounts matching 'cats'", run.Output);
    }

    [Fact]
    public void Search_SearchesAsTheProfileNamedByTheOverrideWithoutChangingTheDefault()
    {
        AddProfile();
        AddProfile("work", "hachyderm.io");

        var run = Run(["search", "cats", "--profile", "work"]);

        Assert.Equal((int)ExitCode.Success, run.ExitCode);
        Assert.Equal("work", Assert.Single(_search.Searches).Profile);
    }

    [Fact]
    public void Search_ReportsThatNothingIsSetUpYetWithTheAuthenticationExitCode()
    {
        var run = Run(["search", "cats"]);

        Assert.Equal((int)ExitCode.AuthenticationError, run.ExitCode);
        Assert.Contains("No profiles", run.ErrorOutput);
        Assert.Empty(_search.Searches);
    }

    /// <summary>
    ///     A search is one call, so a rate limit leaves nothing to show — which is why this is a failure outright
    ///     rather than the partial answer a timeline read hands back (ADR-0011).
    /// </summary>
    [Fact]
    public void Search_ReportsARateLimitAsTheFailureItIs()
    {
        AddProfile();
        _search = FakeInstanceSearch.RateLimited();

        var run = Run(["search", "cats"]);

        Assert.Equal((int)ExitCode.RateLimited, run.ExitCode);
        Assert.Contains("Rate limited by mastodon.social", run.ErrorOutput);
        Assert.Empty(run.Output.Trim());
    }

    [Fact]
    public void Search_WritesWhatItFoundAsMachineReadableJson()
    {
        AddProfile();

        var run = Run(["search", "cats", "--json"]);

        Assert.Equal((int)ExitCode.Success, run.ExitCode);

        var found = JsonDocument.Parse(run.Output).RootElement;
        Assert.Equal("cats", found.GetProperty("query").GetString());

        var account = Assert.Single(found.GetProperty("accounts").EnumerateArray().ToList());
        Assert.Equal("alice@hachyderm.io", account.GetProperty("account").GetString());
        Assert.Equal("Alice", account.GetProperty("author").GetString());
        Assert.Equal(1203, account.GetProperty("followers").GetInt64());
        Assert.Equal(187, account.GetProperty("following").GetInt64());
        Assert.Equal(4210, account.GetProperty("posts").GetInt64());
        Assert.Equal("https://hachyderm.io/@alice", account.GetProperty("url").GetString());

        var hashtag = Assert.Single(found.GetProperty("hashtags").EnumerateArray().ToList());
        Assert.Equal("cats", hashtag.GetProperty("hashtag").GetString());
        Assert.Equal(42, hashtag.GetProperty("recentPosts").GetInt64());
        Assert.Equal(30, hashtag.GetProperty("recentAccounts").GetInt64());
    }

    /// <summary>The posts a search found are the same document every other command writes for a post.</summary>
    [Fact]
    public void Search_WritesThePostsItFoundTheWayEveryOtherCommandWritesOne()
    {
        AddProfile();

        var run = Run(["search", "cats", "--json"]);

        var post = JsonDocument.Parse(run.Output).RootElement.GetProperty("posts")[0];

        Assert.Equal("110", post.GetProperty("id").GetString());
        Assert.Equal("Hello world", post.GetProperty("content").GetString());
        Assert.Equal(3, post.GetProperty("boosts").GetInt64());
        Assert.Equal("public", post.GetProperty("visibility").GetString());
    }

    /// <summary>
    ///     The distinction <c>--type</c> would otherwise destroy: an empty <c>posts</c> would tell a script that
    ///     nothing it searched for was posted, when in fact posts were never looked for.
    /// </summary>
    [Fact]
    public void Search_LeavesOutTheKindsItWasNotAskedFor()
    {
        AddProfile();

        var run = Run(["search", "cats", "--type", "accounts", "--json"]);

        var found = JsonDocument.Parse(run.Output).RootElement;

        Assert.NotEmpty(found.GetProperty("accounts").EnumerateArray().ToList());
        Assert.False(found.TryGetProperty("hashtags", out _));
        Assert.False(found.TryGetProperty("posts", out _));
    }

    /// <summary>
    ///     A kind that was asked for and found nothing is an empty list rather than an absent one — that is the whole
    ///     difference the field is carrying.
    /// </summary>
    [Fact]
    public void Search_WritesAKindItFoundNoneOfAsAnEmptyList()
    {
        AddProfile();
        _search = FakeInstanceSearch.FindingNothing();

        var run = Run(["search", "cats", "--json"]);

        var found = JsonDocument.Parse(run.Output).RootElement;

        Assert.Empty(found.GetProperty("accounts").EnumerateArray().ToList());
        Assert.Empty(found.GetProperty("hashtags").EnumerateArray().ToList());
        Assert.Empty(found.GetProperty("posts").EnumerateArray().ToList());
    }

    /// <summary>CONTEXT.md's vocabulary, at the one place a user reads it: nothing on screen says reblog or toot.</summary>
    [Fact]
    public void Search_NamesWhatItShowsInThisProjectsVocabulary()
    {
        AddProfile();

        var run = Run(["search", "cats"]);

        Assert.Contains("boosts", run.Output);
        Assert.Contains("favorites", run.Output);
        Assert.DoesNotContain("reblog", run.Output);
        Assert.DoesNotContain("favourite", run.Output);
        Assert.DoesNotContain("toot", run.Output);
        Assert.DoesNotContain("status", run.Output);
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
            services.AddSingleton<IInstanceSearch>(_search);
        });

        var exitCode = app.Run(args, TestContext.Current.CancellationToken);

        return new CommandRun(exitCode, console.Output, errorConsole.Output);
    }

    private sealed record CommandRun(int ExitCode, string Output, string ErrorOutput);
}
