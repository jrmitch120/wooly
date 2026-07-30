using System.Net;
using System.Net.Http;
using System.Text;

namespace Wooly.Tests.Fakes;

/// <summary>
///     Stands in for the network: each send is answered by the next step in a script, and the last step repeats once
///     the script runs out. A step may also throw, which is how a transient network failure is faked.
/// </summary>
internal sealed class ScriptedHttpMessageHandler(params Func<HttpRequestMessage, HttpResponseMessage>[] steps) : HttpMessageHandler
{
    /// <summary>Every request the handler was asked to send, in order.</summary>
    public List<HttpRequestMessage> Requests { get; } = [];

    /// <summary>
    ///     What each of those requests carried, in the same order, and an empty string where one carried nothing. Read
    ///     as the request goes past rather than off <see cref="Requests" /> afterwards, because a request body is a
    ///     stream that has been consumed by then.
    /// </summary>
    public List<string> Bodies { get; } = [];

    /// <summary>A handler that fails every send the way a dropped connection does.</summary>
    public static ScriptedHttpMessageHandler AlwaysUnreachable(string message = "Connection refused") =>
        new(Unreachable(message));

    /// <summary>A step answering with <paramref name="json" /> and <c>200 OK</c>.</summary>
    public static Func<HttpRequestMessage, HttpResponseMessage> Json(string json) =>
        _ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };

    /// <summary>A step that fails the way a dropped connection does.</summary>
    public static Func<HttpRequestMessage, HttpResponseMessage> Unreachable(string message = "Connection refused") =>
        _ => throw new HttpRequestException(message);

    /// <summary>A step answering with a bare status code and no body.</summary>
    public static Func<HttpRequestMessage, HttpResponseMessage> Status(HttpStatusCode statusCode) =>
        _ => new HttpResponseMessage(statusCode);

    /// <summary>A step answering the way an instance turns a request down: an error status plus Mastodon's error JSON.</summary>
    public static Func<HttpRequestMessage, HttpResponseMessage> Refusal(HttpStatusCode statusCode, string error) =>
        _ => new HttpResponseMessage(statusCode)
        {
            Content = new StringContent($$"""{"error":"{{error}}"}""", Encoding.UTF8, "application/json"),
        };

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        Requests.Add(request);
        Bodies.Add(request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken));

        var step = steps[Math.Min(Requests.Count - 1, steps.Length - 1)];

        return step(request);
    }
}
