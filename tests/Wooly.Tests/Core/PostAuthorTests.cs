using System.Net;
using System.Net.Http;
using Microsoft.Extensions.DependencyInjection;
using Wooly.Core;
using Wooly.Core.Errors;
using Wooly.Core.Posts;
using Wooly.Core.Profiles;
using Wooly.Tests.Fakes;

namespace Wooly.Tests.Core;

/// <summary>
///     The author is the adapter behind <see cref="IPostAuthor" />, and ADR-0005 puts an adapter's tests at the
///     <see cref="HttpMessageHandler" /> seam: what it does is turn a draft into the request an instance takes, and turn
///     what comes back into a post, neither of which is observable above the wire. Two further things are only visible
///     here — that media is uploaded before the post that carries it, and that an edit carries forward what it was not
///     asked to change, which takes a read of the post as well as a write. Commands above this fake
///     <see cref="IPostAuthor" /> instead.
/// </summary>
public class PostAuthorTests : IDisposable
{
    private static readonly ActiveProfile Profile = new()
    {
        Name = "personal",
        Instance = "mastodon.social",
        Account = "jeff@mastodon.social",
        AccessToken = "token-personal",
    };

    private readonly TemporaryDirectory _directory = new();

    public void Dispose() => _directory.Dispose();

    [Fact]
    public async Task Publish_PublishesTheDraftsTextAsThePostsText()
    {
        var network = Answering(StatusJson("110"));

        await NewAuthor(network).Publish(Profile, Draft("Hello world"), TestContext.Current.CancellationToken);

        var request = Assert.Single(network.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("https://mastodon.social/api/v1/statuses", request.RequestUri?.ToString());
        Assert.Equal("Bearer token-personal", request.Headers.Authorization?.ToString());
        Assert.Contains("status=Hello+world", network.Bodies[0]);
    }

    /// <summary>
    ///     What comes back is a post in this project's vocabulary, the same as one read off a timeline — the id above all,
    ///     which is how every later command (boosting it, deleting it) names the thing that was just published.
    /// </summary>
    [Fact]
    public async Task Publish_ReportsThePostTheInstancePublished()
    {
        var network = Answering(StatusJson("110", visibility: "private"));

        var post = await NewAuthor(network).Publish(
            Profile,
            Draft("Hello world"),
            TestContext.Current.CancellationToken);

        Assert.Equal("110", post.Id);
        Assert.Equal("jeff@mastodon.social", post.Account);
        Assert.Equal("Hello world", post.Content);
        Assert.Equal(PostVisibility.Private, post.Visibility);
        Assert.Equal("https://mastodon.social/@jeff/110", post.Url);
    }

    /// <summary>
    ///     A warning is only a warning if the instance also knows the post is one to hide. Mastodon carries those as two
    ///     fields, and sending the text without the flag leaves a post whose warning nothing honours.
    /// </summary>
    [Fact]
    public async Task Publish_AttachesAContentWarningAndSaysThePostIsOneToHide()
    {
        var network = Answering(StatusJson("110"));

        await NewAuthor(network).Publish(
            Profile,
            Draft("Hello world") with { ContentWarning = "spoilers" },
            TestContext.Current.CancellationToken);

        Assert.Contains("spoiler_text=spoilers", network.Bodies[0]);
        Assert.Contains("sensitive=true", network.Bodies[0]);
    }

    [Theory]
    [InlineData(PostVisibility.Public, "visibility=public")]
    [InlineData(PostVisibility.Unlisted, "visibility=unlisted")]
    [InlineData(PostVisibility.Private, "visibility=private")]
    [InlineData(PostVisibility.Direct, "visibility=direct")]
    public async Task Publish_PublishesAtTheVisibilityTheDraftAsksFor(PostVisibility visibility, string expected)
    {
        var network = Answering(StatusJson("110"));

        await NewAuthor(network).Publish(
            Profile,
            Draft("Hello world") with { Visibility = visibility },
            TestContext.Current.CancellationToken);

        Assert.Contains(expected, network.Bodies[0]);
    }

    /// <summary>
    ///     A draft that says nothing about who can see it has to say nothing to the instance either. Filling in
    ///     "public" would publish an account whose own default is followers-only wider than the account asked for, and
    ///     that is not a mistake its author can take back.
    /// </summary>
    [Fact]
    public async Task Publish_LeavesVisibilityToTheAccountsOwnDefaultWhenTheDraftDoesNotSay()
    {
        var network = Answering(StatusJson("110"));

        await NewAuthor(network).Publish(Profile, Draft("Hello world"), TestContext.Current.CancellationToken);

        Assert.DoesNotContain("visibility=", network.Bodies[0]);
    }

    /// <summary>
    ///     A reply reads the post it answers before it publishes, because it may not go out wider than that post
    ///     (ADR-0013) — so the request naming the answered post comes first, and the publish second.
    /// </summary>
    [Fact]
    public async Task Publish_NamesThePostAReplyAnswers()
    {
        var network = Answering(StatusJson("110"));

        await NewAuthor(network).Publish(
            Profile,
            Draft("Quite so") with { InReplyTo = "99" },
            TestContext.Current.CancellationToken);

        Assert.Equal(2, network.Requests.Count);
        Assert.Equal(HttpMethod.Get, network.Requests[0].Method);
        Assert.Equal("https://mastodon.social/api/v1/statuses/99", network.Requests[0].RequestUri?.ToString());
        Assert.Contains("in_reply_to_id=99", network.Bodies[1]);
    }

    /// <summary>
    ///     The reason for that read. Mastodon takes whatever visibility a request names, whatever it is answering, so a
    ///     reply to a direct message composed at the account's own default would be published to the world — which is
    ///     what makes an answer to a direct message go out direct without anybody saying so.
    /// </summary>
    [Theory]
    [InlineData("direct", "visibility=direct")]
    [InlineData("private", "visibility=private")]
    [InlineData("unlisted", "visibility=unlisted")]
    public async Task Publish_AnswersAPostAsNarrowlyAsItWasSaidWhenTheDraftDoesNotSay(string answered, string expected)
    {
        var network = Answering(StatusJson("110", visibility: answered));

        await NewAuthor(network).Publish(
            Profile,
            Draft("Quite so") with { InReplyTo = "99" },
            TestContext.Current.CancellationToken);

        Assert.Contains(expected, network.Bodies[1]);
    }

    /// <summary>
    ///     A standing preference too wide for the post being answered is narrowed to fit, without comment. Refusing it
    ///     would leave a profile whose <c>default_visibility</c> is public unable to answer a direct message at all.
    /// </summary>
    [Fact]
    public async Task Publish_NarrowsAStandingPreferenceTooWideForThePostItAnswers()
    {
        var network = Answering(StatusJson("110", visibility: "direct"));

        await NewAuthor(network).Publish(
            Profile,
            Draft("Quite so") with { InReplyTo = "99", Visibility = PostVisibility.Public },
            TestContext.Current.CancellationToken);

        Assert.Contains("visibility=direct", network.Bodies[1]);
    }

    /// <summary>
    ///     A visibility named on the invocation itself is refused rather than narrowed: publishing something other than
    ///     what was asked for is not a thing to do quietly, and under a pipe the sentence saying so is read by nothing.
    /// </summary>
    [Fact]
    public async Task Publish_RefusesToAnswerAPostMoreWidelyThanItWasSaid()
    {
        var network = Answering(StatusJson("110", visibility: "direct"));

        var refusal = await Assert.ThrowsAsync<WiderReplyException>(
            () => NewAuthor(network).Publish(
                Profile,
                Draft("Quite so") with
                {
                    InReplyTo = "99",
                    Visibility = PostVisibility.Public,
                    VisibilityChosen = true,
                },
                TestContext.Current.CancellationToken));

        Assert.Equal(PostVisibility.Public, refusal.Asked);
        Assert.Equal(PostVisibility.Direct, refusal.Answered);

        // Refused before anything was published, so there is nothing to take back.
        Assert.Single(network.Requests);
    }

    /// <summary>Narrower than the post being answered is the author's to choose, and is left alone.</summary>
    [Fact]
    public async Task Publish_AnswersAPostMoreNarrowlyThanItWasSaidWhenAsked()
    {
        var network = Answering(StatusJson("110", visibility: "public"));

        await NewAuthor(network).Publish(
            Profile,
            Draft("Quite so") with
            {
                InReplyTo = "99",
                Visibility = PostVisibility.Direct,
                VisibilityChosen = true,
            },
            TestContext.Current.CancellationToken);

        Assert.Contains("visibility=direct", network.Bodies[1]);
    }

    /// <summary>A post answering nothing has nothing to be wider than, and pays for no read.</summary>
    [Fact]
    public async Task Publish_ReadsNoPostForOneThatAnswersNothing()
    {
        var network = Answering(StatusJson("110"));

        await NewAuthor(network).Publish(Profile, Draft("Hello world"), TestContext.Current.CancellationToken);

        Assert.Single(network.Requests);
    }

    /// <summary>
    ///     The ticket's point about media: attaching is part of composing, so one call to this port uploads the files and
    ///     publishes the post that carries them, in that order and in the order the author gave them.
    /// </summary>
    [Fact]
    public async Task Publish_UploadsEachFileAndThenPublishesThePostCarryingThem()
    {
        var network = new ScriptedHttpMessageHandler(
            ScriptedHttpMessageHandler.Json(AttachmentJson("m1")),
            ScriptedHttpMessageHandler.Json(AttachmentJson("m2")),
            ScriptedHttpMessageHandler.Json(StatusJson("110")));

        await NewAuthor(network).Publish(
            Profile,
            Draft("Two pictures") with
            {
                Media =
                [
                    new MediaAttachment { Path = _directory.WriteFile("first.png") },
                    new MediaAttachment { Path = _directory.WriteFile("second.png") },
                ],
            },
            TestContext.Current.CancellationToken);

        Assert.Equal(3, network.Requests.Count);
        Assert.Equal("https://mastodon.social/api/v2/media", network.Requests[0].RequestUri?.ToString());
        Assert.Equal("https://mastodon.social/api/v2/media", network.Requests[1].RequestUri?.ToString());
        Assert.Equal("https://mastodon.social/api/v1/statuses", network.Requests[2].RequestUri?.ToString());

        // In the order the author gave them: an attachment's place on a post is part of what they composed.
        Assert.Contains("media_ids%5B%5D=m1&media_ids%5B%5D=m2", network.Bodies[2]);
    }

    [Fact]
    public async Task Publish_SendsAnAttachmentsAltTextAlongWithTheFileItDescribes()
    {
        var network = new ScriptedHttpMessageHandler(
            ScriptedHttpMessageHandler.Json(AttachmentJson("m1")),
            ScriptedHttpMessageHandler.Json(StatusJson("110")));

        await NewAuthor(network).Publish(
            Profile,
            Draft("A cat") with
            {
                Media = [new MediaAttachment { Path = _directory.WriteFile("cat.png"), AltText = "a ginger cat" }],
            },
            TestContext.Current.CancellationToken);

        Assert.Contains("cat.png", network.Bodies[0]);
        Assert.Contains("a ginger cat", network.Bodies[0]);
    }

    /// <summary>
    ///     A mistyped path is found before anything is sent, so a post with three attachments and a typo in the third
    ///     publishes nothing at all rather than something half composed that cannot be taken back.
    /// </summary>
    [Fact]
    public async Task Publish_UploadsNothingAndPublishesNothingWhenAFileIsNotThere()
    {
        var network = Answering(StatusJson("110"));

        var missing = Path.Combine(_directory.Path, "not-here.png");

        var failure = await Assert.ThrowsAsync<MediaNotFoundException>(() => NewAuthor(network).Publish(
            Profile,
            Draft("A cat") with
            {
                Media =
                [
                    new MediaAttachment { Path = _directory.WriteFile("real.png") },
                    new MediaAttachment { Path = missing },
                ],
            },
            TestContext.Current.CancellationToken));

        Assert.Equal(missing, failure.Path);
        Assert.Empty(network.Requests);
    }

    /// <summary>
    ///     The draft's own rule, asked again at the boundary. Reaching here with a draft an instance would refuse is a
    ///     defect in whatever built it, and it costs nothing to say so before a request goes out.
    /// </summary>
    [Fact]
    public async Task Publish_RefusesADraftWithNothingToSay()
    {
        var network = Answering(StatusJson("110"));

        await Assert.ThrowsAsync<ArgumentException>(() => NewAuthor(network).Publish(
            Profile,
            Draft(string.Empty),
            TestContext.Current.CancellationToken));

        Assert.Empty(network.Requests);
    }

    [Fact]
    public async Task Publish_ReportsThePostsAvatarUrl()
    {
        var network = Answering(StatusJson("110", avatarUrl: "https://mastodon.social/avatars/jeff.png"));

        var post = await NewAuthor(network).Publish(Profile, Draft("Hello world"), TestContext.Current.CancellationToken);

        Assert.Equal("https://mastodon.social/avatars/jeff.png", post.AvatarUrl);
    }

    /// <summary>
    ///     The common case: the account being answered is one the post itself names, so the reply target can be
    ///     resolved with no extra fetch.
    /// </summary>
    [Fact]
    public async Task Publish_ResolvesTheReplyTargetsHandleFromTheMentionItMatches()
    {
        var network = Answering(StatusJson(
            "110",
            inReplyToId: "99",
            inReplyToAccountId: "42",
            mentionsJson: """{"id":"42","username":"maria","acct":"maria@fosstodon.org","url":"https://fosstodon.org/@maria"}"""));

        var post = await NewAuthor(network).Publish(Profile, Draft("Quite so"), TestContext.Current.CancellationToken);

        Assert.Equal("99", post.InReplyTo?.PostId);
        Assert.Equal("maria@fosstodon.org", post.InReplyTo?.Handle);
    }

    /// <summary>A self-reply's handle is the post's own author, since Mastodon does not mention an author on their own post.</summary>
    [Fact]
    public async Task Publish_ResolvesASelfReplysHandleAsThePostsOwnAuthor()
    {
        var network = Answering(StatusJson("110", inReplyToId: "99", inReplyToAccountId: "1"));

        var post = await NewAuthor(network).Publish(Profile, Draft("Quite so"), TestContext.Current.CancellationToken);

        Assert.Equal("99", post.InReplyTo?.PostId);
        Assert.Equal(post.Account, post.InReplyTo?.Handle);
    }

    /// <summary>
    ///     An account this client cannot name — reachable only by an id the post never mentions — leaves the handle
    ///     null rather than guessed at.
    /// </summary>
    [Fact]
    public async Task Publish_LeavesTheReplyTargetsHandleNullWhenTheAnsweredAccountIsNotInMentions()
    {
        var network = Answering(StatusJson("110", inReplyToId: "99", inReplyToAccountId: "42"));

        var post = await NewAuthor(network).Publish(Profile, Draft("Quite so"), TestContext.Current.CancellationToken);

        Assert.Equal("99", post.InReplyTo?.PostId);
        Assert.Null(post.InReplyTo?.Handle);
    }

    [Fact]
    public async Task Publish_ReadsNoReplyTargetForAPostThatAnswersNothing()
    {
        var network = Answering(StatusJson("110"));

        var post = await NewAuthor(network).Publish(Profile, Draft("Hello world"), TestContext.Current.CancellationToken);

        Assert.Null(post.InReplyTo);
    }

    /// <summary>
    ///     The withheld state some instances report until this profile votes or the poll closes — a real third state
    ///     for an option's own count, distinct from a genuine zero.
    /// </summary>
    [Fact]
    public async Task Publish_ReadsAPollsWithheldOptionCountAsNullRatherThanZero()
    {
        var network = Answering(StatusJson(
            "110",
            pollJson: """{"id":"p1","expired":false,"multiple":false,"votes_count":5,"options":[{"title":"Cats","votes_count":null},{"title":"Dogs","votes_count":5}]}"""));

        var post = await NewAuthor(network).Publish(Profile, Draft("Cats or dogs?"), TestContext.Current.CancellationToken);

        Assert.Equal(5, post.Poll?.Votes);
        Assert.Null(post.Poll?.Options[0].Votes);
        Assert.Equal(5, post.Poll?.Options[1].Votes);
    }

    [Fact]
    public async Task Publish_ReadsThePollsShapeOffTheInstance()
    {
        var network = Answering(StatusJson(
            "110",
            pollJson: """
                      {
                        "id": "p1", "expired": true, "multiple": true, "votes_count": 9, "voters_count": 7,
                        "voted": true, "expires_at": "2026-08-01T00:00:00.000Z", "own_votes": [1],
                        "options": [{"title":"Cats","votes_count":4},{"title":"Dogs","votes_count":5}]
                      }
                      """));

        var post = await NewAuthor(network).Publish(Profile, Draft("Cats or dogs?"), TestContext.Current.CancellationToken);

        var poll = post.Poll!;
        Assert.True(poll.MultipleChoice);
        Assert.True(poll.Closed);
        Assert.True(poll.Voted);
        Assert.Equal(7, poll.Voters);
        Assert.Equal(new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.Zero), poll.ExpiresAt);
        Assert.False(poll.Options[0].Picked);
        Assert.True(poll.Options[1].Picked);
    }

    [Fact]
    public async Task Publish_AttachesAPollWithItsAnswersAndHowLongItStaysOpen()
    {
        var network = Answering(StatusJson("110"));

        await NewAuthor(network).Publish(
            Profile,
            Draft("Cats or dogs?") with
            {
                Poll = PollDraft.Of(["Cats", "Dogs"], TimeSpan.FromHours(6), multipleChoice: true),
            },
            TestContext.Current.CancellationToken);

        var body = network.Bodies[0];
        Assert.Contains("poll%5Boptions%5D%5B%5D=Cats", body);
        Assert.Contains("poll%5Boptions%5D%5B%5D=Dogs", body);
        Assert.Contains("poll%5Bexpires_in%5D=21600", body);
        Assert.Contains("poll%5Bmultiple%5D=true", body);
    }

    [Fact]
    public async Task Edit_ReplacesThePostsText()
    {
        var network = Answering(StatusJson("110"));

        var post = await NewAuthor(network).Edit(
            Profile,
            "110",
            new PostEdit { Text = "Fixed the typo" },
            TestContext.Current.CancellationToken);

        Assert.Equal("110", post.Id);
        Assert.Equal(HttpMethod.Put, network.Requests[^1].Method);
        Assert.Equal("https://mastodon.social/api/v1/statuses/110", network.Requests[^1].RequestUri?.ToString());
        Assert.Contains("status=Fixed+the+typo", network.Bodies[^1]);
    }

    /// <summary>
    ///     The ticket's quietest trap. Mastodon's edit replaces a post rather than amending it, so an edit that names no
    ///     attachments is an edit that removes them — which would make fixing a typo a way to lose a photograph.
    /// </summary>
    [Fact]
    public async Task Edit_CarriesThePostsExistingAttachmentsThrough()
    {
        var network = new ScriptedHttpMessageHandler(
            ScriptedHttpMessageHandler.Json(StatusJson("110", attachmentIds: ["m1", "m2"])),
            ScriptedHttpMessageHandler.Json(StatusJson("110", attachmentIds: ["m1", "m2"])));

        await NewAuthor(network).Edit(
            Profile,
            "110",
            new PostEdit { Text = "Fixed the typo" },
            TestContext.Current.CancellationToken);

        Assert.Equal(2, network.Requests.Count);
        Assert.Equal(HttpMethod.Get, network.Requests[0].Method);
        Assert.Contains("media_ids%5B%5D=m1&media_ids%5B%5D=m2", network.Bodies[1]);
    }

    /// <summary>
    ///     The same trap, and worse where it catches: a warning dropped by an edit that never mentioned it shows a reader
    ///     exactly what they had asked not to be shown.
    /// </summary>
    [Fact]
    public async Task Edit_LeavesTheContentWarningAloneWhenTheEditSaysNothingAboutIt()
    {
        var network = Answering(StatusJson("110", contentWarning: "spoilers"));

        await NewAuthor(network).Edit(
            Profile,
            "110",
            new PostEdit { Text = "Fixed the typo" },
            TestContext.Current.CancellationToken);

        Assert.Contains("spoiler_text=spoilers", network.Bodies[^1]);
        Assert.Contains("sensitive=true", network.Bodies[^1]);
    }

    [Fact]
    public async Task Edit_ReplacesTheContentWarningWhenTheEditGivesANewOne()
    {
        var network = Answering(StatusJson("110", contentWarning: "spoilers"));

        await NewAuthor(network).Edit(
            Profile,
            "110",
            new PostEdit { Text = "Fixed the typo", ContentWarning = "later spoilers" },
            TestContext.Current.CancellationToken);

        Assert.Contains("spoiler_text=later+spoilers", network.Bodies[^1]);
    }

    /// <summary>
    ///     The other direction, and not symmetrical with it: a warning given to a post that had none marks the post as
    ///     one to hide, which is what the same expression has always done on publish. Worth pinning now that the TUI
    ///     reaches it — before #140, `e` could not touch this flag at all.
    /// </summary>
    [Fact]
    public async Task Edit_MarksAPostToHideWhenTheEditGivesItAWarningItHadNone()
    {
        var network = Answering(StatusJson("110"));

        await NewAuthor(network).Edit(
            Profile,
            "110",
            new PostEdit { Text = "Fixed the typo", ContentWarning = "spoilers" },
            TestContext.Current.CancellationToken);

        Assert.Contains("spoiler_text=spoilers", network.Bodies[^1]);
        Assert.Contains("sensitive=true", network.Bodies[^1]);
    }

    /// <summary>An empty warning is how an author says the post no longer needs one.</summary>
    [Fact]
    public async Task Edit_TakesTheContentWarningAwayWhenTheEditAsksForNoWarning()
    {
        var network = Answering(StatusJson("110", contentWarning: "spoilers"));

        await NewAuthor(network).Edit(
            Profile,
            "110",
            new PostEdit { Text = "Fixed the typo", ContentWarning = string.Empty },
            TestContext.Current.CancellationToken);

        Assert.Contains("spoiler_text=", network.Bodies[^1]);
        Assert.DoesNotContain("spoiler_text=spoilers", network.Bodies[^1]);
        Assert.DoesNotContain("sensitive=true", network.Bodies[^1]);
    }

    /// <summary>
    ///     The third thing an edit must not drop, and the one with no text to read it off: a post can be marked as one to
    ///     hide because of what its pictures show, with no warning written at all. Working the flag out from the warning
    ///     alone would un-blur those pictures on an edit that only fixed a typo.
    /// </summary>
    [Fact]
    public async Task Edit_KeepsAPostHiddenWhenItWasHiddenWithNoWarningWrittenOnIt()
    {
        var network = Answering(StatusJson("110", sensitive: true, attachmentIds: ["m1"]));

        await NewAuthor(network).Edit(
            Profile,
            "110",
            new PostEdit { Text = "Fixed the typo" },
            TestContext.Current.CancellationToken);

        Assert.Contains("sensitive=true", network.Bodies[^1]);
    }

    /// <summary>
    ///     Nor does taking the warning away un-blur anything: the flag is the instance's mark over the attachments and
    ///     survives an edit that removes the words. Worth pinning now that the TUI asks for the warning to be taken off
    ///     every time an author clears the field (#140), where before only an explicit CLI flag could.
    /// </summary>
    [Fact]
    public async Task Edit_KeepsAFlaggedPostHiddenWhenTheWarningIsTakenAway()
    {
        var network = Answering(StatusJson("110", contentWarning: "spoilers", sensitive: true, attachmentIds: ["m1"]));

        await NewAuthor(network).Edit(
            Profile,
            "110",
            new PostEdit { Text = "Fixed the typo", ContentWarning = string.Empty },
            TestContext.Current.CancellationToken);

        Assert.DoesNotContain("spoiler_text=spoilers", network.Bodies[^1]);
        Assert.Contains("sensitive=true", network.Bodies[^1]);
    }

    /// <summary>
    ///     A warning made of spaces hides a post behind nothing, which is worse than either hiding it or not. Read the
    ///     same way as it is when a post is first composed.
    /// </summary>
    [Fact]
    public async Task Edit_ReadsAWarningMadeOnlyOfSpacesAsNoWarningAtAll()
    {
        var network = Answering(StatusJson("110", contentWarning: "spoilers"));

        await NewAuthor(network).Edit(
            Profile,
            "110",
            new PostEdit { Text = "Fixed the typo", ContentWarning = "   " },
            TestContext.Current.CancellationToken);

        Assert.Contains("spoiler_text=", network.Bodies[^1]);
        Assert.DoesNotContain("spoiler_text=spoilers", network.Bodies[^1]);
        Assert.DoesNotContain("sensitive=true", network.Bodies[^1]);
    }

    /// <summary>
    ///     The one thing this client will not do rather than do badly: an edit cannot carry a poll through, and a request
    ///     that left the poll out would take it — and every vote cast in it — away.
    /// </summary>
    [Fact]
    public async Task Edit_RefusesAPostCarryingAPollRatherThanQuietlyRemoveIt()
    {
        var network = Answering(StatusJson("110", poll: true));

        var failure = await Assert.ThrowsAsync<UneditablePostException>(() => NewAuthor(network).Edit(
            Profile,
            "110",
            new PostEdit { Text = "Fixed the typo" },
            TestContext.Current.CancellationToken));

        Assert.Equal("110", failure.PostId);

        // It read the post and stopped there. Nothing was written.
        Assert.Single(network.Requests);
        Assert.Equal(HttpMethod.Get, network.Requests[0].Method);
    }

    [Fact]
    public async Task Delete_TakesThePostDown()
    {
        var network = new ScriptedHttpMessageHandler(ScriptedHttpMessageHandler.Status(HttpStatusCode.OK));

        await NewAuthor(network).Delete(Profile, "110", TestContext.Current.CancellationToken);

        var request = Assert.Single(network.Requests);
        Assert.Equal(HttpMethod.Delete, request.Method);
        Assert.Equal("https://mastodon.social/api/v1/statuses/110", request.RequestUri?.ToString());
        Assert.Equal("Bearer token-personal", request.Headers.Authorization?.ToString());
    }

    private static PostDraft Draft(string text) => new() { Text = text };

    private static ScriptedHttpMessageHandler Answering(string json) =>
        new(ScriptedHttpMessageHandler.Json(json));

    /// <summary>Resolved from the container the app builds, so the wiring is under test alongside the behavior.</summary>
    private static IPostAuthor NewAuthor(HttpMessageHandler network)
    {
        var services = new ServiceCollection();
        services.AddWoolyCore();
        services.AddHttpClient(WoolyClient.HttpClientName).ConfigurePrimaryHttpMessageHandler(() => network);

        return services.BuildServiceProvider().GetRequiredService<IPostAuthor>();
    }

    private static string AttachmentJson(string id) =>
        $$"""{"id":"{{id}}","type":"image","url":"https://mastodon.social/media/{{id}}.png"}""";

    private static string StatusJson(
        string id,
        string content = "<p>Hello world</p>",
        string? contentWarning = null,
        string visibility = "public",
        string[]? attachmentIds = null,
        bool sensitive = false,
        bool poll = false,
        string? pollJson = null,
        string? avatarUrl = null,
        string? inReplyToId = null,
        string? inReplyToAccountId = null,
        string? mentionsJson = null)
    {
        var attachments = string.Join(",", (attachmentIds ?? []).Select(AttachmentJson));

        return $$"""
                 {
                   "id": "{{id}}",
                   "uri": "https://mastodon.social/users/jeff/statuses/{{id}}",
                   "url": "https://mastodon.social/@jeff/{{id}}",
                   "created_at": "2026-07-29T12:00:00.000Z",
                   "account": { "id": "1", "username": "jeff", "acct": "jeff", "display_name": "Jeff", "avatar": "{{avatarUrl}}" },
                   "content": "{{content}}",
                   "spoiler_text": "{{contentWarning}}",
                   "sensitive": {{(sensitive ? "true" : "false")}},
                   "visibility": "{{visibility}}",
                   "reblogs_count": 0,
                   "favourites_count": 0,
                   "replies_count": 0,
                   "media_attachments": [{{attachments}}],
                   "in_reply_to_id": {{(inReplyToId is null ? "null" : $"\"{inReplyToId}\"")}},
                   "in_reply_to_account_id": {{(inReplyToAccountId is null ? "null" : $"\"{inReplyToAccountId}\"")}},
                   "mentions": [{{mentionsJson}}],
                   "poll": {{(pollJson ?? (poll ? """{"id":"p1","expired":false,"multiple":false,"votes_count":2,"options":[{"title":"Cats","votes_count":1},{"title":"Dogs","votes_count":1}]}""" : "null"))}}
                 }
                 """;
    }
}
