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

    /// <summary>
    ///     Story 54's indicator has to be drawable before the budget runs out, so what is left is read off every
    ///     response rather than off the one that finally says none.
    /// </summary>
    [Fact]
    public async Task SendAsync_TakesDownTheBudgetAnOrdinaryResponseReported()
    {
        var report = new RateLimitReport();
        var network = new ScriptedHttpMessageHandler(Budgeted(remaining: "213", limit: "300"));

        await Send(network, TestContext.Current.CancellationToken, report);

        Assert.Equal(213, report.Latest?.Remaining);
        Assert.Equal(300, report.Latest?.Limit);
        Assert.Equal(new DateTimeOffset(2026, 7, 29, 12, 30, 0, TimeSpan.Zero), report.Latest?.ResetsAt);
    }

    /// <summary>The refusal is the one response a reader most wants a number off, so it reports one too.</summary>
    [Fact]
    public async Task SendAsync_TakesDownTheBudgetARefusalReported()
    {
        var report = new RateLimitReport();
        var network = new ScriptedHttpMessageHandler(RateLimited(response =>
        {
            response.Headers.Add("X-RateLimit-Remaining", "0");
            response.Headers.Add("X-RateLimit-Limit", "300");
        }));
        var cancellationToken = TestContext.Current.CancellationToken;

        await Assert.ThrowsAsync<RateLimitedException>(() => Send(network, cancellationToken, report));

        Assert.Equal(0, report.Latest?.Remaining);
        Assert.Equal(300, report.Latest?.Limit);
    }

    /// <summary>
    ///     Both numbers or neither: a remaining count with no limit to read it against cannot be drawn as a
    ///     proportion, and half a budget shown as a whole one is worse than showing nothing.
    /// </summary>
    [Theory]
    [InlineData(null, null)]
    [InlineData("213", null)]
    [InlineData(null, "300")]
    [InlineData("not a number", "300")]
    public async Task SendAsync_ReportsNoBudgetWhereTheInstanceGaveOnlyHalfOfOne(string? remaining, string? limit)
    {
        var report = new RateLimitReport();
        var network = new ScriptedHttpMessageHandler(Budgeted(remaining, limit));

        await Send(network, TestContext.Current.CancellationToken, report);

        Assert.Null(report.Latest);
    }

    /// <summary>A budget one call out of date is a budget; a budget overwritten by a response that carried none is not.</summary>
    [Fact]
    public async Task SendAsync_KeepsTheLastBudgetWhereAResponseReportsNone()
    {
        var report = new RateLimitReport();
        var network = new ScriptedHttpMessageHandler(
            Budgeted(remaining: "213", limit: "300"),
            ScriptedHttpMessageHandler.Json("{}"));
        var cancellationToken = TestContext.Current.CancellationToken;

        await Send(network, cancellationToken, report);
        await Send(network, cancellationToken, report);

        Assert.Equal(213, report.Latest?.Remaining);
    }

    /// <summary>What a proportion is drawn from, including the instance that says this client cannot divide by it.</summary>
    [Theory]
    [InlineData(213, 300, 0.71)]
    [InlineData(0, 300, 0)]
    [InlineData(300, 300, 1)]
    [InlineData(5, 0, 0)]
    public void Fraction_IsWhatIsLeftOfWhatIsAllowed(int remaining, int limit, double expected)
    {
        Assert.Equal(expected, new RateLimitQuota(remaining, limit, ResetsAt: null).Fraction, 2);
    }

    /// <summary>A response carrying whatever the instance said about the budget, and when the window rolls over.</summary>
    private static Func<HttpRequestMessage, HttpResponseMessage> Budgeted(string? remaining, string? limit) =>
        _ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK);

            if (remaining is not null)
            {
                response.Headers.Add("X-RateLimit-Remaining", remaining);
            }

            if (limit is not null)
            {
                response.Headers.Add("X-RateLimit-Limit", limit);
            }

            response.Headers.Add("X-RateLimit-Reset", "2026-07-29T12:30:00.000Z");

            return response;
        };

    private static Func<HttpRequestMessage, HttpResponseMessage> RateLimited(Action<HttpResponseMessage> describeLimit) =>
        _ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
            describeLimit(response);

            return response;
        };

    private static async Task<HttpResponseMessage> Send(
        HttpMessageHandler network,
        CancellationToken cancellationToken,
        RateLimitReport? report = null)
    {
        var handler = new RateLimitHandler(new FixedTimeProvider(Now), report ?? new RateLimitReport())
        {
            InnerHandler = network,
        };

        using var client = new HttpClient(handler);

        return await client.GetAsync("https://mastodon.social/api/v1/timelines/home", cancellationToken);
    }
}
