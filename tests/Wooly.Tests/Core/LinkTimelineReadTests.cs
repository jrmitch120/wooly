using System.Net;
using System.Net.Http;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Wooly.Core;
using Wooly.Core.Errors;
using Wooly.Core.Profiles;
using Wooly.Core.Timelines;
using Wooly.Tests.Fakes;

namespace Wooly.Tests.Core;

/// <summary>
///     The link timeline — the posts sharing a <b>Trending link</b> (#603) — as the reader reads it, at the
///     <see cref="HttpMessageHandler" /> seam the timeline reader's other tests use. Mastonet 3.1.3 has no link timeline,
///     so the ask is raw, and it is the request itself that is asserted on: the address as a query value, paging by
///     <c>max_id</c>, and the 404 an instance answers a link that is not trending with.
/// </summary>
public class LinkTimelineReadTests
{
    private static readonly ActiveProfile Profile = new()
    {
        Name = "personal",
        Instance = "mastodon.social",
        Account = "jeff@mastodon.social",
        AccessToken = "token-personal",
    };

    private static readonly CancellationToken Token = TestContext.Current.CancellationToken;

    private static readonly Uri Article = new("https://example.com/sheep?breed=merino");

    /// <summary>
    ///     The address goes as a query value, escaped whole, so that its own <c>?</c> and <c>=</c> are part of the
    ///     address rather than more parameters for the instance to read.
    /// </summary>
    [Fact]
    public async Task Read_AsksTheInstanceForThePostsSharingTheLink()
    {
        var network = new ScriptedHttpMessageHandler(ScriptedHttpMessageHandler.Json(Page(PostJson("110"))));

        var fetch = await NewReader(network).Read(Profile, Timeline.Sharing(Article), 20, Token);

        var request = Assert.Single(network.Requests);
        Assert.Equal(
            "https://mastodon.social/api/v1/timelines/link?url=https%3A%2F%2Fexample.com%2Fsheep%3Fbreed%3Dmerino&limit=20",
            request.RequestUri?.OriginalString);
        Assert.Equal("Bearer token-personal", request.Headers.Authorization?.ToString());
        Assert.Equal("110", Assert.Single(fetch.Items).Id);
        Assert.True(fetch.IsWhole);
    }

    /// <summary>
    ///     More than one page's worth pages by <c>max_id</c>, as every other feed does (ADR-0007): from where the
    ///     instance's link header says the next page starts.
    /// </summary>
    [Fact]
    public async Task Read_PagesOnByMaxIdFromWhereTheInstanceSaidTheNextPageStarts()
    {
        var network = new ScriptedHttpMessageHandler(
            PageResponse(PageOf(40, firstId: 200), nextMaxId: "161"),
            PageResponse(PageOf(5, firstId: 160)));

        var fetch = await NewReader(network).Read(Profile, Timeline.Sharing(Article), 50, Token);

        Assert.Equal(45, fetch.Items.Count);
        Assert.True(fetch.IsWhole);
        Assert.Equal(
            "https://mastodon.social/api/v1/timelines/link?url=https%3A%2F%2Fexample.com%2Fsheep%3Fbreed%3Dmerino&limit=10&max_id=161",
            network.Requests[1].RequestUri?.OriginalString);
    }

    /// <summary>Reading on starts at the page an earlier read said it stopped before, rather than at the top.</summary>
    [Fact]
    public async Task ReadOn_AsksForThePageAfterWhereTheLastReadStopped()
    {
        var network = new ScriptedHttpMessageHandler(
            PageResponse(PageOf(20, firstId: 200), nextMaxId: "181"),
            PageResponse(PageOf(20, firstId: 180), nextMaxId: "161"));
        var reader = NewReader(network);

        var first = await reader.Read(Profile, Timeline.Sharing(Article), 20, Token);
        var next = await reader.ReadOn(Profile, Timeline.Sharing(Article), first.Onward!, 20, Token);

        Assert.Equal("180", next.Items[0].Id);
        Assert.Equal(
            "https://mastodon.social/api/v1/timelines/link?url=https%3A%2F%2Fexample.com%2Fsheep%3Fbreed%3Dmerino&limit=20&max_id=181",
            network.Requests[1].RequestUri?.OriginalString);
    }

    /// <summary>
    ///     An instance keeps the posts sharing a link only while it trends, and answers any other with a 404. That is
    ///     not found, said in the user's words and naming the link — never an empty timeline, which is what a trending
    ///     link nobody has posted about yet reads as.
    /// </summary>
    [Fact]
    public async Task Read_ReportsALinkTheInstanceDoesNotHaveTrendingAsNotTrending()
    {
        var network = new ScriptedHttpMessageHandler(ScriptedHttpMessageHandler.Status(HttpStatusCode.NotFound));

        var refusal = await Assert.ThrowsAsync<LinkNotTrendingException>(() =>
            NewReader(network).Read(Profile, Timeline.Sharing(Article), 20, Token));

        Assert.Equal(Article, refusal.Link);
        Assert.Contains("https://example.com/sheep?breed=merino", refusal.Message);
        Assert.Contains("mastodon.social", refusal.Message);
    }

    /// <summary>A trending link nobody has posted about yet is a timeline with nothing on it, not a failure.</summary>
    [Fact]
    public async Task Read_ReportsATrendingLinkNobodySharedAsAnEmptyTimeline()
    {
        var network = new ScriptedHttpMessageHandler(ScriptedHttpMessageHandler.Json(Page()));

        var fetch = await NewReader(network).Read(Profile, Timeline.Sharing(Article), 20, Token);

        Assert.Empty(fetch.Items);
        Assert.True(fetch.IsWhole);
    }

    /// <summary>
    ///     A link that stops trending part way through a read has ended, and what was already read is kept: the posts
    ///     arrived, and losing them to a later page's 404 would cost the reader what they had.
    /// </summary>
    [Fact]
    public async Task Read_EndsWithWhatArrivedWhenTheLinkStopsTrendingPartWay()
    {
        var network = new ScriptedHttpMessageHandler(
            PageResponse(PageOf(40, firstId: 200), nextMaxId: "161"),
            ScriptedHttpMessageHandler.Status(HttpStatusCode.NotFound));

        var fetch = await NewReader(network).Read(Profile, Timeline.Sharing(Article), 50, Token);

        Assert.Equal(40, fetch.Items.Count);
        Assert.True(fetch.IsWhole);
    }

    /// <summary>
    ///     Reading on through a link that has since stopped trending simply ends it, rather than failing (#598's
    ///     fourteenth story): what is on screen stays, and there is nothing behind it to read on to.
    /// </summary>
    [Fact]
    public async Task ReadOn_EndsTheTimelineWhenTheLinkHasStoppedTrending()
    {
        var network = new ScriptedHttpMessageHandler(
            PageResponse(PageOf(20, firstId: 200), nextMaxId: "181"),
            ScriptedHttpMessageHandler.Status(HttpStatusCode.NotFound));
        var reader = NewReader(network);

        var first = await reader.Read(Profile, Timeline.Sharing(Article), 20, Token);
        var next = await reader.ReadOn(Profile, Timeline.Sharing(Article), first.Onward!, 20, Token);

        Assert.Empty(next.Items);
        Assert.True(next.IsWhole);
        Assert.Null(next.Onward);
    }

    /// <summary>Any other refusal is not a link that stopped trending, and fails as a refusal does elsewhere.</summary>
    [Fact]
    public async Task Read_LetsAnyOtherRefusalThrough()
    {
        var network = new ScriptedHttpMessageHandler(ScriptedHttpMessageHandler.Status(HttpStatusCode.Forbidden));

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            NewReader(network).Read(Profile, Timeline.Sharing(Article), 20, Token));
    }

    /// <summary>
    ///     The domain's own guard, as a tag timeline has one: a caller is expected to have turned such an address down
    ///     already, so reaching here with one is a defect — but never something to send an instance.
    /// </summary>
    [Fact]
    public void Sharing_RefusesAnAddressThatIsNotAWebPage()
    {
        var exception = Assert.Throws<ArgumentException>(() => Timeline.Sharing(new Uri("file:///etc/passwd")));

        Assert.Contains("file:///etc/passwd", exception.Message);
    }

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
        Page([.. Enumerable.Range(0, count).Select(offset => PostJson($"{firstId - offset}"))]);

    /// <param name="nextMaxId">
    ///     What the instance names as the start of the next page in its link header, or <see langword="null" /> for
    ///     an instance that sends none.
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
                    $"<https://mastodon.social/api/v1/timelines/link?url=x&max_id={nextMaxId}>; rel=\"next\"");
            }

            return response;
        };

    private static string PostJson(string id) =>
        $$"""
          {
            "id": "{{id}}",
            "uri": "https://mastodon.social/users/jeff/statuses/{{id}}",
            "url": "https://mastodon.social/@jeff/{{id}}",
            "created_at": "2026-07-29T12:00:00.000Z",
            "account": {
              "id": "1",
              "username": "jeff",
              "acct": "jeff",
              "display_name": "Jeff"
            },
            "content": "<p>Hello world</p>",
            "spoiler_text": "",
            "visibility": "public",
            "reblogs_count": 3,
            "favourites_count": 5,
            "replies_count": 1,
            "reblog": null
          }
          """;
}
