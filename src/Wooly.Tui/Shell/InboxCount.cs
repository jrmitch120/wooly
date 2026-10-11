using Wooly.Core.Accounts;
using Wooly.Core.Conversations;
using Wooly.Core.Notifications;
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
    /// <summary>The Inbox's three destinations, in the rail's order — which is the order they are read in, too.</summary>
    public static IReadOnlyList<DestinationKind> Kinds { get; } =
        [DestinationKind.Notifications, DestinationKind.Messages, DestinationKind.Requests];

    /// <summary>
    ///     Reads the badge for <paramref name="kind" /> as <paramref name="profile" />, and what the read found waiting
    ///     there — for the screen in front to put at its top whatever of it is new (#400).
    /// </summary>
    /// <param name="standIns">
    ///     Where an account with no read marker counts from this session, started by the first read of it.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">Not one of the Inbox's three.</exception>
    public static async Task<Reading> Read(
        ShellPorts ports,
        ActiveProfile profile,
        DestinationKind kind,
        StandInMarkers standIns,
        CancellationToken cancellationToken)
    {
        switch (kind)
        {
            case DestinationKind.Notifications:
                var (unread, _) = await Unread(ports, profile, standIns, cancellationToken);

                return new Reading(Of(kind, unread.Count), new InboxList.Notifications(unread));

            case DestinationKind.Messages:
                var listed = (await ports.Messages.List(profile, Arrival.CountedAtMost, cancellationToken)).Items;

                return new Reading(OffList(listed), new InboxList.Messages(listed));

            case DestinationKind.Requests:
                var asking = (await ports.Accounts.PendingRequests(profile, Arrival.CountedAtMost, cancellationToken))
                    .Items;

                return new Reading(OffList(asking), new InboxList.Requests(asking));

            default:
                throw new ArgumentOutOfRangeException(nameof(kind), kind, "Not a destination in the Inbox.");
        }
    }

    /// <summary>
    ///     The notifications newer than the read marker — or, for an account with none, newer than where this session
    ///     started counting it (story 8): nothing unread on the first read, which is only where counting starts, and
    ///     whatever arrived after it on every read since. The account's marker is not touched; the reader has looked
    ///     at nothing.
    /// </summary>
    /// <returns>What is unread, and whether the account has a marker of its own.</returns>
    private static async Task<(IReadOnlyList<Notification> Unread, bool HasMarker)> Unread(
        ShellPorts ports,
        ActiveProfile profile,
        StandInMarkers standIns,
        CancellationToken cancellationToken)
    {
        var unread = await ports.Notifications.ReadUnread(profile, Arrival.CountedAtMost, cancellationToken);

        if (unread.HasMarker)
        {
            return (unread.Since.Items, true);
        }

        if (standIns.For(profile) is { } from)
        {
            return ((await ports.Notifications.ReadNewer(profile, from, Arrival.CountedAtMost, cancellationToken))
                .Items, false);
        }

        var newest = await ports.Notifications.Read(profile, 1, cancellationToken);

        standIns.Start(profile, newest.Items is [var first, ..] ? first.Id : StandInMarkers.BeforeEverything);

        return ([], false);
    }

    /// <summary>
    ///     The Notifications badge on arriving there, which reads them: the read marker is moved to the newest of
    ///     <paramref name="shown" />, so the badge clears as the reader looks — and on the web too, the marker being the
    ///     instance's (ADR-0027, #397).
    /// </summary>
    /// <param name="shown">The notifications the screen is drawing, newest first.</param>
    /// <param name="standIns">
    ///     Where an account with no read marker counts from this session — so arriving draws unread the same
    ///     notifications the badge counted (stories 8 and 11).
    /// </param>
    /// <returns>
    ///     The badge — what is still waiting once the marker has moved: anything that arrived between the list read and
    ///     this one, newer than everything shown; or, where the move was not made, what was waiting before it, which is
    ///     still true — and the notifications that were newer than the old marker, to be drawn unread for this visit.
    /// </returns>
    public static async Task<Tally> Arrived(
        ShellPorts ports,
        ActiveProfile profile,
        IReadOnlyList<Notification> shown,
        StandInMarkers standIns,
        CancellationToken cancellationToken)
    {
        var (unread, hasMarker) = await Unread(ports, profile, standIns, cancellationToken);
        var waiting = Of(DestinationKind.Notifications, unread.Count);
        var drawn = shown.Select(notification => notification.Id).ToHashSet(StringComparer.Ordinal);
        var wasUnread = unread.Select(notification => notification.Id).ToHashSet(StringComparer.Ordinal);

        // Moved only where it moves past something unread on screen — or sets a marker the account never had. Nothing
        // unread is nothing to ask the instance for, and a marker already past the newest shown would be moved back.
        var reads = shown.Count > 0 && (!hasMarker || wasUnread.Overlaps(drawn));

        if (!reads || !await ports.Notifications.MoveMarker(profile, shown[0].Id, cancellationToken))
        {
            return new Tally(waiting, wasUnread);
        }

        return new Tally(
            Of(DestinationKind.Notifications, wasUnread.Count(id => !drawn.Contains(id))),
            wasUnread);
    }

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

    /// <summary>What re-reading an Inbox destination's count came back with.</summary>
    /// <param name="Badge">What the rail's badge says.</param>
    /// <param name="Found">What is waiting there now, for the screen in front to put at its top what is new.</param>
    public sealed record Reading(Found.Badge Badge, InboxList Found);

    /// <summary>What arriving at an Inbox destination counted: its badge, and which of its rows to draw unread.</summary>
    /// <param name="Badge">What the rail's badge says.</param>
    /// <param name="Unread">
    ///     The ids of the rows to draw unread for this visit, or <see langword="null" /> for none. Only Notifications
    ///     has any: arriving there moves the read marker past them, so the badge no longer says which were new, and
    ///     the screen does instead. A conversation carries its own unread flag, and a request waits until answered.
    /// </param>
    public sealed record Tally(Found.Badge Badge, IReadOnlySet<string>? Unread = null)
    {
        /// <summary>No row drawn unread.</summary>
        public static IReadOnlySet<string> NoneUnread { get; } = new HashSet<string>(StringComparer.Ordinal);
    }
}
