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

    [Fact]
    public async Task Publish_NamesThePostAReplyAnswers()
    {
        var network = Answering(StatusJson("110"));

        await NewAuthor(network).Publish(
            Profile,
            Draft("Quite so") with { InReplyTo = "99" },
            TestContext.Current.CancellationToken);

        Assert.Contains("in_reply_to_id=99", network.Bodies[0]);
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
    public async Task Publish_AttachesAPollWithItsAnswersAndHowLongItStaysOpen()
    {
        var network = Answering(StatusJson("110"));

        await NewAuthor(network).Publish(
            Profile,
            Draft("Cats or dogs?") with
            {
                Poll = PostPoll.Of(["Cats", "Dogs"], TimeSpan.FromHours(6), multipleChoice: true),
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
        bool poll = false)
    {
        var attachments = string.Join(",", (attachmentIds ?? []).Select(AttachmentJson));

        return $$"""
                 {
                   "id": "{{id}}",
                   "uri": "https://mastodon.social/users/jeff/statuses/{{id}}",
                   "url": "https://mastodon.social/@jeff/{{id}}",
                   "created_at": "2026-07-29T12:00:00.000Z",
                   "account": { "id": "1", "username": "jeff", "acct": "jeff", "display_name": "Jeff" },
                   "content": "{{content}}",
                   "spoiler_text": "{{contentWarning}}",
                   "sensitive": {{(sensitive ? "true" : "false")}},
                   "visibility": "{{visibility}}",
                   "reblogs_count": 0,
                   "favourites_count": 0,
                   "replies_count": 0,
                   "media_attachments": [{{attachments}}],
                   "poll": {{(poll ? """{"id":"p1","expired":false,"multiple":false,"votes_count":2,"options":[{"title":"Cats","votes_count":1},{"title":"Dogs","votes_count":1}]}""" : "null")}}
                 }
                 """;
    }
}
