using System.Net;
using System.Net.Http;
using Wooly.Core.Errors;
using Wooly.Core.Http;
using Wooly.Tests.Fakes;

namespace Wooly.Tests.Core;

/// <summary>
///     An instance answering 422 to a post being published or changed is turning the post down as written — most
///     often for being longer than it allows — which <see cref="PostRefusedException" /> names in the instance's own
///     words (#319). Read here, off the status, because Mastonet's own exception for it carries no status at all.
/// </summary>
public class RefusedPostHandlerTests
{
    private const string TooLong = "Validation failed: Text character limit of 500 exceeded";

    [Theory]
    [InlineData("POST", "https://mastodon.social/api/v1/statuses")]
    [InlineData("PUT", "https://mastodon.social/api/v1/statuses/110")]
    public async Task SendAsync_TurnsA422ToAPostIntoARefusal_InTheInstancesWords(string method, string address)
    {
        var network = new ScriptedHttpMessageHandler(
            ScriptedHttpMessageHandler.Refusal(HttpStatusCode.UnprocessableEntity, TooLong));

        var exception = await Assert.ThrowsAsync<PostRefusedException>(
            () => Send(network, new HttpMethod(method), address, TestContext.Current.CancellationToken));

        Assert.Equal($"The instance would not take that post: {TooLong}", exception.Message);
    }

    [Fact]
    public async Task SendAsync_SaysSoEvenWhereTheInstanceGaveNoReason()
    {
        var network = new ScriptedHttpMessageHandler(ScriptedHttpMessageHandler.Status(HttpStatusCode.UnprocessableEntity));

        var exception = await Assert.ThrowsAsync<PostRefusedException>(
            () => Send(network, HttpMethod.Post, "https://mastodon.social/api/v1/statuses", TestContext.Current.CancellationToken));

        Assert.Equal("The instance would not take that post.", exception.Message);
    }

    /// <summary>
    ///     A 422 to anything else — a boost, a vote, an upload — is about that thing rather than a post as written, and
    ///     is handed back for its own caller to say.
    /// </summary>
    [Theory]
    [InlineData("POST", "https://mastodon.social/api/v1/statuses/110/reblog")]
    [InlineData("POST", "https://mastodon.social/api/v1/polls/7/votes")]
    [InlineData("POST", "https://mastodon.social/api/v2/media")]
    [InlineData("GET", "https://mastodon.social/api/v1/statuses/110")]
    public async Task SendAsync_HandsBackA422ToAnythingElseUntouched(string method, string address)
    {
        var network = new ScriptedHttpMessageHandler(
            ScriptedHttpMessageHandler.Refusal(HttpStatusCode.UnprocessableEntity, "No."));

        var response = await Send(network, new HttpMethod(method), address, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    /// <summary>And any other refusal of a post, which is not the instance judging what was written.</summary>
    [Theory]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.NotFound)]
    public async Task SendAsync_HandsBackAnyOtherRefusalUntouched(HttpStatusCode status)
    {
        var network = new ScriptedHttpMessageHandler(ScriptedHttpMessageHandler.Status(status));

        var response = await Send(network, HttpMethod.Put, "https://mastodon.social/api/v1/statuses/110", TestContext.Current.CancellationToken);

        Assert.Equal(status, response.StatusCode);
    }

    private static async Task<HttpResponseMessage> Send(
        HttpMessageHandler network,
        HttpMethod method,
        string address,
        CancellationToken cancellationToken)
    {
        var handler = new RefusedPostHandler { InnerHandler = network };

        using var client = new HttpClient(handler);
        using var request = new HttpRequestMessage(method, address);

        return await client.SendAsync(request, cancellationToken);
    }
}
