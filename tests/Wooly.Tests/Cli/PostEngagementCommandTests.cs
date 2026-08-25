using System.Text.Json;
using Mastonet;
using Mastonet.Entities;
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

    /// <summary>
    ///     ADR-0018: the CLI prints what an instance made of a link the author wrote — the address to reach it by, and
    ///     the title, site and description the raw link does not carry. After whatever is attached, which is the order
    ///     both surfaces render a post in.
    /// </summary>
    [Fact]
    public void Show_LinksALinkPreviewAfterWhatIsAttached()
    {
        AddProfile();
        _posts = FakePostEngagement.Answering(APost.With(
            media: [APost.APicture()],
            linkPreview: APost.ALinkPreview()));

        var output = Run(["post", "show", "110"]).Output.ReplaceLineEndings("\n");

        // The address and the title on the walked line, the way an attachment's are, and what the instance said about
        // the page a step further in under it.
        Assert.Contains(
            "  ⏵ https://example.com/sheep — Sheep, at length\n"
            + "    Example News\n"
            + "    What a flock does all winter\n"
            + "    by Maria Shepherd\n",
            output);

        Assert.True(
            output.IndexOf("original.png", StringComparison.Ordinal)
            < output.IndexOf("example.com/sheep", StringComparison.Ordinal),
            "The link preview follows what is attached.");
    }

    /// <summary>
    ///     The page's own byline, as plain text — never an address of its own, so a post does not come to carry three
    ///     things reaching for the same handful of places (ADR-0018).
    /// </summary>
    [Fact]
    public void Show_NamesWhoALinkPreviewSaysWroteThePage()
    {
        AddProfile();
        _posts = FakePostEngagement.Answering(APost.With(linkPreview: APost.ALinkPreview()));

        Assert.Contains("by Maria Shepherd", Run(["post", "show", "110"]).Output);
    }

    /// <summary>
    ///     The site's name stands in for a title the instance made nothing of, rather than a line with nothing after
    ///     the dash — and is not then said twice.
    /// </summary>
    [Fact]
    public void Show_StandsTheSitesNameInForALinkPreviewWithNoTitle()
    {
        AddProfile();
        _posts = FakePostEngagement.Answering(APost.With(
            linkPreview: APost.ALinkPreview(title: null, description: null, author: null)));

        var output = Run(["post", "show", "110"]).Output;

        Assert.Contains("https://example.com/sheep — Example News", output);
        Assert.Equal(1, output.Split("Example News").Length - 1);
    }

    /// <summary>
    ///     A preview the instance sent nothing but an address for is still worth the line: the address is the whole
    ///     reason a link preview is rendered at all.
    /// </summary>
    [Fact]
    public void Show_LinksALinkPreviewTheInstanceNamedNothingOf()
    {
        AddProfile();
        _posts = FakePostEngagement.Answering(APost.With(linkPreview: APost.ALinkPreview(
            title: null,
            description: null,
            providerName: null,
            author: null)));

        Assert.Contains("⏵ https://example.com/sheep", Run(["post", "show", "110"]).Output);
    }

    /// <summary>
    ///     A post with no preview prints nothing extra — no empty mark, no dangling dash where a title would have
    ///     been. Said over a post carrying an attachment, since the two share a mark: counting the marks proves the
    ///     preview added none, where looking for the mark at all would only prove the post had nothing on it.
    /// </summary>
    [Fact]
    public void Show_PrintsNothingExtraForAPostWithNoLinkPreview()
    {
        AddProfile();
        _posts = FakePostEngagement.Answering(APost.With(media: [APost.APicture()]));

        var output = Run(["post", "show", "110"]).Output;

        Assert.Equal(1, output.Split("⏵").Length - 1);
    }

    /// <summary>
    ///     The CLI prints a warned post's link preview like any other, the same asymmetry with the TUI that #113
    ///     settled for attachments: nothing is rendered here for a warning to be about, and no key to ask past it with.
    /// </summary>
    [Fact]
    public void Show_LinksALinkPreviewOnAWarnedPostToo()
    {
        AddProfile();
        _posts = FakePostEngagement.Answering(APost.With(
            contentWarning: "spoilers",
            sensitive: true,
            linkPreview: APost.ALinkPreview()));

        Assert.Contains("https://example.com/sheep — Sheep, at length", Run(["post", "show", "110"]).Output);
    }

    /// <summary>
    ///     What the human output says, for a script — with the field names this client's <c>*Document</c>s use rather
    ///     than the domain record's, since they are a contract with whatever is parsing them.
    /// </summary>
    [Fact]
    public void Show_WritesTheLinkPreviewAsMachineReadableJson()
    {
        AddProfile();
        _posts = FakePostEngagement.Answering(APost.With(linkPreview: APost.ALinkPreview()));

        var link = JsonDocument.Parse(Run(["post", "show", "110", "--json"]).Output)
                               .RootElement.GetProperty("linkPreview");

        Assert.Equal("https://example.com/sheep", link.GetProperty("url").GetString());
        Assert.Equal("Sheep, at length", link.GetProperty("title").GetString());
        Assert.Equal("Example News", link.GetProperty("provider").GetString());
        Assert.Equal("What a flock does all winter", link.GetProperty("description").GetString());
        Assert.Equal("https://files.example.com/sheep/card.png", link.GetProperty("image").GetString());
        Assert.Equal("Maria Shepherd", link.GetProperty("author").GetString());
    }

    /// <summary>
    ///     What the instance said nothing about is left out rather than written as null, which is how this client says
    ///     "does not apply" everywhere else — including the preview itself, on a post that has none.
    /// </summary>
    [Fact]
    public void Show_LeavesOutWhatALinkPreviewDoesNotSay()
    {
        AddProfile();
        _posts = FakePostEngagement.Answering(APost.With(linkPreview: APost.ALinkPreview(
            description: null,
            image: null,
            author: null)));

        var post = JsonDocument.Parse(Run(["post", "show", "110", "--json"]).Output).RootElement;
        var link = post.GetProperty("linkPreview");

        Assert.False(link.TryGetProperty("description", out _));
        Assert.False(link.TryGetProperty("image", out _));
        Assert.False(link.TryGetProperty("author", out _));
    }

    /// <summary>A post the instance made nothing of carries no <c>linkPreview</c> key at all.</summary>
    [Fact]
    public void Show_LeavesTheLinkPreviewOutForAPostWithNone()
    {
        AddProfile();

        var post = JsonDocument.Parse(Run(["post", "show", "110", "--json"]).Output).RootElement;

        Assert.False(post.TryGetProperty("linkPreview", out _));
    }

    /// <summary>
    ///     #122: a post the instance flagged says so, beside the warning its author wrote rather than instead of it.
    ///     The two are separate fields on the wire and separate promises to a reader, so a report that folded one into
    ///     the other would print a flagged post exactly like a clean one.
    /// </summary>
    [Fact]
    public void Show_SaysTheInstanceFlaggedThePostBesideTheWarningItsAuthorWrote()
    {
        AddProfile();
        _posts = FakePostEngagement.Answering(APost.With(
            contentWarning: "spoilers",
            sensitive: true,
            media: [APost.APicture()]));

        var output = Run(["post", "show", "110"]).Output.ReplaceLineEndings("\n");

        Assert.Contains("  content warning: spoilers\n  marked sensitive\n", output);

        // Said, not acted on: the address is printed as it always was (ADR-0016).
        Assert.Contains("https://files.mastodon.social/m1/original.png", output);
    }

    /// <summary>
    ///     The flag stands on its own. Mastodon's commonest sensitive post is a picture with nothing written over it,
    ///     so a report that only spoke where a warning already had would say nothing on exactly those.
    /// </summary>
    [Fact]
    public void Show_SaysSoForAPostFlaggedWithNothingWrittenOverIt()
    {
        AddProfile();
        _posts = FakePostEngagement.Answering(APost.With(sensitive: true, media: [APost.APicture()]));

        Assert.Contains("marked sensitive", Run(["post", "show", "110"]).Output);
    }

    /// <summary>
    ///     The flag is reported as the instance set it, including on a post carrying nothing for it to be over — which
    ///     an instance is free to send. <see cref="Post.IsWarned" /> discounts that post because there is nothing there
    ///     to ask past; this surface asks past nothing and so has nothing to discount, only a flag to report.
    /// </summary>
    [Fact]
    public void Show_SaysSoEvenWhereThePostCarriesNothingTheFlagCouldBeOver()
    {
        AddProfile();
        _posts = FakePostEngagement.Answering(APost.With(sensitive: true));

        Assert.Contains("marked sensitive", Run(["post", "show", "110"]).Output);
    }

    /// <summary>
    ///     A post nobody flagged says nothing: the line is a fact about the post, not a heading over every one.
    /// </summary>
    [Fact]
    public void Show_SaysNothingAboutTheFlagOnAPostTheInstanceDidNotFlag()
    {
        AddProfile();
        _posts = FakePostEngagement.Answering(APost.With(media: [APost.APicture()]));

        Assert.DoesNotContain("marked sensitive", Run(["post", "show", "110"]).Output);
    }

    /// <summary>
    ///     A boost carries the flag of the post it points at, like every other thing about it: the boost itself has no
    ///     media of its own for a flag to be over, and what is on screen is the post underneath.
    /// </summary>
    [Fact]
    public void Show_SaysTheFlagOfThePostABoostPointsAt()
    {
        AddProfile();
        _posts = FakePostEngagement.Answering(APost.With(
            account: "sam@hachyderm.io",
            boosted: APost.With(sensitive: true, media: [APost.APicture()])));

        Assert.Contains("marked sensitive", Run(["post", "show", "110"]).Output);
    }

    /// <summary>
    ///     And a script reads that flag off the post the boost points at, where the human output reads it — the boost
    ///     itself answers for the boost, the way <c>contentWarning</c> and <c>media</c> already do.
    /// </summary>
    [Fact]
    public void Show_WritesTheFlagOfABoostOnThePostItPointsAt()
    {
        AddProfile();
        _posts = FakePostEngagement.Answering(APost.With(
            account: "sam@hachyderm.io",
            boosted: APost.With(sensitive: true, media: [APost.APicture()])));

        var post = JsonDocument.Parse(Run(["post", "show", "110", "--json"]).Output).RootElement;

        Assert.True(post.GetProperty("boosted").GetProperty("sensitive").GetBoolean());
        Assert.False(post.GetProperty("sensitive").GetBoolean());
    }

    /// <summary>
    ///     The same fact for a script, so <c>--json</c> and the human output agree about a post rather than one of
    ///     them alone being able to tell a flagged post from a clean one (#122).
    /// </summary>
    [Fact]
    public void Show_WritesTheSensitiveFlagAsMachineReadableJson()
    {
        AddProfile();
        _posts = FakePostEngagement.Answering(APost.With(sensitive: true, media: [APost.APicture()]));

        var post = JsonDocument.Parse(Run(["post", "show", "110", "--json"]).Output).RootElement;

        Assert.True(post.GetProperty("sensitive").GetBoolean());
    }

    /// <summary>
    ///     Written on every post rather than only the flagged ones, unlike the keys this client leaves out where they
    ///     do not apply: <see langword="false" /> is an answer rather than an absence, and a script filtering a
    ///     timeline on it should not have to tell "not flagged" from "a client too old to say".
    /// </summary>
    [Fact]
    public void Show_WritesTheSensitiveFlagOnAPostNobodyFlaggedToo()
    {
        AddProfile();

        var post = JsonDocument.Parse(Run(["post", "show", "110", "--json"]).Output).RootElement;

        Assert.False(post.GetProperty("sensitive").GetBoolean());
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

    /// <summary>
    ///     Each option reads back as the row the poll itself writes (<see cref="PollBar.RowOf" />), indented, and led
    ///     by a <c>✓</c> on the one this profile picked. What the row says is asserted in
    ///     <see cref="Core.PollBarTests" /> and not restated here — restating it is how the two surfaces drifted
    ///     apart in the first place (#150).
    /// </summary>
    [Fact]
    public void Show_WritesEachOptionAsThePollsOwnRow()
    {
        AddProfile();
        var poll = APost.APoll(
            options: [APost.AnAnswer("Cats", 4), APost.AnAnswer("Dogs", 6, picked: true)],
            votes: 10);
        _posts = FakePostEngagement.Answering(APost.With(poll: poll));

        var run = Run(["post", "show", "110"]);

        Assert.Contains($"  {PollBar.RowOf(poll, poll.Options[0])}", run.Output, StringComparison.Ordinal);
        Assert.Contains($"  {PollBar.RowOf(poll, poll.Options[1])}", run.Output, StringComparison.Ordinal);
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

    /// <summary>
    ///     The answers are numbered as a person reads them, from 1, and reach the port as indices into the poll's own
    ///     options — the zero the API counts by is nothing the command line ever says.
    /// </summary>
    [Fact]
    public void Vote_CastsTheAnswerNamedOnTheCommandLine()
    {
        AddProfile();
        _posts = WithAPoll();

        var run = Run(["post", "vote", "110", "2"]);

        Assert.Equal((int)ExitCode.Success, run.ExitCode);
        Assert.Empty(run.ErrorOutput.Trim());

        var cast = Assert.Single(_posts.Votes);
        Assert.Equal("personal", cast.Profile);
        Assert.Equal("110", cast.PostId);
        Assert.Equal([1], cast.Choices);
    }

    /// <summary>A poll that lets a voter choose several takes several, in the order they were typed.</summary>
    [Fact]
    public void Vote_CastsEveryAnswerNamedWhereThePollAllowsMoreThanOne()
    {
        AddProfile();
        _posts = WithAPoll(APost.APoll(multipleChoice: true));

        var run = Run(["post", "vote", "110", "2", "1"]);

        Assert.Equal((int)ExitCode.Success, run.ExitCode);
        Assert.Equal([1, 0], Assert.Single(_posts.Votes).Choices);
    }

    /// <summary>Mastodon votes on the poll, whose id is only knowable from the post — so the post is read first.</summary>
    [Fact]
    public void Vote_ReadsThePostTheIdNamesBeforeVotingInItsPoll()
    {
        AddProfile();
        _posts = WithAPoll();

        Run(["post", "vote", "110", "1"]);

        Assert.Equal("110", Assert.Single(_posts.Reads).PostId);
    }

    /// <summary>A vote nothing takes back, so a person at a terminal is asked before one is cast.</summary>
    [Fact]
    public void Vote_AsksBeforeCastingWhenThereIsSomebodyToAsk()
    {
        AddProfile();
        _posts = WithAPoll();

        var run = Run(["post", "vote", "110", "1"], atATerminal: true, typed: "y");

        Assert.Equal((int)ExitCode.Success, run.ExitCode);
        Assert.Equal([0], Assert.Single(_posts.Votes).Choices);
    }

    [Fact]
    public void Vote_LeavesThePollAloneWhenTheAnswerIsNo()
    {
        AddProfile();
        _posts = WithAPoll();

        var run = Run(["post", "vote", "110", "1"], atATerminal: true, typed: "n");

        Assert.Equal((int)ExitCode.Success, run.ExitCode);
        Assert.Empty(_posts.Votes);
        Assert.Contains("Left the poll", run.Output);
    }

    [Fact]
    public void Vote_DoesNotAskWhenTheCommandLineAlreadySaidYes()
    {
        AddProfile();
        _posts = WithAPoll();

        var run = Run(["post", "vote", "110", "1", "--yes"], atATerminal: true);

        Assert.Equal((int)ExitCode.Success, run.ExitCode);
        Assert.Single(_posts.Votes);
    }

    /// <summary>
    ///     A script has nobody to answer a prompt, and stopping to ask would make the command unusable in the
    ///     automation the CLI exists for. Typing the command is that invocation's consent.
    /// </summary>
    [Fact]
    public void Vote_CastsWithoutAskingWhereThereIsNoTerminal()
    {
        AddProfile();
        _posts = WithAPoll();

        var run = Run(["post", "vote", "110", "1"], atATerminal: false);

        Assert.Equal((int)ExitCode.Success, run.ExitCode);
        Assert.Single(_posts.Votes);
    }

    /// <summary>
    ///     The vote endpoint answers with the poll as it now stands, and that is the part the voter does not already
    ///     know — so it is what gets written back out.
    /// </summary>
    [Fact]
    public void Vote_ReportsThePollAsItNowStands()
    {
        AddProfile();
        _posts = WithAPoll();
        var voted = APost.APoll(
            options: [APost.AnAnswer("Cats", 4), APost.AnAnswer("Dogs", 7, picked: true)],
            votes: 11,
            voted: true);
        _posts.Voted = APost.With(poll: voted);

        var run = Run(["post", "vote", "110", "2"]);

        Assert.Contains("Voted in the poll on", run.Output);
        Assert.Contains(PollBar.RowOf(voted, voted.Options[1]), run.Output, StringComparison.Ordinal);
        Assert.Contains("11 votes", run.Output);
    }

    [Fact]
    public void Vote_WritesTheVotedPostAsMachineReadableJson()
    {
        AddProfile();
        _posts = WithAPoll();

        var run = Run(["post", "vote", "110", "1", "--json"]);

        Assert.Equal((int)ExitCode.Success, run.ExitCode);
        Assert.Equal("110", JsonDocument.Parse(run.Output).RootElement.GetProperty("id").GetString());
    }

    /// <summary>
    ///     A number that is not one of the answers is a value on the command line that is wrong, which this client can
    ///     see for itself once it has the poll — rather than a vote sent to be turned down.
    /// </summary>
    [Fact]
    public void Vote_ReportsAnAnswerThatIsNotOnThePollAsAUsageError()
    {
        AddProfile();
        _posts = WithAPoll();

        var run = Run(["post", "vote", "110", "3"]);

        Assert.Equal((int)ExitCode.UsageError, run.ExitCode);
        Assert.Empty(_posts.Votes);
        Assert.Contains("no answer 3", run.ErrorOutput);
    }

    [Fact]
    public void Vote_ReportsAPostWithNoPollOnItAsAUsageError()
    {
        AddProfile();

        var run = Run(["post", "vote", "110", "1"]);

        Assert.Equal((int)ExitCode.UsageError, run.ExitCode);
        Assert.Empty(_posts.Votes);
        Assert.Contains("no poll", run.ErrorOutput);
    }

    [Fact]
    public void Vote_ReportsAMissingChoiceAsAUsageError()
    {
        AddProfile();
        _posts = WithAPoll();

        var run = Run(["post", "vote", "110"]);

        Assert.Equal((int)ExitCode.UsageError, run.ExitCode);
        Assert.Empty(_posts.Votes);
    }

    /// <summary>
    ///     An instance refuses a second vote outright rather than replacing the first, and says so in its own words —
    ///     which this client passes on rather than second-guessing.
    /// </summary>
    [Fact]
    public void Vote_ReportsWhatTheInstanceRefusedInTheInstancesOwnWords()
    {
        AddProfile();
        _posts = WithAPoll();
        _posts.VoteRefusal = new VoteRefusedException(
            new ServerErrorException(new Error { Description = "You have already voted on this poll" }));

        var run = Run(["post", "vote", "110", "1"]);

        Assert.NotEqual((int)ExitCode.Success, run.ExitCode);
        Assert.Contains("already voted", run.ErrorOutput);
    }

    /// <summary>An instance that holds a poll to vote in, which is what every vote test starts from.</summary>
    private static FakePostEngagement WithAPoll(PostPoll? poll = null) =>
        FakePostEngagement.Answering(APost.With(poll: poll ?? APost.APoll()));

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
            services.AddSingleton<IPostEngagement>(_posts);
        });

        var exitCode = app.Run(args, TestContext.Current.CancellationToken);

        return new CommandRun(exitCode, console.Output, errorConsole.Output);
    }

    private sealed record CommandRun(int ExitCode, string Output, string ErrorOutput);
}
