using System.Net;
using System.Net.Http;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Wooly.Core;
using Wooly.Core.Accounts;
using Wooly.Core.Posts;
using Wooly.Core.Profiles;
using Wooly.Core.Timelines;
using Wooly.Tests.Fakes;

namespace Wooly.Tests.Core;

/// <summary>
///     The reader is the adapter behind <see cref="ITimelineReader" />, and ADR-0005 puts an adapter's tests at the
///     <see cref="HttpMessageHandler" /> seam: what it does is turn what Mastonet deserialized into posts, so there has
///     to be a payload for the mapping to be observable at all. Two further things it does can only be tested here for
///     ADR-0006's reason — paging asks the instance for the next page by <c>max_id</c>, and the rate limit it stops on
///     is raised below <c>IMastodonClient</c>. Commands above this fake <see cref="ITimelineReader" /> instead.
/// </summary>
public class TimelineReaderTests
{
    private static readonly ActiveProfile Profile = new()
    {
        Name = "personal",
        Instance = "mastodon.social",
        Account = "jeff@mastodon.social",
        AccessToken = "token-personal",
    };

    [Fact]
    public async Task Read_ReportsThePostsOnTheTimeline()
    {
        var network = new ScriptedHttpMessageHandler(ScriptedHttpMessageHandler.Json(Page(PostJson("110"))));

        var fetch = await NewReader(network).Read(Profile, Timeline.Home, 20, TestContext.Current.CancellationToken);

        var post = Assert.Single(fetch.Posts);
        Assert.Equal("110", post.Id);
        Assert.Equal("jeff@mastodon.social", post.Account);
        Assert.Equal("Hello world", post.Content);
    }

    /// <summary>
    ///     CONTEXT.md's vocabulary is the point of this layer: what the wire calls <c>reblogs_count</c> and
    ///     <c>favourites_count</c> reaches anything above here as boosts and favorites, spelled the project's way.
    /// </summary>
    [Fact]
    public async Task Read_ReportsAPostInThisProjectsVocabularyRatherThanTheApis()
    {
        var network = new ScriptedHttpMessageHandler(ScriptedHttpMessageHandler.Json(Page(PostJson("110"))));

        var fetch = await NewReader(network).Read(Profile, Timeline.Home, 20, TestContext.Current.CancellationToken);

        var post = Assert.Single(fetch.Posts);
        Assert.Equal("Jeff", post.Author);
        Assert.Equal(new DateTimeOffset(2026, 7, 29, 12, 0, 0, TimeSpan.Zero), post.PostedAt);
        Assert.Equal(3, post.Boosts);
        Assert.Equal(5, post.Favorites);
        Assert.Equal(1, post.Replies);
        Assert.Equal("https://mastodon.social/@jeff/110", post.Url);
        Assert.Null(post.ContentWarning);
        Assert.Null(post.Boosted);
    }

    /// <summary>
    ///     Read off the post rather than assumed. A reader who cannot see that a post went out followers-only cannot tell
    ///     which of their own posts is safe to quote elsewhere.
    /// </summary>
    [Theory]
    [InlineData("public", PostVisibility.Public)]
    [InlineData("unlisted", PostVisibility.Unlisted)]
    [InlineData("private", PostVisibility.Private)]
    [InlineData("direct", PostVisibility.Direct)]
    public async Task Read_ReportsWhoCanSeeEachPost(string wire, PostVisibility expected)
    {
        var network = new ScriptedHttpMessageHandler(
            ScriptedHttpMessageHandler.Json(Page(PostJson("110", visibility: wire))));

        var fetch = await NewReader(network).Read(Profile, Timeline.Home, 20, TestContext.Current.CancellationToken);

        Assert.Equal(expected, Assert.Single(fetch.Posts).Visibility);
    }

    /// <summary>
    ///     An account on another instance is named by the wire as <c>username@instance</c> and one of this instance's
    ///     own by bare username. A timeline mixes them, and two posts side by side may not say who wrote them two
    ///     different ways.
    /// </summary>
    [Fact]
    public async Task Read_NamesEveryAccountByTheInstanceItIsOn()
    {
        var network = new ScriptedHttpMessageHandler(
            ScriptedHttpMessageHandler.Json(Page(PostJson("110"), PostJson("109", account: "alice@hachyderm.io"))));

        var fetch = await NewReader(network).Read(Profile, Timeline.Home, 20, TestContext.Current.CancellationToken);

        Assert.Equal(["jeff@mastodon.social", "alice@hachyderm.io"], fetch.Posts.Select(post => post.Account));
    }

    /// <summary>A content warning is what the post's text is hidden behind, so it has to survive as its own field.</summary>
    [Fact]
    public async Task Read_ReportsAPostsContentWarningApartFromItsText()
    {
        var network = new ScriptedHttpMessageHandler(
            ScriptedHttpMessageHandler.Json(Page(PostJson("110", contentWarning: "spoilers"))));

        var fetch = await NewReader(network).Read(Profile, Timeline.Home, 20, TestContext.Current.CancellationToken);

        var post = Assert.Single(fetch.Posts);
        Assert.Equal("spoilers", post.ContentWarning);
        Assert.Equal("Hello world", post.Content);
    }

    /// <summary>A boost carries no text of its own, so what is worth reading is the post it points at.</summary>
    [Fact]
    public async Task Read_ReportsABoostAsWhoBoostedItAndThePostTheyBoosted()
    {
        var network = new ScriptedHttpMessageHandler(
            ScriptedHttpMessageHandler.Json(Page(PostJson(
                "110",
                account: "jeff",
                content: "",
                boosting: PostJson("99", account: "alice@hachyderm.io", content: "<p>The original</p>")))));

        var fetch = await NewReader(network).Read(Profile, Timeline.Home, 20, TestContext.Current.CancellationToken);

        var boost = Assert.Single(fetch.Posts);
        Assert.True(boost.IsBoost);
        Assert.Equal("jeff@mastodon.social", boost.Account);
        Assert.Equal("alice@hachyderm.io", boost.Boosted?.Account);
        Assert.Equal("The original", boost.Boosted?.Content);
    }

    /// <summary>
    ///     The counts say how many accounts boosted or favorited a post; these say whether one of them was the profile
    ///     doing the reading. A screen cannot draw a lit star, or offer to take a boost back rather than put one on,
    ///     without the second answer (ADR-0014).
    /// </summary>
    [Fact]
    public async Task Read_ReportsWhichMarksThisProfileAlreadyHasOnAPost()
    {
        var network = new ScriptedHttpMessageHandler(
            ScriptedHttpMessageHandler.Json(Page(PostJson(
                "110",
                marks: "\"reblogged\": true, \"favourited\": true, \"pinned\": true,"))));

        var fetch = await NewReader(network).Read(Profile, Timeline.Home, 20, TestContext.Current.CancellationToken);

        var post = Assert.Single(fetch.Posts);
        Assert.True(post.Marks.Boosted);
        Assert.True(post.Marks.Favorited);
        Assert.True(post.Marks.Pinned);
        Assert.True(post.Marks.Has(PostMark.Favorite));
    }

    /// <summary>
    ///     One mark at a time, because three flags mapped in one go is exactly where two of them come to be read off
    ///     the same field.
    /// </summary>
    [Theory]
    [InlineData(PostMark.Boost, "\"reblogged\": true,")]
    [InlineData(PostMark.Favorite, "\"favourited\": true,")]
    [InlineData(PostMark.Pin, "\"pinned\": true,")]
    public async Task Read_ReportsEachMarkFromItsOwnFieldOnTheWire(PostMark mark, string marks)
    {
        var network = new ScriptedHttpMessageHandler(ScriptedHttpMessageHandler.Json(Page(PostJson("110", marks: marks))));

        var fetch = await NewReader(network).Read(Profile, Timeline.Home, 20, TestContext.Current.CancellationToken);

        var post = Assert.Single(fetch.Posts);
        Assert.True(post.Marks.Has(mark));
        Assert.Equal(
            [mark],
            Enum.GetValues<PostMark>().Where(post.Marks.Has));
    }

    /// <summary>
    ///     An instance leaves the three flags out where it has nobody to answer them about. Every call this client
    ///     makes is signed in, so silence there is a post this profile has not marked rather than a question that was
    ///     never put.
    /// </summary>
    [Fact]
    public async Task Read_ReportsAPostAsUnmarkedWhereTheInstanceSaidNothingAboutTheMarks()
    {
        var network = new ScriptedHttpMessageHandler(ScriptedHttpMessageHandler.Json(Page(PostJson("110"))));

        var fetch = await NewReader(network).Read(Profile, Timeline.Home, 20, TestContext.Current.CancellationToken);

        Assert.Equal(PostMarks.None, Assert.Single(fetch.Posts).Marks);
    }

    /// <summary>
    ///     What came down, which is not what <see cref="MediaAttachment" /> describes: a post being read has no path on
    ///     this machine, and the description is the only field the two share.
    /// </summary>
    [Fact]
    public async Task Read_ReportsWhatIsAttachedToAPost()
    {
        var network = new ScriptedHttpMessageHandler(
            ScriptedHttpMessageHandler.Json(Page(PostJson("110", media: MediaJson()))));

        var fetch = await NewReader(network).Read(Profile, Timeline.Home, 20, TestContext.Current.CancellationToken);

        var attached = Assert.Single(Assert.Single(fetch.Posts).Media);
        Assert.Equal("m1", attached.Id);
        Assert.Equal(MediaKind.Image, attached.Kind);
        Assert.Equal("https://files.mastodon.social/m1/original.png", attached.Url);
        Assert.Equal("https://files.mastodon.social/m1/small.png", attached.Preview);
        Assert.Equal("A cartoon sheep", attached.Description);
    }

    [Theory]
    [InlineData("image", MediaKind.Image)]
    [InlineData("gifv", MediaKind.Animation)]
    [InlineData("video", MediaKind.Video)]
    [InlineData("audio", MediaKind.Audio)]
    [InlineData("unknown", MediaKind.Unknown)]
    public async Task Read_ReportsWhatKindOfThingIsAttached(string wire, MediaKind expected)
    {
        var network = new ScriptedHttpMessageHandler(
            ScriptedHttpMessageHandler.Json(Page(PostJson("110", media: MediaJson(type: wire)))));

        var fetch = await NewReader(network).Read(Profile, Timeline.Home, 20, TestContext.Current.CancellationToken);

        Assert.Equal(expected, Assert.Single(Assert.Single(fetch.Posts).Media).Kind);
    }

    /// <summary>
    ///     An instance may serve a kind newer than this client, and dropping the attachment would show the post as
    ///     nothing but its text — which is a lie a reader has no way to notice.
    /// </summary>
    [Fact]
    public async Task Read_KeepsAnAttachmentOfAKindItCannotName()
    {
        var network = new ScriptedHttpMessageHandler(
            ScriptedHttpMessageHandler.Json(Page(PostJson("110", media: MediaJson(type: "hologram")))));

        var fetch = await NewReader(network).Read(Profile, Timeline.Home, 20, TestContext.Current.CancellationToken);

        var attached = Assert.Single(Assert.Single(fetch.Posts).Media);
        Assert.Equal(MediaKind.Unknown, attached.Kind);
        Assert.Equal("https://files.mastodon.social/m1/original.png", attached.Url);
    }

    /// <summary>An attachment nobody described is not one described as the empty string.</summary>
    [Fact]
    public async Task Read_ReportsAnUndescribedAttachmentAsHavingNoDescription()
    {
        var network = new ScriptedHttpMessageHandler(
            ScriptedHttpMessageHandler.Json(Page(PostJson("110", media: MediaJson(description: "")))));

        var fetch = await NewReader(network).Read(Profile, Timeline.Home, 20, TestContext.Current.CancellationToken);

        Assert.Null(Assert.Single(Assert.Single(fetch.Posts).Media).Description);
    }

    /// <summary>A post carrying nothing carries an empty list, which is not a hole for a caller to check for.</summary>
    [Fact]
    public async Task Read_ReportsAPostWithNothingAttachedAsCarryingNoMedia()
    {
        var network = new ScriptedHttpMessageHandler(ScriptedHttpMessageHandler.Json(Page(PostJson("110"))));

        var fetch = await NewReader(network).Read(Profile, Timeline.Home, 20, TestContext.Current.CancellationToken);

        Assert.Empty(Assert.Single(fetch.Posts).Media);
    }

    /// <summary>
    ///     A boost carries no text of its own and no pictures of its own either — both belong to the post it points at,
    ///     which is what a feed has to draw.
    /// </summary>
    [Fact]
    public async Task Read_ReportsABoostsMediaAndMarksOnThePostThatWasBoosted()
    {
        var network = new ScriptedHttpMessageHandler(
            ScriptedHttpMessageHandler.Json(Page(PostJson(
                "110",
                content: "",
                boosting: PostJson(
                    "99",
                    account: "alice@hachyderm.io",
                    marks: "\"favourited\": true,",
                    media: MediaJson())))));

        var fetch = await NewReader(network).Read(Profile, Timeline.Home, 20, TestContext.Current.CancellationToken);

        var boosted = Assert.Single(fetch.Posts).Boosted;
        Assert.NotNull(boosted);
        Assert.True(boosted.Marks.Favorited);
        Assert.Equal("A cartoon sheep", Assert.Single(boosted.Media).Description);
    }

    [Theory]
    [InlineData(TimelineScope.Home, "https://mastodon.social/api/v1/timelines/home?limit=20")]
    [InlineData(TimelineScope.Local, "https://mastodon.social/api/v1/timelines/public?local=true&limit=20")]
    [InlineData(TimelineScope.Federated, "https://mastodon.social/api/v1/timelines/public?limit=20")]
    [InlineData(TimelineScope.Tag, "https://mastodon.social/api/v1/timelines/tag/cats?limit=20")]
    public async Task Read_AsksTheInstanceForTheTimelineItWasNamed(TimelineScope scope, string expected)
    {
        var network = new ScriptedHttpMessageHandler(ScriptedHttpMessageHandler.Json(Page(PostJson("110"))));

        await NewReader(network).Read(Profile, TimelineFor(scope), 20, TestContext.Current.CancellationToken);

        var request = Assert.Single(network.Requests);
        Assert.Equal(expected, request.RequestUri?.ToString());
        Assert.Equal("Bearer token-personal", request.Headers.Authorization?.ToString());
    }

    /// <summary>
    ///     An account's posts are a timeline like the other four, and the one thing that differs is that this endpoint
    ///     takes an id where a user has an address — so the address is looked up first, the same resolving search a tie
    ///     is put on an account through.
    /// </summary>
    [Fact]
    public async Task Read_LooksUpAnAccountBeforeReadingThePostsItWrote()
    {
        var network = new ScriptedHttpMessageHandler(
            ScriptedHttpMessageHandler.Json("""[{"id": "42", "username": "alice", "acct": "alice@hachyderm.io"}]"""),
            ScriptedHttpMessageHandler.Json(Page(PostJson("110"))));

        var fetch = await NewReader(network).Read(
            Profile,
            Timeline.By(AccountAddress.Parse("alice@hachyderm.io")),
            20,
            TestContext.Current.CancellationToken);

        Assert.Equal(
            "https://mastodon.social/api/v1/accounts/search?q=alice%40hachyderm.io&limit=10&resolve=true",
            network.Requests[0].RequestUri?.ToString());

        // Boosts left in and replies left out: what this account says and passes on, rather than half of a hundred
        // conversations they answered.
        Assert.Equal(
            "https://mastodon.social/api/v1/accounts/42/statuses?exclude_replies=true&limit=20",
            network.Requests[1].RequestUri?.ToString());

        Assert.Single(fetch.Posts);
    }

    /// <summary>
    ///     One lookup for the whole read, however many pages it takes. Paying for it per page would spend a call to
    ///     learn what the last one already knew.
    /// </summary>
    [Fact]
    public async Task Read_LooksUpAnAccountOnceHoweverManyPagesItsPostsTake()
    {
        var network = new ScriptedHttpMessageHandler(
            ScriptedHttpMessageHandler.Json("""[{"id": "42", "username": "alice", "acct": "alice@hachyderm.io"}]"""),
            PageResponse(PageOf(count: 40, firstId: 200), nextMaxId: "150"),
            PageResponse(PageOf(count: 5, firstId: 149)));

        var fetch = await NewReader(network).Read(
            Profile,
            Timeline.By(AccountAddress.Parse("alice@hachyderm.io")),
            45,
            TestContext.Current.CancellationToken);

        Assert.Equal(45, fetch.Posts.Count);
        Assert.Equal(3, network.Requests.Count);
        Assert.Equal(
            "https://mastodon.social/api/v1/accounts/42/statuses?exclude_replies=true&max_id=150&limit=5",
            network.Requests[2].RequestUri?.ToString());
    }

    /// <summary>Every timeline says what it is in a sentence, including the one that belongs to somebody.</summary>
    [Fact]
    public void Description_NamesWhoseTimelineAnAccountsPostsAre()
    {
        Assert.Equal(
            "the posts of @alice@hachyderm.io",
            Timeline.By(AccountAddress.Parse("alice@hachyderm.io")).Description);
    }

    /// <summary>
    ///     An instance serves at most a page at a time, so more posts than that is more than one call — and the caller
    ///     asked for posts, not pages.
    /// </summary>
    [Fact]
    public async Task Read_PagesUntilItHasAsManyPostsAsWereAskedFor()
    {
        // Deliberately not the oldest post of the first page (161), so that honouring the header is distinguishable
        // from falling back to it.
        var network = new ScriptedHttpMessageHandler(
            PageResponse(PageOf(count: 40, firstId: 200), nextMaxId: "150"),
            PageResponse(PageOf(count: 5, firstId: 149)));

        var fetch = await NewReader(network).Read(Profile, Timeline.Home, 45, TestContext.Current.CancellationToken);

        Assert.Equal(45, fetch.Posts.Count);
        Assert.True(fetch.IsComplete);
        Assert.Equal(2, network.Requests.Count);

        // The first call asks for a full page; the second asks only for what is still missing, from where the
        // instance itself said the next page starts.
        Assert.Equal("https://mastodon.social/api/v1/timelines/home?limit=40", network.Requests[0].RequestUri?.ToString());
        Assert.Equal(
            "https://mastodon.social/api/v1/timelines/home?max_id=150&limit=5",
            network.Requests[1].RequestUri?.ToString());
    }

    /// <summary>Not every instance sends the link header, and paging cannot depend on one that may not come.</summary>
    [Fact]
    public async Task Read_PagesFromTheLastPostItSawWhenTheInstanceNamesNoNextPage()
    {
        var network = new ScriptedHttpMessageHandler(
            PageResponse(PageOf(count: 40, firstId: 200)),
            PageResponse(PageOf(count: 5, firstId: 160)));

        var fetch = await NewReader(network).Read(Profile, Timeline.Home, 45, TestContext.Current.CancellationToken);

        Assert.Equal(45, fetch.Posts.Count);
        Assert.Equal(
            "https://mastodon.social/api/v1/timelines/home?max_id=161&limit=5",
            network.Requests[1].RequestUri?.ToString());
    }

    /// <summary>
    ///     A short page from an instance that names no next page is the end of the timeline, and asking again for a page
    ///     that is not there is one call spent on nothing — and, against a repeating instance, a loop.
    /// </summary>
    [Fact]
    public async Task Read_StopsWhenTheTimelineRunsOutBeforeTheLimit()
    {
        var network = new ScriptedHttpMessageHandler(PageResponse(PageOf(count: 3, firstId: 200)));

        var fetch = await NewReader(network).Read(Profile, Timeline.Home, 45, TestContext.Current.CancellationToken);

        Assert.Equal(3, fetch.Posts.Count);
        Assert.True(fetch.IsComplete);
        Assert.Single(network.Requests);
    }

    /// <summary>
    ///     A short page is not by itself the end. An instance drops posts a filter hid from a page it had already
    ///     counted, so a page can come back with room to spare and still have more behind it — which it says by naming
    ///     the next one. Reading that as the end would report a fetch as complete with most of what was asked for
    ///     missing, and no sign that anything was.
    /// </summary>
    [Fact]
    public async Task Read_KeepsPagingWhenAShortPageStillNamesANextPage()
    {
        var network = new ScriptedHttpMessageHandler(
            PageResponse(PageOf(count: 12, firstId: 200), nextMaxId: "150"),
            PageResponse(PageOf(count: 8, firstId: 149)));

        var fetch = await NewReader(network).Read(Profile, Timeline.Home, 40, TestContext.Current.CancellationToken);

        Assert.Equal(20, fetch.Posts.Count);
        Assert.True(fetch.IsComplete);
        Assert.Equal(2, network.Requests.Count);
        Assert.Equal(
            "https://mastodon.social/api/v1/timelines/home?max_id=150&limit=28",
            network.Requests[1].RequestUri?.ToString());
    }

    /// <summary>
    ///     The one thing that has to stop the loop whatever the instance claims is left: a page with nothing on it
    ///     cannot be followed by a further ask that does better.
    /// </summary>
    [Fact]
    public async Task Read_StopsOnAnEmptyPageEvenWhereTheInstanceNamesAnotherOne()
    {
        var network = new ScriptedHttpMessageHandler(PageResponse("[]", nextMaxId: "150"));

        var fetch = await NewReader(network).Read(Profile, Timeline.Home, 40, TestContext.Current.CancellationToken);

        Assert.Empty(fetch.Posts);
        Assert.True(fetch.IsComplete);
        Assert.Single(network.Requests);
    }

    /// <summary>
    ///     The ticket's point about paging: a limit hit part way through loses none of what already arrived, and the
    ///     fetch says it was cut short so nobody reports a quiet timeline the user does not have.
    /// </summary>
    [Fact]
    public async Task Read_StopsOnARateLimitAndKeepsThePostsItAlreadyHad()
    {
        var network = new ScriptedHttpMessageHandler(
            PageResponse(PageOf(count: 40, firstId: 200), nextMaxId: "161"),
            ScriptedHttpMessageHandler.Status(HttpStatusCode.TooManyRequests));

        var fetch = await NewReader(network).Read(Profile, Timeline.Home, 45, TestContext.Current.CancellationToken);

        Assert.Equal(40, fetch.Posts.Count);
        Assert.False(fetch.IsComplete);
        Assert.Equal("mastodon.social", fetch.StoppedBy?.Instance);

        // Fail fast: the limit is not waited out and the page it refused is not asked for again.
        Assert.Equal(2, network.Requests.Count);
    }

    /// <summary>The same thing on the first page: no posts, but emphatically not an empty timeline.</summary>
    [Fact]
    public async Task Read_ReportsARateLimitOnTheFirstPageAsAFetchThatNeverGotGoing()
    {
        var network = new ScriptedHttpMessageHandler(ScriptedHttpMessageHandler.Status(HttpStatusCode.TooManyRequests));

        var fetch = await NewReader(network).Read(Profile, Timeline.Home, 20, TestContext.Current.CancellationToken);

        Assert.Empty(fetch.Posts);
        Assert.False(fetch.IsComplete);
        Assert.NotNull(fetch.StoppedBy);
    }

    private static Timeline TimelineFor(TimelineScope scope) => scope switch
    {
        TimelineScope.Home => Timeline.Home,
        TimelineScope.Local => Timeline.Local,
        TimelineScope.Federated => Timeline.Federated,
        _ => Timeline.Tag("cats"),
    };

    /// <summary>Resolved from the container the app builds, so the wiring is under test alongside the behavior.</summary>
    private static ITimelineReader NewReader(HttpMessageHandler network)
    {
        var services = new ServiceCollection();
        services.AddWoolyCore();
        services.AddHttpClient(WoolyClient.HttpClientName).ConfigurePrimaryHttpMessageHandler(() => network);

        return services.BuildServiceProvider().GetRequiredService<ITimelineReader>();
    }

    private static string Page(params string[] posts) => $"[{string.Join(",", posts)}]";

    /// <summary>A page of <paramref name="count" /> posts with descending ids, the way a timeline comes back.</summary>
    private static string PageOf(int count, int firstId) =>
        Page(Enumerable.Range(0, count).Select(offset => PostJson($"{firstId - offset}")).ToArray());

    /// <param name="nextMaxId">
    ///     What the instance names as the start of the next page, in the link header it sends alongside a timeline —
    ///     or <see langword="null" /> for an instance that sends none.
    /// </param>
    private static Func<HttpRequestMessage, HttpResponseMessage> PageResponse(string json, string? nextMaxId = null) =>
        _ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json"),
            };

            if (nextMaxId is not null)
            {
                response.Headers.Add(
                    "Link",
                    $"<https://mastodon.social/api/v1/timelines/home?max_id={nextMaxId}>; rel=\"next\"");
            }

            return response;
        };

    /// <param name="account">
    ///     The wire's <c>acct</c>: bare for an account on the instance being read, <c>username@instance</c> for one
    ///     anywhere else.
    /// </param>
    /// <summary>One attachment, as the wire serves one back on a post.</summary>
    private static string MediaJson(
        string id = "m1",
        string type = "image",
        string? description = "A cartoon sheep") =>
        $$"""
          [{
            "id": "{{id}}",
            "type": "{{type}}",
            "url": "https://files.mastodon.social/{{id}}/original.png",
            "preview_url": "https://files.mastodon.social/{{id}}/small.png",
            "description": "{{description}}"
          }]
          """;

    /// <param name="marks">
    ///     What the instance said this profile has already done to the post, as the wire spells the three flags — or
    ///     <see langword="null" /> for an instance that sent none of them.
    /// </param>
    /// <param name="media">The wire's <c>media_attachments</c> array, or <see langword="null" /> for a post carrying none.</param>
    private static string PostJson(
        string id,
        string account = "jeff",
        string content = "<p>Hello world</p>",
        string? contentWarning = null,
        string visibility = "public",
        string? boosting = null,
        string? marks = null,
        string? media = null) =>
        $$"""
          {
            "id": "{{id}}",
            {{marks ?? string.Empty}}
            "media_attachments": {{media ?? "[]"}},
            "uri": "https://mastodon.social/users/jeff/statuses/{{id}}",
            "url": "https://mastodon.social/@jeff/{{id}}",
            "created_at": "2026-07-29T12:00:00.000Z",
            "account": {
              "id": "1",
              "username": "{{account.Split('@')[0]}}",
              "acct": "{{account}}",
              "display_name": "{{char.ToUpperInvariant(account[0]) + account.Split('@')[0][1..]}}"
            },
            "content": "{{content}}",
            "spoiler_text": "{{contentWarning}}",
            "visibility": "{{visibility}}",
            "reblogs_count": 3,
            "favourites_count": 5,
            "replies_count": 1,
            "reblog": {{boosting ?? "null"}}
          }
          """;
}
