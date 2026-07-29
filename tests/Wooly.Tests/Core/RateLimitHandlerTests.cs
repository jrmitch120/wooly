using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using Wooly.Core.Errors;
using Wooly.Core.Http;
using Wooly.Tests.Fakes;

namespace Wooly.Tests.Core;

public class RateLimitHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 29, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task SendAsync_HandsBackAnUnlimitedResponseUntouched()
    {
        var network = new ScriptedHttpMessageHandler(ScriptedHttpMessageHandler.Json("{}"));

        var response = await Send(network, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task SendAsync_FailsTheCallInsteadOfReturningARateLimitedResponse()
    {
        var network = new ScriptedHttpMessageHandler(ScriptedHttpMessageHandler.Status(HttpStatusCode.TooManyRequests));
        var cancellationToken = TestContext.Current.CancellationToken;

        var exception = await Assert.ThrowsAsync<RateLimitedException>(() => Send(network, cancellationToken));

        Assert.Equal("mastodon.social", exception.Instance);
        Assert.Contains("Rate limited by mastodon.social", exception.Message);
    }

    /// <summary>Fail fast means fail fast: exactly one request goes out, and no wait happens inside the handler.</summary>
    [Fact]
    public async Task SendAsync_NeverWaitsOutOrRetriesARateLimit()
    {
        var network = new ScriptedHttpMessageHandler(ScriptedHttpMessageHandler.Status(HttpStatusCode.TooManyRequests));
        var cancellationToken = TestContext.Current.CancellationToken;

        await Assert.ThrowsAsync<RateLimitedException>(() => Send(network, cancellationToken));

        Assert.Single(network.Requests);
    }

    [Fact]
    public async Task SendAsync_CarriesTheResetTimeMastodonReportsOnTheFailure()
    {
        var resetsAt = Now.AddMinutes(3);
        var network = new ScriptedHttpMessageHandler(RateLimited(response =>
            response.Headers.Add("X-RateLimit-Reset", resetsAt.ToString("o"))));
        var cancellationToken = TestContext.Current.CancellationToken;

        var exception = await Assert.ThrowsAsync<RateLimitedException>(() => Send(network, cancellationToken));

        Assert.Equal(resetsAt, exception.ResetsAt);
        Assert.Contains("2026-07-29 12:03:00Z", exception.Message);
    }

    [Fact]
    public async Task SendAsync_ReadsARetryAfterDelayAsADeadlineRelativeToNow()
    {
        var network = new ScriptedHttpMessageHandler(RateLimited(response =>
            response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(90))));
        var cancellationToken = TestContext.Current.CancellationToken;

        var exception = await Assert.ThrowsAsync<RateLimitedException>(() => Send(network, cancellationToken));

        Assert.Equal(Now.AddSeconds(90), exception.ResetsAt);
    }

    [Fact]
    public async Task SendAsync_StillReadsWellWhenTheInstanceDoesNotSayWhenTheLimitResets()
    {
        var network = new ScriptedHttpMessageHandler(ScriptedHttpMessageHandler.Status(HttpStatusCode.TooManyRequests));
        var cancellationToken = TestContext.Current.CancellationToken;

        var exception = await Assert.ThrowsAsync<RateLimitedException>(() => Send(network, cancellationToken));

        Assert.Null(exception.ResetsAt);
        Assert.Contains("Wait a while before retrying", exception.Message);
    }

    private static Func<HttpRequestMessage, HttpResponseMessage> RateLimited(Action<HttpResponseMessage> describeLimit) =>
        _ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
            describeLimit(response);

            return response;
        };

    private static async Task<HttpResponseMessage> Send(HttpMessageHandler network, CancellationToken cancellationToken)
    {
        var handler = new RateLimitHandler(new FixedTimeProvider(Now))
        {
            InnerHandler = network,
        };

        using var client = new HttpClient(handler);

        return await client.GetAsync("https://mastodon.social/api/v1/timelines/home", cancellationToken);
    }
}
