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

    /// <summary>
    ///     Stories 50 and 51: the CLI links an attachment and says what it shows, whatever kind it is and whatever the
    ///     terminal could have drawn. Nothing here is conditional on a capability, because the same command run twice
    ///     on two machines has to produce the same bytes.
    /// </summary>
    [Theory]
    [InlineData(MediaKind.Image)]
    [InlineData(MediaKind.Animation)]
    [InlineData(MediaKind.Video)]
    [InlineData(MediaKind.Audio)]
    [InlineData(MediaKind.Unknown)]
    public void Show_LinksAnAttachmentAndSaysWhatItShows(MediaKind kind)
    {
        AddProfile();
        _posts = FakePostEngagement.Answering(APost.With(media: [APost.Attached(kind)]));

        var run = Run(["post", "show", "110"]);

        Assert.Contains("https://files.mastodon.social/m1/original.png", run.Output);
        Assert.Contains("A cartoon sheep", run.Output);
    }

    /// <summary>An attachment nobody described says so, rather than trailing off after the address.</summary>
    [Fact]
    public void Show_SaysWhereNobodyDescribedAnAttachment()
    {
        AddProfile();
        _posts = FakePostEngagement.Answering(APost.With(
            media: [APost.Attached(MediaKind.Video, description: null)]));

        var run = Run(["post", "show", "110"]);

        Assert.Contains("a video, undescribed", run.Output);
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

    /// <summary>
    ///     A script asking what a post carries gets the answer from <c>--json</c> rather than from the human output —
    ///     including whether the attachment was described at all, which is the question the human line answers with a
    ///     phrase of this client's own.
    /// </summary>
    [Fact]
    public void Show_WritesWhatIsAttachedAsMachineReadableJson()
    {
        AddProfile();
        _posts = FakePostEngagement.Answering(APost.With(media:
        [
            APost.APicture(description: "A cartoon sheep"),
            APost.Attached(MediaKind.Video, id: "m2", description: null),
        ]));

        var media = JsonDocument.Parse(Run(["post", "show", "110", "--json"]).Output)
                                .RootElement.GetProperty("media");

        Assert.Equal(2, media.GetArrayLength());

        Assert.Equal("m1", media[0].GetProperty("id").GetString());
        Assert.Equal("image", media[0].GetProperty("kind").GetString());
        Assert.Equal("https://files.mastodon.social/m1/original.png", media[0].GetProperty("url").GetString());
        Assert.Equal("https://files.mastodon.social/m1/small.png", media[0].GetProperty("preview").GetString());
        Assert.Equal("A cartoon sheep", media[0].GetProperty("description").GetString());

        // Left out rather than written as null, which is how this client says "does not apply" everywhere else.
        Assert.Equal("video", media[1].GetProperty("kind").GetString());
        Assert.False(media[1].TryGetProperty("description", out _));
    }

    /// <summary>A post carrying nothing says so with an empty list, rather than leaving the key out.</summary>
    [Fact]
    public void Show_WritesAnEmptyListForAPostWithNothingAttached()
    {
        AddProfile();

        var media = JsonDocument.Parse(Run(["post", "show", "110", "--json"]).Output)
                                .RootElement.GetProperty("media");

        Assert.Equal(0, media.GetArrayLength());
    }

    [Fact]
    public void Show_MarksAReplyAsAnsweringTheAccountItNames()
    {
        AddProfile();
        _posts = FakePostEngagement.Answering(APost.With(
            inReplyTo: new PostReplyTarget { PostId = "99", Handle = "maria@fosstodon.org" }));

        var run = Run(["post", "show", "110"]);

        Assert.Contains("↳ answering @maria@fosstodon.org", run.Output);
    }

    /// <summary>A self-reply is marked as continuing rather than naming the author back to themself.</summary>
    [Fact]
    public void Show_MarksASelfReplyAsContinuing()
    {
        AddProfile();
        _posts = FakePostEngagement.Answering(APost.With(
            account: "jeff@mastodon.social",
            inReplyTo: new PostReplyTarget { PostId = "99", Handle = "jeff@mastodon.social" }));

        var run = Run(["post", "show", "110"]);

        Assert.Contains("↳ continuing", run.Output);
    }

    /// <summary>The bare mark for a reply whose answered account this client cannot name.</summary>
    [Fact]
    public void Show_MarksAnUnresolvableReplyAsTheBareFactOfReplying()
    {
        AddProfile();
        _posts = FakePostEngagement.Answering(APost.With(
            inReplyTo: new PostReplyTarget { PostId = "99", Handle = null }));

        var run = Run(["post", "show", "110"]);

        Assert.Contains("↳ reply", run.Output);
        Assert.DoesNotContain("↳ answering", run.Output);
    }

    /// <summary>A post that answers nothing carries no reply mark at all.</summary>
    [Fact]
    public void Show_CarriesNoReplyMarkForAPostThatAnswersNothing()
    {
        AddProfile();

        var run = Run(["post", "show", "110"]);

        Assert.DoesNotContain("↳", run.Output);
    }

    /// <summary>Each option's bar, its share and raw count, and a leading mark on the one this profile picked.</summary>
    [Fact]
    public void Show_DrawsABarWithThePercentageAndRawCountForEachOption()
    {
        AddProfile();
        _posts = FakePostEngagement.Answering(APost.With(poll: APost.APoll(
            options: [APost.AnAnswer("Cats", 4), APost.AnAnswer("Dogs", 6, picked: true)],
            votes: 10)));

        var run = Run(["post", "show", "110"]);

        Assert.Contains("▓▓▓▓░░░░░░ 40% (4)  Cats", run.Output);
        Assert.Contains("✓ ▓▓▓▓▓▓░░░░ 60% (6)  Dogs", run.Output);
    }

    /// <summary>A genuinely unvoted option still draws a bar, at 0% — not the same thing as a withheld count.</summary>
    [Fact]
    public void Show_DrawsAnEmptyBarAndZeroPercentForAGenuinelyUnvotedOption()
    {
        AddProfile();
        _posts = FakePostEngagement.Answering(APost.With(poll: APost.APoll(
            options: [APost.AnAnswer("Cats", 0), APost.AnAnswer("Dogs", 6)],
            votes: 6)));

        var run = Run(["post", "show", "110"]);

        Assert.Contains("░░░░░░░░░░ 0% (0)  Cats", run.Output);
    }

    /// <summary>
    ///     An instance withholds a per-option breakdown until this profile votes or the poll closes — a third state,
    ///     distinct from a genuine zero, that draws no bar at all rather than guess at one.
    /// </summary>
    [Fact]
    public void Show_DrawsNoBarAtAllForAnOptionWhoseCountIsWithheld()
    {
        AddProfile();
        _posts = FakePostEngagement.Answering(APost.With(poll: APost.APoll(
            options: [APost.AnAnswer("Cats", null), APost.AnAnswer("Dogs", null)],
            votes: 0)));

        var run = Run(["post", "show", "110"]);

        Assert.Contains("Cats", run.Output);
        Assert.Contains("Dogs", run.Output);
        Assert.DoesNotContain("▓", run.Output);
        Assert.DoesNotContain("░", run.Output);
    }

    [Fact]
    public void Show_ReportsAClosedPollInPlaceOfWhenItCloses()
    {
        AddProfile();
        _posts = FakePostEngagement.Answering(APost.With(poll: APost.APoll(closed: true)));

        var run = Run(["post", "show", "110"]);

        Assert.Contains("Poll closed", run.Output);
    }

    [Fact]
    public void Show_ReportsAnOpenPollsClosingTime()
    {
        AddProfile();
        _posts = FakePostEngagement.Answering(APost.With(poll: APost.APoll(
            expiresAt: new DateTimeOffset(2026, 8, 10, 12, 0, 0, TimeSpan.Zero))));

        var run = Run(["post", "show", "110"]);

        Assert.Contains("Poll closes", run.Output);
    }

    [Fact]
    public void Show_SaysNothingAboutClosingWhenThePollHasNoEndDate()
    {
        AddProfile();
        _posts = FakePostEngagement.Answering(APost.With(poll: APost.APoll()));

        var run = Run(["post", "show", "110"]);

        Assert.DoesNotContain("Poll closes", run.Output);
        Assert.DoesNotContain("Poll closed", run.Output);
    }

    [Fact]
    public void Show_NotesWhenAPollLetsAVoterChooseMoreThanOneAnswer()
    {
        AddProfile();
        _posts = FakePostEngagement.Answering(APost.With(poll: APost.APoll(multipleChoice: true)));

        var run = Run(["post", "show", "110"]);

        Assert.Contains("Choose as many as you like.", run.Output);
    }

    /// <summary>The vote count normally says only itself, however many accounts cast the votes.</summary>
    [Fact]
    public void Show_ReportsTheVoteCount()
    {
        AddProfile();
        _posts = FakePostEngagement.Answering(APost.With(poll: APost.APoll(votes: 10)));

        var run = Run(["post", "show", "110"]);

        Assert.Contains("10 votes", run.Output);
        Assert.DoesNotContain("accounts", run.Output);
    }

    /// <summary>
    ///     Multiple choice lets one account cast several votes, so the count says both — but only once an instance has
    ///     actually reported how many accounts that was.
    /// </summary>
    [Fact]
    public void Show_ReportsVotesAndVotersForAMultipleChoicePollThatNamesBoth()
    {
        AddProfile();
        _posts = FakePostEngagement.Answering(
            APost.With(poll: APost.APoll(votes: 16, voters: 7, multipleChoice: true)));

        var run = Run(["post", "show", "110"]);

        Assert.Contains("16 votes from 7 accounts", run.Output);
    }

    /// <summary>A multiple-choice poll whose instance withheld the voter count still says just the vote count.</summary>
    [Fact]
    public void Show_ReportsOnlyTheVoteCountForMultipleChoiceWithNoVoterCount()
    {
        AddProfile();
        _posts = FakePostEngagement.Answering(
            APost.With(poll: APost.APoll(votes: 16, voters: null, multipleChoice: true)));

        var run = Run(["post", "show", "110"]);

        Assert.Contains("16 votes", run.Output);
        Assert.DoesNotContain("accounts", run.Output);
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
