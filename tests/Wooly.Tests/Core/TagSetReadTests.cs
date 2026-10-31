using System.Net;
using System.Net.Http;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Wooly.Core;
using Wooly.Core.Errors;
using Wooly.Core.Paging;
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

        var fetch = await NewReader(network).Read(Profile, Timeline.AnyOf(dotnet), 20, Token);

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

        await NewReader(network).Read(Profile, Timeline.AnyOf(TagSet.Of("cats", ["cats"])), 20, Token);

        Assert.Equal(
            "https://mastodon.social/api/v1/timelines/tag/cats?limit=20",
            Assert.Single(network.Requests).RequestUri?.OriginalString);
    }

    [Fact]
    public async Task Read_AsksForAllFourTagsOfAFullSetInOneAsk()
    {
        var network = new ScriptedHttpMessageHandler(ScriptedHttpMessageHandler.Json(Page(PostJson("110"))));
        var set = TagSet.Of("pets", ["cats", "dogs", "rabbits", "ferrets"]);

        await NewReader(network).Read(Profile, Timeline.AnyOf(set), 20, Token);

        Assert.Equal(
            "https://mastodon.social/api/v1/timelines/tag/cats?limit=20&any[]=dogs&any[]=rabbits&any[]=ferrets",
            Assert.Single(network.Requests).RequestUri?.OriginalString);
    }

    /// <summary>
    ///     Past four tags the instance would drop the rest without a word, so a set is cut into runs of four in the
    ///     order written, each its own ask (ADR-0033).
    /// </summary>
    [Fact]
    public async Task Read_AsksOnceForEveryFourTags_EachRunsFirstInThePathAndTheRestAsAny()
    {
        var network = new ScriptedHttpMessageHandler(ScriptedHttpMessageHandler.Json(Page()));

        await NewReader(network).Read(Profile, Timeline.AnyOf(TagSet.Of("many", Tags(12))), 20, Token);

        Assert.Equal(
            [
                "https://mastodon.social/api/v1/timelines/tag/t1?limit=20&any[]=t2&any[]=t3&any[]=t4",
                "https://mastodon.social/api/v1/timelines/tag/t5?limit=20&any[]=t6&any[]=t7&any[]=t8",
                "https://mastodon.social/api/v1/timelines/tag/t9?limit=20&any[]=t10&any[]=t11&any[]=t12",
            ],
            Asked(network));
    }

    /// <summary>One ask for every four tags, a short last run included, and never one per tag.</summary>
    [Theory]
    [InlineData(1, 1)]
    [InlineData(4, 1)]
    [InlineData(5, 2)]
    [InlineData(8, 2)]
    [InlineData(9, 3)]
    [InlineData(12, 3)]
    public async Task Read_SendsOneAskForEveryFourTags(int tags, int asks)
    {
        var network = new ScriptedHttpMessageHandler(ScriptedHttpMessageHandler.Json(Page()));

        await NewReader(network).Read(Profile, Timeline.AnyOf(TagSet.Of("many", Tags(tags))), 20, Token);

        Assert.Equal(asks, network.Requests.Count);
    }

    /// <summary>
    ///     The runs interleaved newest first by id, read as the number it is — <c>1200</c> ahead of <c>999</c> — and a
    ///     post that carries tags from both runs shown once.
    /// </summary>
    [Fact]
    public async Task Read_MergesTheRunsNewestFirst_APostBothRunsFoundOnce()
    {
        var network = new ScriptedHttpMessageHandler(ByRun(
            ("t1", [PageResponse(Page(PostJson("1200"), PostJson("300"), PostJson("200")))]),
            ("t5", [PageResponse(Page(PostJson("999"), PostJson("200"), PostJson("50")))])));

        var fetch = await NewReader(network).Read(Profile, Timeline.AnyOf(TagSet.Of("many", Tags(5))), 20, Token);

        Assert.Equal(["1200", "999", "300", "200", "50"], fetch.Items.Select(post => post.Id));
        Assert.True(fetch.IsWhole);
    }

    /// <summary>
    ///     What a run fetched and the merge did not hand out is kept for the read on, so the read on asks only of the
    ///     run that has nothing left — and the set is whole only once both runs are.
    /// </summary>
    [Fact]
    public async Task ReadOn_AsksOnlyTheRunsWhosePostsAreUsedUp_AndIsWholeOnlyWhenEveryRunIs()
    {
        var network = new ScriptedHttpMessageHandler(ByRun(
            ("t1", [
                PageResponse(Page(PostJson("300"), PostJson("200"), PostJson("100")), nextMaxId: "100"),
                PageResponse(Page(PostJson("90"))),
            ]),
            ("t5", [
                PageResponse(Page(PostJson("250"), PostJson("50")), nextMaxId: "50"),
                PageResponse(Page()),
            ])));
        var reader = NewReader(network);
        var set = Timeline.AnyOf(TagSet.Of("many", Tags(5)));

        var first = await reader.Read(Profile, set, 3, Token);
        var second = await reader.ReadOn(Profile, set, first.Onward!, 3, Token);
        var askedBySecond = network.Requests.Skip(2).Select(request => request.RequestUri?.OriginalString).ToList();
        var third = await reader.ReadOn(Profile, set, second.Onward!, 3, Token);

        Assert.Equal(["300", "250", "200"], first.Items.Select(post => post.Id));
        Assert.False(first.IsWhole);
        Assert.Equal(["100", "90", "50"], second.Items.Select(post => post.Id));
        Assert.Equal(
            ["https://mastodon.social/api/v1/timelines/tag/t1?limit=2&max_id=100&any[]=t2&any[]=t3&any[]=t4"],
            askedBySecond);

        // The first run is whole now, the second not yet: it named a page behind its 50.
        Assert.False(second.IsWhole);
        Assert.Equal(
            "https://mastodon.social/api/v1/timelines/tag/t5?limit=3&max_id=50",
            Assert.Single(network.Requests.Skip(3)).RequestUri?.OriginalString);
        Assert.Empty(third.Items);
        Assert.True(third.IsWhole);
    }

    /// <summary>
    ///     Where a set of several runs reads on from is compared by value, as every <c>Onward</c> is (ADR-0007's
    ///     amendment): the same answers give the same place, and a run paged to somewhere else gives another.
    /// </summary>
    [Fact]
    public async Task Read_GivesTheSamePlaceToReadOnFrom_ForTheSameAnswers()
    {
        async Task<Onward> OnwardOf(string secondRunNext)
        {
            var network = new ScriptedHttpMessageHandler(ByRun(
                ("t1", [PageResponse(Page(PostJson("300"), PostJson("200")), nextMaxId: "200")]),
                ("t5", [PageResponse(Page(PostJson("250")), nextMaxId: secondRunNext)])));

            return (await NewReader(network).Read(Profile, Timeline.AnyOf(TagSet.Of("many", Tags(5))), 2, Token)).Onward!;
        }

        var one = await OnwardOf("250");
        var other = await OnwardOf("250");
        var elsewhere = await OnwardOf("240");

        Assert.Equal(one, other);
        Assert.Equal(one.GetHashCode(), other.GetHashCode());
        Assert.NotEqual(one, elsewhere);
    }

    /// <summary>
    ///     A place in another list is not one to read a set of several runs on from, and reading from the top instead
    ///     would hand the reader again what their screen already holds: it is refused, as the defect it is.
    /// </summary>
    [Fact]
    public async Task ReadOn_RefusesAPlaceFromAnotherList()
    {
        var network = new ScriptedHttpMessageHandler(
            PageResponse(Page(PostJson("200"), PostJson("199")), nextMaxId: "199"));
        var reader = NewReader(network);
        var one = await reader.Read(Profile, Timeline.AnyOf(TagSet.Of("cats", ["cats"])), 2, Token);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            reader.ReadOn(Profile, Timeline.AnyOf(TagSet.Of("many", Tags(5))), one.Onward!, 20, Token));
    }

    /// <summary>A post handed out by one read is not handed out again when the other run reaches it on a read on.</summary>
    [Fact]
    public async Task ReadOn_LeavesOutAPostAnEarlierReadHandedOutFromTheOtherRun()
    {
        var network = new ScriptedHttpMessageHandler(ByRun(
            ("t1", [PageResponse(Page(PostJson("300"), PostJson("200")))]),
            ("t5", [
                PageResponse(Page(PostJson("250")), nextMaxId: "250"),
                PageResponse(Page(PostJson("200"), PostJson("150")), nextMaxId: "150"),
                PageResponse(Page()),
            ])));
        var reader = NewReader(network);
        var set = Timeline.AnyOf(TagSet.Of("many", Tags(5)));

        var first = await reader.Read(Profile, set, 3, Token);
        var onward = await reader.ReadOn(Profile, set, first.Onward!, 3, Token);

        Assert.Equal(["300", "250", "200"], first.Items.Select(post => post.Id));
        Assert.Equal(["150"], onward.Items.Select(post => post.Id));
        Assert.True(onward.IsWhole);
    }

    /// <summary>
    ///     A merged list with a run missing would look whole while missing posts from its middle, so one run refused is
    ///     the whole read refused, and the posts the other run answered with go nowhere (ADR-0033).
    /// </summary>
    [Fact]
    public async Task Read_FailsWhole_WhenOneRunIsRefused()
    {
        var network = new ScriptedHttpMessageHandler(ByRun(
            ("t1", [PageResponse(Page(PostJson("300")))]),
            ("t5", [ScriptedHttpMessageHandler.Status(HttpStatusCode.InternalServerError)])));

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            NewReader(network).Read(Profile, Timeline.AnyOf(TagSet.Of("many", Tags(5))), 20, Token));
    }

    /// <summary>A refresh is a read from the top again, and fails whole the same way however well the last one went.</summary>
    [Fact]
    public async Task Read_AgainFailsWhole_WhenOneRunIsRefusedThisTime()
    {
        var network = new ScriptedHttpMessageHandler(ByRun(
            ("t1", [PageResponse(Page(PostJson("300")))]),
            ("t5", [
                PageResponse(Page(PostJson("250"))),
                ScriptedHttpMessageHandler.Status(HttpStatusCode.InternalServerError),
            ])));
        var reader = NewReader(network);
        var set = Timeline.AnyOf(TagSet.Of("many", Tags(5)));

        var first = await reader.Read(Profile, set, 20, Token);

        Assert.Equal(["300", "250"], first.Items.Select(post => post.Id));
        await Assert.ThrowsAsync<HttpRequestException>(() => reader.Read(Profile, set, 20, Token));
    }

    [Fact]
    public async Task ReadOn_FailsWhole_WhenARunItAsksIsRefused()
    {
        var network = new ScriptedHttpMessageHandler(ByRun(
            ("t1", [PageResponse(Page(PostJson("300"), PostJson("200")), nextMaxId: "200")]),
            ("t5", [
                PageResponse(Page(PostJson("250")), nextMaxId: "250"),
                ScriptedHttpMessageHandler.Status(HttpStatusCode.InternalServerError),
            ])));
        var reader = NewReader(network);
        var set = Timeline.AnyOf(TagSet.Of("many", Tags(5)));

        var first = await reader.Read(Profile, set, 2, Token);

        await Assert.ThrowsAsync<HttpRequestException>(() => reader.ReadOn(Profile, set, first.Onward!, 20, Token));
    }

    /// <summary>
    ///     In a set of several runs, a run the rate limit refuses is a run that did not answer, so nothing of the other
    ///     is kept either, where a set of one run keeps what arrived (ADR-0033's amendment of ADR-0007). It is said
    ///     as every limited read is, though — stopped by the limit rather than thrown (ADR-0006) — and goes on from
    ///     where it started, so reading on once the limit is waited out asks both runs from the top again.
    /// </summary>
    [Fact]
    public async Task Read_KeepsNothing_WhenTheRateLimitRefusesOneOfSeveralRuns_AndReadsOnFromTheTop()
    {
        var network = new ScriptedHttpMessageHandler(ByRun(
            ("t1", [PageResponse(Page(PostJson("300")))]),
            ("t5", [RateLimited, PageResponse(Page(PostJson("250")))])));
        var reader = NewReader(network);
        var set = Timeline.AnyOf(TagSet.Of("many", Tags(5)));

        var fetch = await reader.Read(Profile, set, 20, Token);
        var again = await reader.ReadOn(Profile, set, fetch.Onward!, 20, Token);

        Assert.Empty(fetch.Items);
        Assert.IsType<RateLimitedException>(fetch.StoppedBy);
        Assert.Equal(
            [
                "https://mastodon.social/api/v1/timelines/tag/t1?limit=20&any[]=t2&any[]=t3&any[]=t4",
                "https://mastodon.social/api/v1/timelines/tag/t5?limit=20",
            ],
            network.Requests.Skip(2).Select(request => request.RequestUri?.OriginalString).Order(StringComparer.Ordinal));
        Assert.Equal(["300", "250"], again.Items.Select(post => post.Id));
    }

    /// <summary>
    ///     And on a read on, where it keeps nothing the read on fetched, and reads on again from where the read on it
    ///     stopped started: the same runs asked the same thing.
    /// </summary>
    [Fact]
    public async Task ReadOn_KeepsNothing_WhenTheRateLimitRefusesOneOfSeveralRuns_AndReadsOnFromWhereItStarted()
    {
        var network = new ScriptedHttpMessageHandler(ByRun(
            ("t1", [
                PageResponse(Page(PostJson("300"), PostJson("200")), nextMaxId: "200"),
                PageResponse(Page(PostJson("190"))),
            ]),
            ("t5", [
                PageResponse(Page(PostJson("250")), nextMaxId: "250"),
                RateLimited,
                PageResponse(Page(PostJson("240"))),
            ])));
        var reader = NewReader(network);
        var set = Timeline.AnyOf(TagSet.Of("many", Tags(5)));

        var first = await reader.Read(Profile, set, 2, Token);
        var stopped = await reader.ReadOn(Profile, set, first.Onward!, 20, Token);
        var again = await reader.ReadOn(Profile, set, stopped.Onward!, 20, Token);

        Assert.Equal(["300", "250"], first.Items.Select(post => post.Id));
        Assert.Empty(stopped.Items);
        Assert.IsType<RateLimitedException>(stopped.StoppedBy);
        Assert.Equal(["240", "200", "190"], again.Items.Select(post => post.Id));
        Assert.All(
            network.Requests.Skip(2).Take(2),
            request => Assert.Equal(
                "https://mastodon.social/api/v1/timelines/tag/t5?limit=20&max_id=250",
                request.RequestUri?.OriginalString));
    }

    /// <summary>Newest first, as the instance answered, and paged on as every timeline is.</summary>
    [Fact]
    public async Task ReadOn_AsksForThePageTheLastReadNamedNext_KeepingTheSetsTags()
    {
        var network = new ScriptedHttpMessageHandler(
            PageResponse(Page(PostJson("200"), PostJson("199")), nextMaxId: "199"),
            PageResponse(Page(PostJson("150"))));
        var reader = NewReader(network);
        var set = Timeline.AnyOf(TagSet.Of("dotnet", ["dotnet", "csharp"]));

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
            NewReader(network).Read(Profile, Timeline.AnyOf(TagSet.Of("cats", ["cats"])), 20, Token));
    }

    /// <summary>
    ///     A set of one run is one list read a page at a time, as Home is, so a rate limit on its first page stops it with
    ///     nothing in hand, said as the limit rather than as an empty timeline (ADR-0007).
    /// </summary>
    [Fact]
    public async Task Read_StopsShortOnARateLimitOnTheFirstPage_WhenTheSetIsOneRun()
    {
        var network = new ScriptedHttpMessageHandler(RateLimited);

        var fetch = await NewReader(network).Read(Profile, Timeline.AnyOf(TagSet.Of("cats", ["cats"])), 20, Token);

        Assert.Empty(fetch.Items);
        Assert.IsType<RateLimitedException>(fetch.StoppedBy);
    }

    /// <summary>
    ///     And part way, it keeps what arrived, exactly as Home and the one hashtag before sets did: one run has no
    ///     middle for a missing run to leave a hole in (ADR-0007, ADR-0033). It reads on from the page the limit refused.
    /// </summary>
    [Fact]
    public async Task Read_KeepsWhatArrived_WhenTheRateLimitStopsASetOfOneRunPartWay()
    {
        var network = new ScriptedHttpMessageHandler(
            PageResponse(Page(PostJson("200"), PostJson("199")), nextMaxId: "199"),
            RateLimited,
            PageResponse(Page(PostJson("150"))));
        var reader = NewReader(network);
        var set = Timeline.AnyOf(TagSet.Of("dotnet", ["dotnet", "csharp"]));

        var fetch = await reader.Read(Profile, set, 60, Token);
        var onward = await reader.ReadOn(Profile, set, fetch.Onward!, 20, Token);

        Assert.Equal(["200", "199"], fetch.Items.Select(post => post.Id));
        Assert.IsType<RateLimitedException>(fetch.StoppedBy);
        Assert.Equal(
            "https://mastodon.social/api/v1/timelines/tag/dotnet?limit=20&max_id=199&any[]=csharp",
            network.Requests[2].RequestUri?.OriginalString);
        Assert.Equal("150", Assert.Single(onward.Items).Id);
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

    /// <summary>
    ///     Answers each of a set's runs by the tag its ask puts in the path, rather than by when it is asked, since the
    ///     runs are asked at once: each run's answers in turn, its last repeating once they run out.
    /// </summary>
    private static Func<HttpRequestMessage, HttpResponseMessage> ByRun(
        params (string First, Func<HttpRequestMessage, HttpResponseMessage>[] Answers)[] runs)
    {
        var asked = runs.ToDictionary(run => run.First, _ => 0);
        var counting = new Lock();

        return request =>
        {
            var first = request.RequestUri!.AbsolutePath.Split('/')[^1];
            var answers = runs.Single(run => run.First == first).Answers;
            int turn;

            lock (counting)
            {
                turn = asked[first]++;
            }

            return answers[Math.Min(turn, answers.Length - 1)](request);
        };
    }

    /// <summary>A step answering as an instance does a profile that has spent its quota.</summary>
    private static HttpResponseMessage RateLimited(HttpRequestMessage request) =>
        new(HttpStatusCode.TooManyRequests)
        {
            Content = new StringContent("""{"error":"Too many requests"}""", Encoding.UTF8, "application/json"),
        };

    private static string[] Tags(int count) => [.. Enumerable.Range(1, count).Select(n => $"t{n}")];

    /// <summary>
    ///     What was asked of the instance, in no particular order: a set's runs are asked at once, so the order they
    ///     arrive in is not the reader's to promise.
    /// </summary>
    private static List<string?> Asked(ScriptedHttpMessageHandler network) =>
        [.. network.Requests.Select(request => request.RequestUri?.OriginalString).Order(StringComparer.Ordinal)];

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
