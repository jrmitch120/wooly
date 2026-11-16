using System.Net;
using System.Net.Http;
using Microsoft.Extensions.DependencyInjection;
using Wooly.Core;
using Wooly.Core.Errors;
using Wooly.Core.Profiles;
using Wooly.Core.Trends;
using Wooly.Tests.Fakes;

namespace Wooly.Tests.Core;

/// <summary>
///     The trends are the adapter behind <see cref="ITrends" />, and ADR-0005 puts an adapter's tests at the
///     <see cref="HttpMessageHandler" /> seam: what it does is ask three endpoints for a page each and turn what they
///     answer with into hashtags, posts and trending links, so a payload is what makes either observable. Commands
///     above this fake <see cref="ITrends" /> instead.
/// </summary>
public class InstanceTrendsTests
{
    private static readonly ActiveProfile Profile = new()
    {
        Name = "personal",
        Instance = "mastodon.social",
        Account = "jeff@mastodon.social",
        AccessToken = "token-personal",
    };

    /// <summary>
    ///     A trending hashtag is a hashtag, carrying the usage a searched one carries — every day the instance sent,
    ///     added up — and bare, so <c>timeline tag</c> takes it as it comes.
    /// </summary>
    [Fact]
    public async Task Hashtags_ReadsTheTrendingHashtagsAndTheirUsage()
    {
        var network = new ScriptedHttpMessageHandler(ScriptedHttpMessageHandler.Json(
            $"[{HashtagJson("cats")},{HashtagJson("dogs")}]"));

        var hashtags = await Resolved(network).Hashtags(Profile, TrendPage.First, TestContext.Current.CancellationToken);

        Assert.Equal(["cats", "dogs"], hashtags.Select(hashtag => hashtag.Name));
        Assert.Equal(12 + 30, hashtags[0].RecentPosts);
        Assert.Equal(5 + 9, hashtags[0].RecentAccounts);
        Assert.Equal("https://mastodon.social/tags/cats", hashtags[0].Url);
    }

    /// <summary>
    ///     A first page asked with nothing said about its size is asked with nothing in the query at all, so the
    ///     instance serves its own default — which is what <c>trends</c> with no <c>--limit</c> means.
    /// </summary>
    [Fact]
    public async Task Hashtags_AsksTheInstanceForItsDefaultPageWhereNoneWasNamed()
    {
        var network = new ScriptedHttpMessageHandler(ScriptedHttpMessageHandler.Json("[]"));

        await Resolved(network).Hashtags(Profile, TrendPage.First, TestContext.Current.CancellationToken);

        var request = Assert.Single(network.Requests);
        Assert.Equal("https://mastodon.social/api/v1/trends/tags", request.RequestUri?.ToString());
        Assert.Equal("Bearer token-personal", request.Headers.Authorization?.ToString());
    }

    /// <summary>
    ///     Every trends endpoint pages by offset, which is the part Mastonet cannot read and the reason this adapter is
    ///     raw: the page goes to the instance as asked, for each of the three.
    /// </summary>
    [Fact]
    public async Task EachKind_SendsThePageItWasAskedFor()
    {
        var network = new ScriptedHttpMessageHandler(ScriptedHttpMessageHandler.Json("[]"));
        var trends = Resolved(network);
        var cancellationToken = TestContext.Current.CancellationToken;

        await trends.Hashtags(Profile, TrendPage.Of(20, offset: 40), cancellationToken);
        await trends.Posts(Profile, TrendPage.Of(3), cancellationToken);
        await trends.Links(Profile, TrendPage.Of(10, offset: 10), cancellationToken);

        Assert.Equal(
            [
                "https://mastodon.social/api/v1/trends/tags?limit=20&offset=40",
                "https://mastodon.social/api/v1/trends/statuses?limit=3",
                "https://mastodon.social/api/v1/trends/links?limit=10&offset=10",
            ],
            network.Requests.Select(request => request.RequestUri?.ToString()));
    }

    /// <summary>A trending post is a post, crossing through the one mapping every post does.</summary>
    [Fact]
    public async Task Posts_ReadsTheTrendingPostsAsPosts()
    {
        var network = new ScriptedHttpMessageHandler(ScriptedHttpMessageHandler.Json($"[{PostJson("110")}]"));

        var posts = await Resolved(network).Posts(Profile, TrendPage.First, TestContext.Current.CancellationToken);

        var post = Assert.Single(posts);
        Assert.Equal("110", post.Id);
        Assert.Equal("Hello world", post.Content);
        Assert.Equal(3, post.Boosts);
        Assert.Equal(
            "https://mastodon.social/api/v1/trends/statuses",
            Assert.Single(network.Requests).RequestUri?.ToString());
    }

    /// <summary>
    ///     A trending link is the new value: where it points, what it is called and what it says of itself, whose site
    ///     it is on, and how many people shared it over the days the instance reported — added up as a hashtag's usage
    ///     is, so the two read alike under the search prompt.
    /// </summary>
    [Fact]
    public async Task Links_ReadsTheTrendingLinksAndHowManyAreSharingThem()
    {
        var network = new ScriptedHttpMessageHandler(ScriptedHttpMessageHandler.Json($"[{LinkJson()}]"));

        var links = await Resolved(network).Links(Profile, TrendPage.First, TestContext.Current.CancellationToken);

        var link = Assert.Single(links);
        Assert.Equal("https://news.example/cats-learn-to-type", link.Url);
        Assert.Equal("Cats learn to type", link.Title);
        Assert.Equal("A study of keyboards and the cats who sit on them.", link.Description);
        Assert.Equal("Example News", link.Site);
        Assert.Equal(40 + 25, link.PeopleSharing);
        Assert.Equal(
            "https://mastodon.social/api/v1/trends/links",
            Assert.Single(network.Requests).RequestUri?.ToString());
    }

    /// <summary>
    ///     A link the instance said little about is still a link to read: a missing description or site is absent
    ///     rather than a failure, and usage it did not send — or sent in a form this client cannot read — is nobody.
    /// </summary>
    [Fact]
    public async Task Links_ReadsALinkTheInstanceSaidLittleAbout()
    {
        var network = new ScriptedHttpMessageHandler(ScriptedHttpMessageHandler.Json(
            """
            [{
              "url": "https://news.example/a",
              "title": "A",
              "description": "",
              "provider_name": "",
              "history": [{ "day": "1", "uses": "1", "accounts": "lots" }]
            }]
            """));

        var link = Assert.Single(
            await Resolved(network).Links(Profile, TrendPage.First, TestContext.Current.CancellationToken));

        Assert.Null(link.Description);
        Assert.Null(link.Site);
        Assert.Equal(0, link.PeopleSharing);
    }

    /// <summary>A card with no address is nothing anybody can open or read the posts sharing, so it is not a link.</summary>
    [Fact]
    public async Task Links_DropsACardWithNoAddress()
    {
        var network = new ScriptedHttpMessageHandler(ScriptedHttpMessageHandler.Json(
            $$"""[{ "url": "", "title": "Nowhere" }, {{LinkJson()}}]"""));

        var links = await Resolved(network).Links(Profile, TrendPage.First, TestContext.Current.CancellationToken);

        Assert.Equal("Cats learn to type", Assert.Single(links).Title);
    }

    /// <summary>
    ///     An instance with trends turned off answers every kind with an empty list, and that is nothing trending —
    ///     not a failure, which is what lets an empty Search say so rather than complain.
    /// </summary>
    [Fact]
    public async Task EachKind_ReadsAnEmptyAnswerAsNothingTrending()
    {
        var network = new ScriptedHttpMessageHandler(ScriptedHttpMessageHandler.Json("[]"));
        var trends = Resolved(network);
        var cancellationToken = TestContext.Current.CancellationToken;

        Assert.Empty(await trends.Hashtags(Profile, TrendPage.First, cancellationToken));
        Assert.Empty(await trends.Posts(Profile, TrendPage.First, cancellationToken));
        Assert.Empty(await trends.Links(Profile, TrendPage.First, cancellationToken));
    }

    /// <summary>One call, so a rate limit is raised for ADR-0006's handler — or the TUI's countdown — to say.</summary>
    [Fact]
    public async Task EachKind_RaisesTheRateLimitTheInstanceAnsweredWith()
    {
        var network = new ScriptedHttpMessageHandler(ScriptedHttpMessageHandler.Status(HttpStatusCode.TooManyRequests));

        var refusal = await Assert.ThrowsAsync<RateLimitedException>(
            () => Resolved(network).Links(Profile, TrendPage.First, TestContext.Current.CancellationToken));

        Assert.Equal("mastodon.social", refusal.Instance);
    }

    /// <summary>
    ///     A refusal is raised as something a front end says, in the instance's own words: the TUI's enquiry says a
    ///     <see cref="WoolyException" /> on the status row and nothing else at all.
    /// </summary>
    [Fact]
    public async Task EachKind_RaisesARefusalInTheInstancesOwnWords()
    {
        var network = new ScriptedHttpMessageHandler(
            ScriptedHttpMessageHandler.Refusal(HttpStatusCode.Forbidden, "This method requires an authenticated user"));

        var refusal = await Assert.ThrowsAsync<TrendsRefusedException>(
            () => Resolved(network).Posts(Profile, TrendPage.First, TestContext.Current.CancellationToken));

        Assert.Contains("This method requires an authenticated user", refusal.Message);
    }

    /// <summary>
    ///     An instance that failed to answer — a <c>5xx</c> — fails as it does under every raw adapter, search and the
    ///     link timeline among them, rather than as the instance turning the read down (#598's review).
    /// </summary>
    [Fact]
    public async Task EachKind_ReportsAnInstanceThatFailedToAnswerAsAFailureRatherThanARefusal()
    {
        var network = new ScriptedHttpMessageHandler(ScriptedHttpMessageHandler.Status(HttpStatusCode.ServiceUnavailable));

        var failure = await Assert.ThrowsAsync<InstanceFailedException>(
            () => Resolved(network).Hashtags(Profile, TrendPage.First, TestContext.Current.CancellationToken));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, failure.Status);
    }

    private static ITrends Resolved(HttpMessageHandler network)
    {
        var services = new ServiceCollection();
        services.AddWoolyCore();
        services.AddHttpClient(WoolyClient.HttpClientName).ConfigurePrimaryHttpMessageHandler(() => network);

        return services.BuildServiceProvider().GetRequiredService<ITrends>();
    }

    private static string HashtagJson(string name) =>
        $$"""
          {
            "name": "{{name}}",
            "url": "https://mastodon.social/tags/{{name}}",
            "history": [
              { "day": "1762819200", "uses": "12", "accounts": "5" },
              { "day": "1762732800", "uses": "30", "accounts": "9" }
            ]
          }
          """;

    private static string LinkJson() =>
        """
        {
          "url": "https://news.example/cats-learn-to-type",
          "title": "Cats learn to type",
          "description": "A study of keyboards and the cats who sit on them.",
          "type": "link",
          "author_name": "",
          "provider_name": "Example News",
          "provider_url": "https://news.example",
          "image": null,
          "history": [
            { "day": "1762819200", "uses": "50", "accounts": "40" },
            { "day": "1762732800", "uses": "31", "accounts": "25" }
          ]
        }
        """;

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
