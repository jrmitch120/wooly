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
///     A <b>Tag set</b>'s timeline as the reader reads it (ADR-0033), at the <see cref="HttpMessageHandler" /> seam the
///     timeline reader's other tests use: what is asked of the instance, and the posts handed back. Mastonet has no
///     <c>any[]</c>, so the ask is raw — which is why it is the request itself that is asserted on.
/// </summary>
public class TagSetReadTests
{
    private static readonly ActiveProfile Profile = new()
    {
        Name = "personal",
        Instance = "mastodon.social",
        Account = "jeff@mastodon.social",
        AccessToken = "token-personal",
    };

    private static readonly CancellationToken Token = TestContext.Current.CancellationToken;

    [Fact]
    public async Task Read_AsksOnceForEveryTagOfASet_TheFirstInThePathAndTheRestAsAny()
    {
        var network = new ScriptedHttpMessageHandler(ScriptedHttpMessageHandler.Json(Page(PostJson("110"))));
        var dotnet = TagSet.Of("dotnet", ["dotnet", "csharp", "fsharp"]);

        var fetch = await NewReader(network).Read(Profile, Timeline.Of(dotnet), 20, Token);

        var request = Assert.Single(network.Requests);
        Assert.Equal(
            "https://mastodon.social/api/v1/timelines/tag/dotnet?limit=20&any[]=csharp&any[]=fsharp",
            request.RequestUri?.OriginalString);
        Assert.Equal("Bearer token-personal", request.Headers.Authorization?.ToString());
        Assert.Equal("110", Assert.Single(fetch.Items).Id);
    }

    [Fact]
    public async Task Read_AsksForASetOfOneTagWithNoAny()
    {
        var network = new ScriptedHttpMessageHandler(ScriptedHttpMessageHandler.Json(Page(PostJson("110"))));

        await NewReader(network).Read(Profile, Timeline.Of(TagSet.Of("cats", ["cats"])), 20, Token);

        Assert.Equal(
            "https://mastodon.social/api/v1/timelines/tag/cats?limit=20",
            Assert.Single(network.Requests).RequestUri?.OriginalString);
    }

    [Fact]
    public async Task Read_AsksForAllFourTagsOfAFullSetInOneAsk()
    {
        var network = new ScriptedHttpMessageHandler(ScriptedHttpMessageHandler.Json(Page(PostJson("110"))));
        var set = TagSet.Of("pets", ["cats", "dogs", "rabbits", "ferrets"]);

        await NewReader(network).Read(Profile, Timeline.Of(set), 20, Token);

        Assert.Equal(
            "https://mastodon.social/api/v1/timelines/tag/cats?limit=20&any[]=dogs&any[]=rabbits&any[]=ferrets",
            Assert.Single(network.Requests).RequestUri?.OriginalString);
    }

    /// <summary>Newest first, as the instance answered, and paged on as every timeline is.</summary>
    [Fact]
    public async Task ReadOn_AsksForThePageTheLastReadNamedNext_KeepingTheSetsTags()
    {
        var network = new ScriptedHttpMessageHandler(
            PageResponse(Page(PostJson("200"), PostJson("199")), nextMaxId: "199"),
            PageResponse(Page(PostJson("150"))));
        var reader = NewReader(network);
        var set = Timeline.Of(TagSet.Of("dotnet", ["dotnet", "csharp"]));

        var first = await reader.Read(Profile, set, 2, Token);
        var onward = await reader.ReadOn(Profile, set, first.Onward!, 2, Token);

        Assert.Equal(["200", "199"], first.Items.Select(post => post.Id));
        Assert.False(first.IsWhole);
        Assert.Equal(
            "https://mastodon.social/api/v1/timelines/tag/dotnet?limit=2&max_id=199&any[]=csharp",
            network.Requests[1].RequestUri?.OriginalString);
        Assert.Equal("150", Assert.Single(onward.Items).Id);
        Assert.True(onward.IsWhole);
    }

    [Fact]
    public async Task Read_FailsAsAnyReadDoesWhenTheInstanceRefuses()
    {
        var network = new ScriptedHttpMessageHandler(
            ScriptedHttpMessageHandler.Status(HttpStatusCode.InternalServerError));

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            NewReader(network).Read(Profile, Timeline.Of(TagSet.Of("cats", ["cats"])), 20, Token));
    }

    [Fact]
    public async Task Read_StopsShortOnARateLimit_AsEveryTimelineDoes()
    {
        var network = new ScriptedHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.TooManyRequests)
            {
                Content = new StringContent("""{"error":"Too many requests"}""", Encoding.UTF8, "application/json"),
            });

        var fetch = await NewReader(network).Read(Profile, Timeline.Of(TagSet.Of("cats", ["cats"])), 20, Token);

        Assert.Empty(fetch.Items);
        Assert.IsType<RateLimitedException>(fetch.StoppedBy);
    }

    /// <summary>
    ///     Resolved from the container the app builds, so the wiring is under test alongside the behavior.
    /// </summary>
    private static ITimelineReader NewReader(HttpMessageHandler network)
    {
        var services = new ServiceCollection();
        services.AddWoolyCore();
        services.AddHttpClient(WoolyClient.HttpClientName).ConfigurePrimaryHttpMessageHandler(() => network);

        return services.BuildServiceProvider().GetRequiredService<ITimelineReader>();
    }

    private static string Page(params string[] posts) => $"[{string.Join(",", posts)}]";

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
                    $"<https://mastodon.social/api/v1/timelines/tag/dotnet?max_id={nextMaxId}>; rel=\"next\"");
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
