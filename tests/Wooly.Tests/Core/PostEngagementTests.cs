using System.Net.Http;
using Microsoft.Extensions.DependencyInjection;
using Wooly.Core;
using Wooly.Core.Posts;
using Wooly.Core.Profiles;
using Wooly.Tests.Fakes;

namespace Wooly.Tests.Core;

/// <summary>
///     The adapter behind <see cref="IPostEngagement" />, tested where ADR-0005 puts an adapter: at the
///     <see cref="HttpMessageHandler" /> seam. Which of Mastodon's six endpoints a mark turns into is only observable
///     on the wire, and so is the one piece of reading this does — a boost comes back wrapped in a post of its own, and
///     unwrapping it is what makes <c>post boost 110</c> answer about post 110. Commands above this fake the port.
/// </summary>
public class PostEngagementTests
{
    private static readonly ActiveProfile Profile = new()
    {
        Name = "personal",
        Instance = "mastodon.social",
        Account = "jeff@mastodon.social",
        AccessToken = "token-personal",
    };

    [Theory]
    [InlineData(PostMark.Boost, true, "reblog")]
    [InlineData(PostMark.Boost, false, "unreblog")]
    [InlineData(PostMark.Favorite, true, "favourite")]
    [InlineData(PostMark.Favorite, false, "unfavourite")]
    [InlineData(PostMark.Pin, true, "pin")]
    [InlineData(PostMark.Pin, false, "unpin")]
    public async Task Mark_AsksTheInstanceForTheMarkWanted(PostMark mark, bool wanted, string endpoint)
    {
        var network = Answering(StatusJson("110"));

        await NewEngagement(network).Mark(Profile, "110", mark, wanted, TestContext.Current.CancellationToken);

        var request = Assert.Single(network.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal($"https://mastodon.social/api/v1/statuses/110/{endpoint}", request.RequestUri?.ToString());
        Assert.Equal("Bearer token-personal", request.Headers.Authorization?.ToString());
    }

    /// <summary>
    ///     Boosting answers with the boost — a post of its own, with its own id, carrying the post that was boosted. A
    ///     caller asked about post 110 and has to be answered about post 110, or <c>post boost 110 --json</c> would
    ///     report an id that is not the one anything else names that post by.
    /// </summary>
    [Fact]
    public async Task Mark_ReportsThePostThatWasBoostedRatherThanTheBoostCarryingIt()
    {
        var network = Answering(BoostJson("555", StatusJson("110", boosts: 4)));

        var post = await NewEngagement(network).Mark(
            Profile,
            "110",
            PostMark.Boost,
            wanted: true,
            TestContext.Current.CancellationToken);

        Assert.Equal("110", post.Id);
        Assert.Equal(4, post.Boosts);
        Assert.False(post.IsBoost);
    }

    /// <summary>Taking a boost back answers with the post itself, which is already the post the caller asked about.</summary>
    [Fact]
    public async Task Mark_ReportsThePostItselfWhereNothingWrapsIt()
    {
        var network = Answering(StatusJson("110", boosts: 3));

        var post = await NewEngagement(network).Mark(
            Profile,
            "110",
            PostMark.Boost,
            wanted: false,
            TestContext.Current.CancellationToken);

        Assert.Equal("110", post.Id);
        Assert.Equal(3, post.Boosts);
    }

    [Fact]
    public async Task Show_ReadsThePostTheIdNames()
    {
        var network = Answering(StatusJson("110"));

        var post = await NewEngagement(network).Show(Profile, "110", TestContext.Current.CancellationToken);

        var request = Assert.Single(network.Requests);
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal("https://mastodon.social/api/v1/statuses/110", request.RequestUri?.ToString());

        Assert.Equal("110", post.Id);
        Assert.Equal("jeff@mastodon.social", post.Account);
        Assert.Equal("Hello world", post.Content);
        Assert.Equal(PostVisibility.Public, post.Visibility);
        Assert.Equal("https://mastodon.social/@jeff/110", post.Url);
    }

    /// <summary>
    ///     A boost asked for by its own id stays a boost, unlike the one <see cref="IPostEngagement.Mark" /> unwraps: a
    ///     reader who named the boost is shown the boost, the same as a timeline shows it.
    /// </summary>
    [Fact]
    public async Task Show_ShowsABoostAsABoostOfThePostItCarries()
    {
        var network = Answering(BoostJson("555", StatusJson("110")));

        var post = await NewEngagement(network).Show(Profile, "555", TestContext.Current.CancellationToken);

        Assert.Equal("555", post.Id);
        Assert.True(post.IsBoost);
        Assert.Equal("110", post.Boosted?.Id);
    }

    private static ScriptedHttpMessageHandler Answering(string json) =>
        new(ScriptedHttpMessageHandler.Json(json));

    /// <summary>Resolved from the container the app builds, so the wiring is under test alongside the behavior.</summary>
    private static IPostEngagement NewEngagement(HttpMessageHandler network)
    {
        var services = new ServiceCollection();
        services.AddWoolyCore();
        services.AddHttpClient(WoolyClient.HttpClientName).ConfigurePrimaryHttpMessageHandler(() => network);

        return services.BuildServiceProvider().GetRequiredService<IPostEngagement>();
    }

    /// <summary>What an instance answers a boost with: a post of the booster's own, carrying the post boosted.</summary>
    private static string BoostJson(string id, string boosted) => StatusJson(id, boosted: boosted);

    private static string StatusJson(string id, long boosts = 0, string? boosted = null) =>
        $$"""
          {
            "id": "{{id}}",
            "uri": "https://mastodon.social/users/jeff/statuses/{{id}}",
            "url": "https://mastodon.social/@jeff/{{id}}",
            "created_at": "2026-07-29T12:00:00.000Z",
            "account": { "id": "1", "username": "jeff", "acct": "jeff", "display_name": "Jeff" },
            "content": "<p>Hello world</p>",
            "spoiler_text": "",
            "sensitive": false,
            "visibility": "public",
            "reblogs_count": {{boosts}},
            "favourites_count": 0,
            "replies_count": 0,
            "media_attachments": [],
            "reblog": {{boosted ?? "null"}},
            "poll": null
          }
          """;
}
