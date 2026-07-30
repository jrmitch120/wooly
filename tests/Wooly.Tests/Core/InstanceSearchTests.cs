using System.Net;
using System.Net.Http;
using Microsoft.Extensions.DependencyInjection;
using Wooly.Core;
using Wooly.Core.Errors;
using Wooly.Core.Profiles;
using Wooly.Core.Search;
using Wooly.Tests.Fakes;

namespace Wooly.Tests.Core;

/// <summary>
///     The search is the adapter behind <see cref="IInstanceSearch" />, and ADR-0005 puts an adapter's tests at the
///     <see cref="HttpMessageHandler" /> seam: what it does is turn the three lists an instance answers with into
///     accounts, hashtags and posts, so there has to be a payload for the mapping to be observable at all. What it
///     asks the instance for — one call, resolving what the instance has not met — is only observable here too.
///     Commands above this fake <see cref="IInstanceSearch" /> instead.
/// </summary>
public class InstanceSearchTests
{
    private static readonly ActiveProfile Profile = new()
    {
        Name = "personal",
        Instance = "mastodon.social",
        Account = "jeff@mastodon.social",
        AccessToken = "token-personal",
    };

    [Fact]
    public async Task Find_ReportsTheAccountsHashtagsAndPostsTheInstanceFound()
    {
        var network = new ScriptedHttpMessageHandler(ScriptedHttpMessageHandler.Json(Found()));

        var results = await Search(network, SearchQuery.For("cats"));

        var account = Assert.Single(results.Accounts!);
        Assert.Equal("alice@hachyderm.io", account.Address);
        Assert.Equal("Alice", account.Author);
        Assert.Equal(1203, account.Followers);
        Assert.Equal(187, account.Following);
        Assert.Equal(4210, account.Posts);
        Assert.Equal("https://hachyderm.io/@alice", account.Url);

        var hashtag = Assert.Single(results.Hashtags!);
        Assert.Equal("cats", hashtag.Name);
        Assert.Equal("https://mastodon.social/tags/cats", hashtag.Url);

        var post = Assert.Single(results.Posts!);
        Assert.Equal("110", post.Id);
        Assert.Equal("Hello world", post.Content);
        Assert.Equal(3, post.Boosts);
    }

    /// <summary>
    ///     One call, whatever is being looked for, and one that asks the instance to go and fetch what it has not met:
    ///     a user who pastes the address of a post they can see in a browser means "find me this", and an instance
    ///     that has never seen it answers with nothing at all unless asked to look.
    /// </summary>
    [Fact]
    public async Task Find_AsksTheInstanceOnceAndHasItResolveWhatItHasNotMet()
    {
        var network = new ScriptedHttpMessageHandler(ScriptedHttpMessageHandler.Json(Found()));

        await Search(network, SearchQuery.For("cats", SearchKind.Accounts));

        var request = Assert.Single(network.Requests);
        Assert.Equal("https://mastodon.social/api/v2/search?q=cats&resolve=true", request.RequestUri?.ToString());
        Assert.Equal("Bearer token-personal", request.Headers.Authorization?.ToString());
    }

    /// <summary>
    ///     The instance answers with all three kinds however it is asked (ADR-0011), so the narrowing happens here —
    ///     and what was not asked for is absent rather than empty, which is what a caller tells "you did not ask" from
    ///     "there were none" by.
    /// </summary>
    [Theory]
    [InlineData(SearchKind.Accounts, true, false, false)]
    [InlineData(SearchKind.Hashtags, false, true, false)]
    [InlineData(SearchKind.Posts, false, false, true)]
    [InlineData(SearchKind.Everything, true, true, true)]
    public async Task Find_KeepsOnlyTheKindOfResultItWasAskedFor(
        SearchKind kind,
        bool accounts,
        bool hashtags,
        bool posts)
    {
        var network = new ScriptedHttpMessageHandler(ScriptedHttpMessageHandler.Json(Found()));

        var results = await Search(network, SearchQuery.For("cats", kind));

        Assert.Equal(accounts, results.Accounts is not null);
        Assert.Equal(hashtags, results.Hashtags is not null);
        Assert.Equal(posts, results.Posts is not null);
    }

    /// <summary>
    ///     An account on another instance is named by the wire as <c>username@instance</c> and one of this instance's
    ///     own by bare username. A list of results mixes the two, and two lines side by side may not say who they are
    ///     about in two different ways.
    /// </summary>
    [Fact]
    public async Task Find_NamesEveryAccountByTheInstanceItIsOn()
    {
        var network = new ScriptedHttpMessageHandler(
            ScriptedHttpMessageHandler.Json(Found(accounts: [AccountJson("alice@hachyderm.io"), AccountJson("jeff")])));

        var results = await Search(network, SearchQuery.For("cats"));

        Assert.Equal(["alice@hachyderm.io", "jeff@mastodon.social"], results.Accounts!.Select(account => account.Address));
    }

    /// <summary>
    ///     Which of several near-identical tags is worth reading is the one people are posting to, so the daily usage
    ///     an instance sends is added up rather than dropped. Mastodon writes those counts as strings.
    /// </summary>
    [Fact]
    public async Task Find_AddsUpTheUsageTheInstanceReportedForAHashtag()
    {
        var network = new ScriptedHttpMessageHandler(
            ScriptedHttpMessageHandler.Json(Found(hashtags: [
                HashtagJson("cats", history: """[{"day":"1","uses":"12","accounts":"9"},{"day":"2","uses":"30","accounts":"21"}]"""),
            ])));

        var results = await Search(network, SearchQuery.For("cats"));

        var hashtag = Assert.Single(results.Hashtags!);
        Assert.Equal(42, hashtag.RecentPosts);
        Assert.Equal(30, hashtag.RecentAccounts);
    }

    /// <summary>Usage is the least of what a hashtag result is for, so an instance that sends none still finds tags.</summary>
    [Fact]
    public async Task Find_ReportsAHashtagTheInstanceSentNoUsageFor()
    {
        var network = new ScriptedHttpMessageHandler(
            ScriptedHttpMessageHandler.Json(Found(hashtags: [HashtagJson("cats", history: null)])));

        var results = await Search(network, SearchQuery.For("cats"));

        var hashtag = Assert.Single(results.Hashtags!);
        Assert.Equal("cats", hashtag.Name);
        Assert.Equal(0, hashtag.RecentPosts);
        Assert.Equal(0, hashtag.RecentAccounts);
    }

    /// <summary>
    ///     A tag comes back bare and stays bare, which is what makes one a search turned up something
    ///     <c>timeline tag</c> will take.
    /// </summary>
    [Fact]
    public async Task Find_ReportsAHashtagTheWayTheTagTimelineTakesOne()
    {
        var network = new ScriptedHttpMessageHandler(
            ScriptedHttpMessageHandler.Json(Found(hashtags: [HashtagJson("#cats")])));

        var results = await Search(network, SearchQuery.For("cats"));

        Assert.Equal("cats", Assert.Single(results.Hashtags!).Name);
    }

    /// <summary>An instance that found none of a kind may leave it out rather than send an empty list.</summary>
    [Fact]
    public async Task Find_ReadsAKindTheInstanceLeftOutAsNoneFound()
    {
        var network = new ScriptedHttpMessageHandler(ScriptedHttpMessageHandler.Json("""{"accounts":[]}"""));

        var results = await Search(network, SearchQuery.For("cats"));

        Assert.Empty(results.Accounts!);
        Assert.Empty(results.Hashtags!);
        Assert.Empty(results.Posts!);
    }

    /// <summary>
    ///     A search is one call, so there is no half-answer to hand back the way a paged read has: the limit is raised,
    ///     and ADR-0006's one handler turns it into the message and the exit code.
    /// </summary>
    [Fact]
    public async Task Find_RaisesTheRateLimitTheInstanceAnsweredWith()
    {
        var network = new ScriptedHttpMessageHandler(ScriptedHttpMessageHandler.Status(HttpStatusCode.TooManyRequests));

        var refusal = await Assert.ThrowsAsync<RateLimitedException>(
            () => Search(network, SearchQuery.For("cats")));

        Assert.Equal("mastodon.social", refusal.Instance);
        Assert.Single(network.Requests);
    }

    /// <summary>Resolved from the container the app builds, so the wiring is under test alongside the behavior.</summary>
    private static Task<SearchResults> Search(HttpMessageHandler network, SearchQuery query)
    {
        var services = new ServiceCollection();
        services.AddWoolyCore();
        services.AddHttpClient(WoolyClient.HttpClientName).ConfigurePrimaryHttpMessageHandler(() => network);

        return services.BuildServiceProvider()
                       .GetRequiredService<IInstanceSearch>()
                       .Find(Profile, query, TestContext.Current.CancellationToken);
    }

    private static string Found(string[]? accounts = null, string[]? hashtags = null, string[]? posts = null) =>
        $$"""
          {
            "accounts": [{{string.Join(",", accounts ?? [AccountJson("alice@hachyderm.io")])}}],
            "statuses": [{{string.Join(",", posts ?? [PostJson("110")])}}],
            "hashtags": [{{string.Join(",", hashtags ?? [HashtagJson("cats")])}}]
          }
          """;

    /// <param name="account">
    ///     The wire's <c>acct</c>: bare for an account on the instance being searched, <c>username@instance</c> for one
    ///     anywhere else.
    /// </param>
    private static string AccountJson(string account) =>
        $$"""
          {
            "id": "1",
            "username": "{{account.Split('@')[0]}}",
            "acct": "{{account}}",
            "display_name": "{{char.ToUpperInvariant(account[0]) + account.Split('@')[0][1..]}}",
            "url": "https://{{(account.Contains('@') ? account.Split('@')[1] : "mastodon.social")}}/@{{account.Split('@')[0]}}",
            "created_at": "2020-01-01T00:00:00.000Z",
            "followers_count": 1203,
            "following_count": 187,
            "statuses_count": 4210
          }
          """;

    private static string HashtagJson(string name, string? history = "[]") =>
        $$"""
          {
            "name": "{{name}}",
            "url": "https://mastodon.social/tags/{{name.TrimStart('#')}}",
            "history": {{history ?? "null"}}
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
