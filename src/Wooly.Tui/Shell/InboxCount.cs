using Wooly.Core.Accounts;
using Wooly.Core.Conversations;
using Wooly.Core.Profiles;

namespace Wooly.Tui.Shell;

/// <summary>
///     How each <b>Inbox</b> destination's badge is counted: its <b>Unread count</b>, in the instance's own terms for
///     each (CONTEXT.md, ADR-0027). Said once, here, because a badge is read from two places — the counts read when the
///     shell opens, and arriving at the destination itself — and the two must not come to disagree (#396).
/// </summary>
/// <remarks>
///     Two of the three are counted off the very list their screen draws, so arriving there costs nothing more than
///     the list; <see cref="OffList" /> is that count, and <see cref="Read" /> reads the list and counts it the same
///     way. Notifications is the one counted apart from its list: what is unread is what is newer than the read
///     marker, which the list read does not say, so it is its own read — the same one on opening and on arriving.
/// </remarks>
internal static class InboxCount
{
    /// <summary>Reads the badge for <paramref name="kind" /> as <paramref name="profile" />.</summary>
    /// <exception cref="ArgumentOutOfRangeException">Not one of the Inbox's three.</exception>
    public static async Task<Found.Badge> Read(
        ShellPorts ports,
        ActiveProfile profile,
        DestinationKind kind,
        CancellationToken cancellationToken) =>
        kind switch
        {
            // No marker reads as nothing unread rather than everything: a count this client cannot prove is not one
            // it draws.
            DestinationKind.Notifications => Of(
                kind,
                (await ports.Notifications.ReadUnread(profile, Arrival.CountedAtMost, cancellationToken))
                .Since.Items.Count),
            DestinationKind.Messages => OffList(
                (await ports.Messages.List(profile, Arrival.CountedAtMost, cancellationToken)).Items),
            DestinationKind.Requests => OffList(
                (await ports.Accounts.PendingRequests(profile, Arrival.CountedAtMost, cancellationToken)).Items),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Not a destination in the Inbox."),
        };

    /// <summary>The Messages badge, off the conversations listed: those the instance flags unread.</summary>
    public static Found.Badge OffList(IReadOnlyList<Conversation> conversations) =>
        Of(DestinationKind.Messages, conversations.Count(conversation => conversation.Unread));

    /// <summary>The Requests badge, off the requests listed: every one, since a request waits until it is answered.</summary>
    public static Found.Badge OffList(IReadOnlyList<Account> asking) => Of(DestinationKind.Requests, asking.Count);

    /// <summary>
    ///     A badge of <paramref name="unread" />, which is full where it reached what one read counts up to — there
    ///     may be more past it, and the badge says so rather than claiming an exact number it cannot know.
    /// </summary>
    private static Found.Badge Of(DestinationKind kind, int unread) =>
        new(kind, unread, Full: unread >= Arrival.CountedAtMost);
}
