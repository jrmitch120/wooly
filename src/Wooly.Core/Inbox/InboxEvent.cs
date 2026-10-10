using Wooly.Core.Notifications;

namespace Wooly.Core.Inbox;

/// <summary>Something the <b>Inbox stream</b> said (#401). Only what touches the Inbox is told; the rest is not.</summary>
public abstract record InboxEvent
{
    private InboxEvent()
    {
    }

    /// <summary>The connection is up, and what arrives from here on will be told.</summary>
    public sealed record Connected : InboxEvent;

    /// <summary>A notification arrived, of <paramref name="Kind" />.</summary>
    /// <param name="Kind">Its kind, in this project's words — a follow request is one the Requests badge counts too.</param>
    public sealed record Notified(NotificationKind Kind) : InboxEvent;

    /// <summary>A conversation arrived, or one already there changed — a new direct message, read or not.</summary>
    public sealed record ConversationChanged : InboxEvent;
}
