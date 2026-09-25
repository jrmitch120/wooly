using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console.Testing;
using Wooly.Cli;
using Wooly.Core;
using Wooly.Core.Accounts;
using Wooly.Core.Errors;
using Wooly.Core.Posts;
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

    private FakeTimelineReader _timelines = FakeTimelineReader.Holding(APost.With());

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

    /// <summary>
    ///     The read the CLI never had (#185): an account's own posts, asked for by address the way the tag timeline is
    ///     asked for by tag.
    /// </summary>
    [Theory]
    [InlineData("account", TimelineScope.Account)]
    [InlineData("pinned", TimelineScope.Pinned)]
    public void Account_ReadsThePostsOfTheAccountNamed(string command, TimelineScope expected)
    {
        AddProfile();

        var run = Run(["timeline", command, "alice@hachyderm.io"]);

        Assert.Equal((int)ExitCode.Success, run.ExitCode);
        Assert.Contains("Hello world", run.Output);
        Assert.Empty(run.ErrorOutput.Trim());

        var read = Assert.Single(_timelines.Reads);
        Assert.Equal(expected, read.Timeline.Scope);
        Assert.Equal("alice@hachyderm.io", read.Timeline.Account?.Address.Text);

        // A command line is an address a user typed, so nothing has been looked up yet: the read pays for the crossing
        // itself, exactly as every other CLI read of a named account does.
        Assert.Null(read.Timeline.Account?.Id);
    }

    /// <summary>
    ///     <c>--replies</c> widens the account's posts to its posts and replies — a reply never fetched is one no pipe can
    ///     get back, which is what made leaving them out a hole on this surface rather than a narrowing (#211). Without
    ///     the flag the read is what it always was.
    /// </summary>
    [Theory]
    [InlineData(new[] { "--replies" }, TimelineScope.WithReplies)]
    [InlineData(new string[0], TimelineScope.Account)]
    public void Account_ReadsThePostsAndRepliesOfTheAccountNamedWhenAskedFor(string[] flags, TimelineScope expected)
    {
        AddProfile();

        var run = Run(["timeline", "account", "alice@hachyderm.io", .. flags]);

        Assert.Equal((int)ExitCode.Success, run.ExitCode);
        Assert.Contains("Hello world", run.Output);

        var read = Assert.Single(_timelines.Reads);
        Assert.Equal(expected, read.Timeline.Scope);
        Assert.Equal("alice@hachyderm.io", read.Timeline.Account?.Address.Text);
    }

    /// <summary>
    ///     The widened run is written under a name of its own, so a saved file says which of the two it holds, and names
    ///     whose it is in full exactly as the account's posts do.
    /// </summary>
    [Fact]
    public void Account_NamesThePostsAndRepliesItReadInTheJsonItWrites()
    {
        AddProfile();

        var run = Run(["timeline", "account", "maria", "--replies", "--json"]);

        Assert.Equal((int)ExitCode.Success, run.ExitCode);

        var timeline = JsonDocument.Parse(run.Output).RootElement;

        Assert.Equal("account-replies", timeline.GetProperty("timeline").GetString());
        Assert.Equal("maria@mastodon.social", timeline.GetProperty("account").GetString());
        Assert.Equal(["timeline", "account", "complete", "posts"], FieldsOf(run));
    }

    /// <summary>The flag widens one reading and no other: the pinned run is already complete, replies and all.</summary>
    [Fact]
    public void Pinned_TurnsDownTheRepliesFlagAsAUsageError()
    {
        AddProfile();

        var run = Run(["timeline", "pinned", "alice@hachyderm.io", "--replies"]);

        Assert.Equal((int)ExitCode.UsageError, run.ExitCode);
        Assert.Empty(_timelines.Reads);
    }

    /// <summary>
    ///     A bare username means somebody on the profile's own instance, and is resolved before the timeline is built —
    ///     so that what is read, what is reported and what <c>--json</c> names is a full <c>user@host</c> rather than a
    ///     name that tells a saved file nothing about which server it came from.
    /// </summary>
    [Theory]
    [InlineData("account")]
    [InlineData("pinned")]
    public void Account_ResolvesABareUsernameAgainstTheProfilesOwnInstance(string command)
    {
        AddProfile();

        var run = Run(["timeline", command, "maria"]);

        Assert.Equal((int)ExitCode.Success, run.ExitCode);
        Assert.Equal(
            "maria@mastodon.social",
            Assert.Single(_timelines.Reads).Timeline.Account?.Address.Text);
    }

    [Theory]
    [InlineData("account")]
    [InlineData("pinned")]
    public void Account_ReportsAMissingAddressAsAUsageError(string command)
    {
        AddProfile();

        var run = Run(["timeline", command]);

        Assert.Equal((int)ExitCode.UsageError, run.ExitCode);
        Assert.NotEmpty(run.ErrorOutput.Trim());
        Assert.Empty(_timelines.Reads);
    }

    /// <summary>
    ///     Turned down against the value the user typed, by the same rule and with the same words every other command
    ///     taking an address uses (ADR-0012) — rather than reaching an instance as a name it could not look up.
    /// </summary>
    [Theory]
    [InlineData("alice@")]
    [InlineData("alice bob")]
    [InlineData("alice@two@instances")]
    public void Account_ReportsAnAddressThatNamesNoAccountAsAUsageError(string address)
    {
        AddProfile();

        var run = Run(["timeline", "account", address]);

        Assert.Equal((int)ExitCode.UsageError, run.ExitCode);
        Assert.Contains("user@instance", run.ErrorOutput);
        Assert.Empty(_timelines.Reads);
    }

    /// <summary>An account the instance cannot find is a value on the command line that is wrong, not a broken client.</summary>
    [Fact]
    public void Account_ReportsAnAccountTheInstanceCouldNotFindAsAUsageError()
    {
        AddProfile();
        _timelines = FakeTimelineReader.Refusing(
            new UnknownAccountException(AccountAddress.Parse("nobody@hachyderm.io"), "mastodon.social"));

        var run = Run(["timeline", "account", "nobody@hachyderm.io"]);

        Assert.Equal((int)ExitCode.UsageError, run.ExitCode);
        Assert.Contains("could not find an account called nobody@hachyderm.io", run.ErrorOutput);
    }

    /// <summary>The shared paged-list options are inherited rather than rewritten, zero-limit rejection included.</summary>
    [Theory]
    [InlineData("account")]
    [InlineData("account", "--replies")]
    [InlineData("pinned")]
    public void Account_TakesTheSamePagedListOptionsEveryOtherListDoes(string command, params string[] flags)
    {
        AddProfile();

        Run(["timeline", command, "alice@hachyderm.io", .. flags, "--limit", "60"]);

        Assert.Equal(60, Assert.Single(_timelines.Reads).Limit);

        var rejected = Run(["timeline", command, "alice@hachyderm.io", .. flags, "--limit", "0"]);

        Assert.Equal((int)ExitCode.UsageError, rejected.ExitCode);
        Assert.Contains("at least one post", rejected.ErrorOutput);
    }

    /// <summary>
    ///     A rate limit part way through an account's posts reports as it does on every other paged list: what arrived
    ///     is printed, and the limit that stopped the rest is the failure.
    /// </summary>
    [Fact]
    public void Account_ShowsThePostsItGotAndReportsTheRateLimitThatStoppedTheRest()
    {
        AddProfile();
        _timelines = FakeTimelineReader.RateLimitedAfter(APost.With());

        var run = Run(["timeline", "account", "alice@hachyderm.io"]);

        Assert.Equal((int)ExitCode.RateLimited, run.ExitCode);
        Assert.Contains("Hello world", run.Output);
        Assert.Contains("Rate limited by mastodon.social", run.ErrorOutput);
    }

    /// <summary>
    ///     Both new timelines are machine-readable, which they were not: the writer's scope-to-wire-name mapping covered
    ///     four scopes and threw on these two (#185). The account is named in full, so a saved file says which server
    ///     these posts came from.
    /// </summary>
    [Theory]
    [InlineData("account")]
    [InlineData("pinned")]
    public void Account_NamesWhosePostsItReadInTheJsonItWrites(string command)
    {
        AddProfile();

        var run = Run(["timeline", command, "maria", "--json"]);

        Assert.Equal((int)ExitCode.Success, run.ExitCode);

        var timeline = JsonDocument.Parse(run.Output).RootElement;

        Assert.Equal(command, timeline.GetProperty("timeline").GetString());
        Assert.Equal("maria@mastodon.social", timeline.GetProperty("account").GetString());
        Assert.Single(timeline.GetProperty("posts").EnumerateArray().ToList());
        Assert.Equal(["timeline", "account", "complete", "posts"], FieldsOf(run));
    }

    /// <summary>
    ///     The branch reaches every timeline a profile can read, which is the whole of #185: a reader who can reach four
    ///     of them and not the rest has no way to know the rest exist. One subcommand per reading rather than per scope
    ///     since #211 — the account's posts and replies is <c>account --replies</c>, the same reading widened.
    /// </summary>
    [Fact]
    public void Timeline_OffersOneSubcommandPerTimelineAProfileCanRead()
    {
        AddProfile();

        var help = Run(["timeline", "--help"]).Output;

        Assert.Contains("home", help);
        Assert.Contains("local", help);
        Assert.Contains("federated", help);
        Assert.Contains("tag", help);
        Assert.Contains("account", help);
        Assert.Contains("pinned", help);
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
        _timelines = FakeTimelineReader.Holding(APost.With(
            content: string.Empty,
            boosted: APost.With(id: "99", account: "alice@hachyderm.io", content: "The original")));

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
        _timelines = FakeTimelineReader.Holding(APost.With(content: "First\n\nSecond"));

        var run = Run(["timeline", "home"]);

        Assert.Contains("  First\n\n  Second\n", run.Output.ReplaceLineEndings("\n"));
    }

    /// <summary>A post can be nothing but media, and blank space is not what "no text" should look like.</summary>
    [Fact]
    public void Home_ShowsAPostWithNoTextAtAllWithoutAnEmptyLineStandingInForIt()
    {
        AddProfile();
        _timelines = FakeTimelineReader.Holding(APost.With(content: string.Empty));

        var run = Run(["timeline", "home"]);

        Assert.Contains("jeff@mastodon.social", run.Output);
        Assert.DoesNotContain("\n\n  3 boosts", run.Output.ReplaceLineEndings("\n"));
    }

    /// <summary>
    ///     A timeline is read down, so a web address on every post would be a line of noise on every one of them. The
    ///     address belongs to <c>post show</c>, which is a post being looked at rather than scrolled past.
    /// </summary>
    [Fact]
    public void Home_LeavesTheWebAddressOffThePostsItScrollsPast()
    {
        AddProfile();

        var run = Run(["timeline", "home"]);

        Assert.Contains("jeff@mastodon.social", run.Output);
        Assert.DoesNotContain("https://mastodon.social/@jeff/110", run.Output);
    }

    /// <summary>
    ///     A link preview is written on every post the CLI shows, a timeline's included — the same reasoning ADR-0016
    ///     gave for an attachment's address: what it carries is not reachable from anything else on screen, and leaving
    ///     it off a timeline would make <c>timeline home</c> the one place a link is enriched and cannot be followed.
    /// </summary>
    [Fact]
    public void Home_LinksALinkPreviewOnThePostsItScrollsPast()
    {
        AddProfile();
        _timelines = FakeTimelineReader.Holding(APost.With(linkPreview: APost.ALinkPreview()));

        var run = Run(["timeline", "home"]);

        Assert.Contains("https://example.com/sheep — Sheep, at length", run.Output);
        Assert.Contains("What a flock does all winter", run.Output);
    }

    /// <summary>
    ///     A post the instance flagged says so wherever the CLI writes one, a timeline's posts included (#122):
    ///     scrolling past a flagged post and a clean one printed the two identically.
    /// </summary>
    [Fact]
    public void Home_SaysWhichPostsTheInstanceFlaggedSensitive()
    {
        AddProfile();
        _timelines = FakeTimelineReader.Holding(
            APost.With(id: "110", content: "Look away", sensitive: true, media: [APost.APicture()]),
            APost.With(id: "111", content: "Nothing to see"));

        var output = Run(["timeline", "home"]).Output;

        // Said once, and on the flagged post rather than the clean one below it.
        Assert.Equal(1, output.Split("marked sensitive").Length - 1);
        Assert.True(
            output.IndexOf("marked sensitive", StringComparison.Ordinal)
            < output.IndexOf("Nothing to see", StringComparison.Ordinal),
            "The line stands on the post the instance flagged.");
    }

    [Fact]
    public void Home_ShowsAContentWarningApartFromThePostsText()
    {
        AddProfile();
        _timelines = FakeTimelineReader.Holding(APost.With(contentWarning: "spoilers"));

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
        _timelines = FakeTimelineReader.RateLimitedAfter(APost.With());

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

    /// <summary>
    ///     What a post answers, which the human output has always said and this output never did — so a pipe handed an
    ///     account's posts and replies had no way to narrow them back down to the replies (#211). Left out on a post
    ///     answering nothing, and the account left out where the post does not name who it answers, the way every
    ///     field here that does not apply is left out rather than written as a null.
    /// </summary>
    [Fact]
    public void Home_SaysWhatEachPostAnswersInTheJsonItWrites()
    {
        AddProfile();
        _timelines = FakeTimelineReader.Holding(
            APost.With(id: "110", inReplyTo: new PostReplyTarget { PostId = "100", Handle = "maria@hachyderm.io" }),
            APost.With(id: "111", inReplyTo: new PostReplyTarget { PostId = "101" }),
            APost.With(id: "112"));

        var run = Run(["timeline", "home", "--json"]);

        var posts = JsonDocument.Parse(run.Output).RootElement.GetProperty("posts").EnumerateArray().ToList();

        var named = posts[0].GetProperty("inReplyTo");
        Assert.Equal("100", named.GetProperty("post").GetString());
        Assert.Equal("maria@hachyderm.io", named.GetProperty("account").GetString());

        var unnamed = posts[1].GetProperty("inReplyTo");
        Assert.Equal("101", unnamed.GetProperty("post").GetString());
        Assert.False(unnamed.TryGetProperty("account", out _));

        Assert.False(posts[2].TryGetProperty("inReplyTo", out _));
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
    ///     Every timeline a profile can read is reachable from this branch and written under a name no other shares. The
    ///     last two scopes added were neither, at first: the writer threw on both, so every <c>--json</c> account read
    ///     would have crashed (#185). A scope added without a way here fails this rather than a user's script.
    /// </summary>
    [Fact]
    public void Timeline_WritesEveryTimelineAProfileCanReadUnderANameOfItsOwn()
    {
        AddProfile();

        var names = Enum.GetValues<TimelineScope>().Select(scope =>
        {
            var run = Run([.. CommandFor(scope), "--json"]);

            Assert.Equal((int)ExitCode.Success, run.ExitCode);

            return JsonDocument.Parse(run.Output).RootElement.GetProperty("timeline").GetString();
        }).ToList();

        Assert.Equal(names.Count, names.Distinct().Count());
        Assert.All(names, name => Assert.False(string.IsNullOrEmpty(name)));
    }

    /// <summary>The command line that reads <paramref name="scope" />, or a failure naming a scope nothing reads.</summary>
    private static string[] CommandFor(TimelineScope scope) => scope switch
    {
        TimelineScope.Home => ["timeline", "home"],
        TimelineScope.Local => ["timeline", "local"],
        TimelineScope.Federated => ["timeline", "federated"],
        TimelineScope.Tag => ["timeline", "tag", "cats"],
        TimelineScope.Account => ["timeline", "account", "alice@hachyderm.io"],
        TimelineScope.Pinned => ["timeline", "pinned", "alice@hachyderm.io"],
        TimelineScope.WithReplies => ["timeline", "account", "alice@hachyderm.io", "--replies"],
        _ => throw new ArgumentOutOfRangeException(nameof(scope), scope, "No command reads this timeline."),
    };

    /// <summary>
    ///     What a script reading this sees, field by field and in order. Asserted because the envelope is built by
    ///     naming its fields at run time rather than by declaring a record (<c>ListDocument</c>): nothing in the type
    ///     system now holds these names, this order, or the rule that a field which does not apply is left out
    ///     entirely rather than written as <c>null</c> (#101).
    /// </summary>
    [Fact]
    public void Timeline_WritesTheEnvelopesFieldsInOneOrder()
    {
        AddProfile();
        _timelines = FakeTimelineReader.RateLimitedAfter();

        var stopped = FieldsOf(Run(["timeline", "tag", "#cats", "--json"]));

        Assert.Equal(["timeline", "hashtag", "complete", "rateLimit", "posts"], stopped);

        // No hashtag was asked for and no limit stopped anything, so neither field is there at all — a null in either
        // place would say something a script would have to read past.
        _timelines = FakeTimelineReader.Holding(APost.With());

        Assert.Equal(["timeline", "complete", "posts"], FieldsOf(Run(["timeline", "home", "--json"])));
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
        _timelines = FakeTimelineReader.Holding(APost.With(content: new string('x', 400)));

        var run = Run(["timeline", "home", "--json"], consoleWidth: 80);

        Assert.Equal(new string('x', 400), JsonDocument.Parse(run.Output)
                                                       .RootElement.GetProperty("posts")[0]
                                                       .GetProperty("content")
                                                       .GetString());
    }

    /// <summary>The top-level fields of what was written, in the order they were written.</summary>
    private static IReadOnlyList<string> FieldsOf(CommandRun run) =>
        JsonDocument.Parse(run.Output)
                    .RootElement.EnumerateObject()
                    .Select(field => field.Name)
                    .ToList();

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
