using System.Net.Http;
using System.Net.WebSockets;
using System.Text.Json;
using System.Text.Json.Serialization;
using Mastonet;
using Wooly.Core.Http;
using Wooly.Core.Notifications;
using Wooly.Core.Profiles;

namespace Wooly.Core.Inbox;

/// <summary>
///     The user stream over a websocket, told through Mastonet's <see cref="TimelineStreaming" /> (#401): Mastonet reads
///     each notification and conversation off the wire, and this tells what it read as the port's events.
/// </summary>
/// <remarks>
///     The connection is this adapter's own rather than <c>GetUserStreaming</c>'s, for what that one cannot do: it says
///     nothing when it has connected, which is what stops the shell polling; it cannot be called off while it connects;
///     it puts the token in the address rather than a header; it reads a closed connection as a message and throws on
///     it; and it dials with a <see cref="ClientWebSocket" /> of its own, past every handler the named
///     <see cref="HttpClient" /> carries. So it is connected here — through those handlers, with the token in a header —
///     and read here, and each message Mastonet would have read is handed to its <see cref="TimelineStreaming" /> to
///     read, the same way ADR-0027 reads the marker around Mastonet and keeps the rest behind it.
///     <para>
///         Two streams over one connection: <c>user</c>, for notifications, and <c>direct</c>, for conversations —
///         which the user stream does not carry, a direct message reaching it only as the mention it also is.
///     </para>
///     <para>
///         Only what is the Inbox's is handed on: the user stream carries the home timeline too, and a post Mastonet
///         could not read is no reason to drop a stream that is only listened to for notifications and conversations.
///     </para>
/// </remarks>
public sealed class InboxStream(IHttpClientFactory httpClientFactory, IHttpMessageHandlerFactory handlerFactory)
    : IInboxStream
{
    /// <inheritdoc />
    public async Task Listen(ActiveProfile profile, Action<InboxEvent> heard, CancellationToken cancellationToken)
    {
        var address = await StreamingAddress(profile, cancellationToken);

        using var socket = new ClientWebSocket();

        // A header rather than the address's access_token, so the token is never written into a log of addresses.
        socket.Options.SetRequestHeader("Authorization", $"Bearer {profile.AccessToken}");

        using var invoker = new HttpMessageInvoker(
            handlerFactory.CreateHandler(WoolyClient.HttpClientName),
            disposeHandler: false);

        await socket.ConnectAsync(address, invoker, cancellationToken);

        // The user stream carries notifications but not conversations, which Mastodon publishes on the direct stream
        // alone — so that is subscribed to as well, over the same connection.
        await socket.SendAsync(
            """{"type":"subscribe","stream":"direct"}"""u8.ToArray(),
            WebSocketMessageType.Text,
            endOfMessage: true,
            cancellationToken);

        heard(new InboxEvent.Connected());

        await Relay(new SocketStreaming(socket, cancellationToken), heard);
    }

    /// <summary>
    ///     Tells <paramref name="heard" /> what <paramref name="streaming" /> reads, until it stops: a notification with
    ///     its kind, in this project's words, and a conversation as one. Everything else it reads is not the Inbox's.
    /// </summary>
    internal static async Task Relay(TimelineStreaming streaming, Action<InboxEvent> heard)
    {
        streaming.OnNotification += (_, said) =>
            heard(new InboxEvent.Notified(NotificationWire.ToKind(said.Notification.Type)));
        streaming.OnConversation += (_, _) => heard(new InboxEvent.ConversationChanged());

        await streaming.Start();
    }

    /// <summary>
    ///     Where the instance says its stream is, on its own description of itself — which need not be the instance's
    ///     own address — or the instance itself where it does not say.
    /// </summary>
    private async Task<Uri> StreamingAddress(ActiveProfile profile, CancellationToken cancellationToken)
    {
        string? advertised = null;

        try
        {
            var described = await RawMastodonCall.Get<InstanceWire>(
                httpClientFactory,
                profile,
                "api/v2/instance",
                [],
                cancellationToken);

            advertised = described?.Configuration?.Urls?.Streaming;
        }
        catch (HttpRequestException refused) when (refused.StatusCode is not null)
        {
            // An instance with no v2 description — older than Mastodon 4.0 — streams where it is.
        }

        var root = string.IsNullOrEmpty(advertised) ? $"wss://{profile.Instance}" : advertised.TrimEnd('/');

        return new Uri($"{root}/api/v1/streaming?stream=user");
    }

    /// <summary>
    ///     Mastonet's streaming over a socket already connected: each message's event and payload handed to Mastonet to
    ///     read, until the instance closes it or the token does.
    /// </summary>
    private sealed class SocketStreaming(WebSocket socket, CancellationToken cancellationToken)
        : TimelineStreaming(StreamingType.User, param: null, accessToken: null)
    {
        public override async Task Start()
        {
            var buffer = new byte[4096];

            using var message = new MemoryStream();

            while (true)
            {
                var received = await socket.ReceiveAsync(buffer, cancellationToken);

                if (received.MessageType == WebSocketMessageType.Close)
                {
                    return;
                }

                message.Write(buffer, 0, received.Count);

                if (!received.EndOfMessage)
                {
                    continue;
                }

                var said = JsonSerializer.Deserialize<StreamMessage>(message.ToArray());

                message.SetLength(0);

                if (said is { Event: "notification" or "conversation", Payload: { } payload })
                {
                    SendEvent(said.Event, payload);
                }
            }
        }

        /// <summary>Nothing: the connection is closed by the token it was opened with.</summary>
        public override void Stop()
        {
        }
    }

    /// <summary>One message on the stream: what it is, and what it carries, itself a JSON document in a string.</summary>
    private sealed record StreamMessage
    {
        [JsonPropertyName("event")]
        public string? Event { get; init; }

        [JsonPropertyName("payload")]
        public string? Payload { get; init; }
    }

    /// <summary>The little of an instance's description of itself this reads.</summary>
    private sealed record InstanceWire
    {
        [JsonPropertyName("configuration")]
        public ConfigurationWire? Configuration { get; init; }
    }

    private sealed record ConfigurationWire
    {
        [JsonPropertyName("urls")]
        public UrlsWire? Urls { get; init; }
    }

    private sealed record UrlsWire
    {
        [JsonPropertyName("streaming")]
        public string? Streaming { get; init; }
    }
}
