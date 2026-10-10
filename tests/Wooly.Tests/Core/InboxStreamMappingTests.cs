using Mastonet;
using Wooly.Core.Inbox;
using Wooly.Core.Notifications;

namespace Wooly.Tests.Core;

/// <summary>
///     The Inbox stream adapter tells what Mastonet's user streaming reads off the wire as the port's events (#401):
///     a notification with its kind, a conversation, and nothing for the rest of what a user stream carries. Driven
///     through Mastonet's own <see cref="TimelineStreaming" />, so its reading of each payload is what is mapped.
/// </summary>
public class InboxStreamMappingTests
{
    [Fact]
    public async Task ANotification_IsToldWithItsKind()
    {
        var heard = await Relayed(("notification", NotificationJson("mention")));

        Assert.Equal([new InboxEvent.Notified(NotificationKind.Mention)], heard);
    }

    [Fact]
    public async Task AFollowRequest_IsToldAsOneThatAsksToFollow()
    {
        var heard = await Relayed(("notification", NotificationJson("follow_request")));

        var notified = Assert.IsType<InboxEvent.Notified>(Assert.Single(heard));
        Assert.True(notified.Kind.AsksToFollow);
    }

    [Fact]
    public async Task AConversation_IsToldAsOne()
    {
        var heard = await Relayed(("conversation", ConversationJson));

        Assert.Equal([new InboxEvent.Conversed()], heard);
    }

    /// <summary>A post on the home timeline, a deletion, a filter changing: none of it is the Inbox's.</summary>
    [Fact]
    public async Task WhatIsNotTheInboxs_IsNotTold()
    {
        var heard = await Relayed(("delete", "110"), ("filters_changed", ""));

        Assert.Empty(heard);
    }

    [Fact]
    public async Task EachIsTold_InTheOrderItCame()
    {
        var heard = await Relayed(
            ("notification", NotificationJson("mention")),
            ("conversation", ConversationJson));

        Assert.Equal([new InboxEvent.Notified(NotificationKind.Mention), new InboxEvent.Conversed()], heard);
    }

    private static async Task<List<InboxEvent>> Relayed(params (string Event, string Payload)[] said)
    {
        var heard = new List<InboxEvent>();

        await InboxStream.Relay(new Scripted(said), heard.Add);

        return heard;
    }

    /// <summary>Mastonet's streaming, saying what a test scripts and then ending, as a dropped connection does.</summary>
    private sealed class Scripted((string Event, string Payload)[] said)
        : TimelineStreaming(StreamingType.User, param: null, accessToken: null)
    {
        public override Task Start()
        {
            foreach (var (name, payload) in said)
            {
                SendEvent(name, payload);
            }

            return Task.CompletedTask;
        }

        public override void Stop()
        {
        }
    }

    private static string NotificationJson(string type) =>
        $$"""
          {
            "id": "1",
            "type": "{{type}}",
            "created_at": "2026-07-29T12:04:00.000Z",
            "account": { "id": "2", "username": "alice", "acct": "alice@hachyderm.io", "display_name": "Alice" },
            "status": null
          }
          """;

    private const string ConversationJson =
        """
        {
          "id": "c1",
          "unread": true,
          "accounts": [{ "id": "2", "username": "alice", "acct": "alice@hachyderm.io", "display_name": "Alice" }],
          "last_status": null
        }
        """;
}
