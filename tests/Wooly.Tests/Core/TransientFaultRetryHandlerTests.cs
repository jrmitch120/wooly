using System.Net;
using System.Net.Http;
using Wooly.Core.Errors;
using Wooly.Core.Http;
using Wooly.Tests.Fakes;

namespace Wooly.Tests.Core;

public class TransientFaultRetryHandlerTests
{
    private static readonly RetryPolicy TwoRetries = new(TimeSpan.FromMilliseconds(250), TimeSpan.FromMilliseconds(750));

    [Fact]
    public async Task SendAsync_DoesNotRetryASendThatSucceeds()
    {
        var network = new ScriptedHttpMessageHandler(ScriptedHttpMessageHandler.Json("{}"));

        var response = await Send(network, TwoRetries, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Single(network.Requests);
    }

    [Fact]
    public async Task SendAsync_RetriesATransientFailureAndReturnsTheLaterSuccess()
    {
        var network = new ScriptedHttpMessageHandler(
            ScriptedHttpMessageHandler.Unreachable(),
            ScriptedHttpMessageHandler.Json("{}"));

        var response = await Send(network, TwoRetries, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, network.Requests.Count);
    }

    [Fact]
    public async Task SendAsync_ReportsAFailureOnceThePolicysRetriesAreSpent()
    {
        var network = ScriptedHttpMessageHandler.AlwaysUnreachable("Connection refused");
        var cancellationToken = TestContext.Current.CancellationToken;

        var exception = await Assert.ThrowsAsync<TransientNetworkException>(
            () => Send(network, TwoRetries, cancellationToken));

        // Two retries on top of the first attempt.
        Assert.Equal(3, network.Requests.Count);
        Assert.Equal(3, exception.Attempts);
        Assert.Contains("mastodon.social", exception.Message);
        Assert.Contains("Connection refused", exception.Message);
    }

    [Fact]
    public async Task SendAsync_WaitsThePolicysBackoffBeforeEachRetry()
    {
        var delay = new RecordingRetryDelay();
        var cancellationToken = TestContext.Current.CancellationToken;

        await Assert.ThrowsAsync<TransientNetworkException>(
            () => Send(ScriptedHttpMessageHandler.AlwaysUnreachable(), TwoRetries, cancellationToken, delay));

        Assert.Equal([TimeSpan.FromMilliseconds(250), TimeSpan.FromMilliseconds(750)], delay.Waits);
    }

    [Fact]
    public async Task Default_BacksOffRoughlyAQuarterThenThreeQuartersOfASecond()
    {
        var delay = new RecordingRetryDelay();
        var cancellationToken = TestContext.Current.CancellationToken;

        await Assert.ThrowsAsync<TransientNetworkException>(
            () => Send(ScriptedHttpMessageHandler.AlwaysUnreachable(), RetryPolicy.Default, cancellationToken, delay));

        Assert.Equal([TimeSpan.FromMilliseconds(250), TimeSpan.FromMilliseconds(750)], delay.Waits);
    }

    /// <summary>
    ///     A 5xx is a response, not a failure to reach the instance — and resending it could publish a post twice.
    ///     ADR-0006 keeps retries to connection-level faults for exactly that reason.
    /// </summary>
    [Fact]
    public async Task SendAsync_HandsBackAServerErrorResponseWithoutARetry()
    {
        var network = new ScriptedHttpMessageHandler(ScriptedHttpMessageHandler.Status(HttpStatusCode.InternalServerError));

        var response = await Send(network, TwoRetries, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Single(network.Requests);
    }

    /// <summary>
    ///     A cancellation arriving here is either the caller giving up or <c>HttpClient.Timeout</c> elapsing, and the
    ///     handler cannot tell them apart. Neither is worth retrying, so neither is (ADR-0006).
    /// </summary>
    [Fact]
    public async Task SendAsync_DoesNotRetryACancelledRequest()
    {
        using var cancellation = new CancellationTokenSource();

        var network = new ScriptedHttpMessageHandler(_ =>
        {
            cancellation.Cancel();

            throw new TaskCanceledException();
        });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Send(network, TwoRetries, cancellation.Token));

        Assert.Single(network.Requests);
    }

    private static async Task<HttpResponseMessage> Send(
        HttpMessageHandler network,
        RetryPolicy policy,
        CancellationToken cancellationToken,
        IRetryDelay? delay = null)
    {
        var handler = new TransientFaultRetryHandler(policy, delay ?? new RecordingRetryDelay())
        {
            InnerHandler = network,
        };

        using var client = new HttpClient(handler);

        return await client.GetAsync("https://mastodon.social/api/v1/instance", cancellationToken);
    }
}
