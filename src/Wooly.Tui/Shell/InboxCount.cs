using Wooly.Core.Accounts;
using Wooly.Core.Conversations;
using Wooly.Core.Notifications;
using Wooly.Core.Paging;
using Wooly.Core.Profiles;

namespace Wooly.Tui.Shell;

/// <summary>
///     How each <b>Inbox</b> destination's badge is counted: its <b>Unread count</b>, in the instance's own terms for
///     each (GLOSSARY.md, ADR-0027). Said once, here, because a badge is read from two places — a re-read of the counts,
///     and arriving at the destination itself — and the two must not come to disagree (#396).
/// </summary>
/// <remarks>
///     Two of the three are counted off the very list their screen draws, so arriving there costs nothing more than
///     the list; <see cref="Read" /> reads that list the same way (<see cref="Conversations" />,
///     <see cref="Requests" />) and counts it the same way. Notifications is the one counted apart from its list: what
///     is unread is what is newer than the read marker, which the list read does not say, so it is its own read — the
///     same one on a re-read and on arriving.
///     <para>
///         One of these is one count: through which ports, as whom, from which session markers and under which token
///         — the four things every read here needs, said once rather than handed down through each call.
///     </para>
/// </remarks>
/// <param name="ports">What the count is read through.</param>
/// <param name="profile">Who it is read as.</param>
/// <param name="sessionMarkers">
///     Where an account with no read marker counts from this session, started by the first read of it (story 8).
/// </param>
/// <param name="cancellationToken">What calls the read off.</param>
internal sealed class InboxCount(
    ShellPorts ports,
    ActiveProfile profile,
    SessionMarkers sessionMarkers,
    CancellationToken cancellationToken)
{
    /// <summary>The Inbox's three destinations, in the rail's order — which is the order they are read in, too.</summary>
    public static IReadOnlyList<DestinationKind> Kinds { get; } =
        [DestinationKind.Notifications, DestinationKind.Messages, DestinationKind.Requests];

    /// <summary>
    ///     Reads the badge for <paramref name="kind" />, and what the read found waiting there — for the screen in
    ///     front to put at its top whatever of it is new (#400).
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Not one of the Inbox's three.</exception>
    public async Task<Waiting> Read(DestinationKind kind)
    {
        switch (kind)
        {
            case DestinationKind.Notifications:
                var unread = await Unread();

                return new Waiting(unread.Badge, new InboxList.Notifications(unread.Unread));

            case DestinationKind.Messages:
                var listed = (await Conversations(ports, profile, cancellationToken)).Items;

                return new Waiting(OffList(listed), new InboxList.Messages(listed));

            case DestinationKind.Requests:
                var asking = (await Requests(ports, profile, cancellationToken)).Items;

                return new Waiting(OffList(asking), new InboxList.Requests(asking));

            default:
                throw new ArgumentOutOfRangeException(nameof(kind), kind, "Not a destination in the Inbox.");
        }
    }

    /// <summary>
    ///     The Messages list: the conversations, most recently spoken in first, as many as are counted. The one read
    ///     arriving at Messages and a re-read of its count both make, so that the two cannot come to read it apart.
    /// </summary>
    public static Task<Fetch<Conversation>> Conversations(
        ShellPorts ports,
        ActiveProfile profile,
        CancellationToken cancellationToken) =>
        ports.Messages.List(profile, Arrival.CountedAtMost, cancellationToken);

    /// <summary>
    ///     The Requests list: whoever is waiting for an answer, each with where the profile stands with them. Whether
    ///     you already follow the person asking may be the most useful thing on the row, and the requests endpoint
    ///     sends no standing — so it is asked for as part of the one read arriving at Requests and a re-read of its
    ///     count both make, and a request put on top live is drawn the same as one arrived at (#204).
    /// </summary>
    public static async Task<Fetch<Account>> Requests(
        ShellPorts ports,
        ActiveProfile profile,
        CancellationToken cancellationToken)
    {
        var asking = await ports.Accounts.PendingRequests(profile, Arrival.CountedAtMost, cancellationToken);

        return asking with { Items = await ports.StoodOrSilent(profile, asking.Items, cancellationToken) };
    }

    /// <summary>
    ///     The Notifications badge on arriving there, which reads them: the read marker is moved to the newest of
    ///     <paramref name="shown" />, so the badge clears as the reader looks — and on the web too, the marker being the
    ///     instance's (ADR-0027, #397).
    /// </summary>
    /// <remarks>
    ///     Counted from the session marker where the account has no read marker — so arriving draws unread the same
    ///     notifications the badge counted (stories 8 and 11).
    /// </remarks>
    /// <param name="shown">The notification groups the screen is drawing, newest first.</param>
    /// <returns>
    ///     The badge — what is still waiting once the marker has moved: anything that arrived between the list read and
    ///     this one, newer than everything shown; or, where the move was not made, what was waiting before it, which is
    ///     still true — and the keys of the groups with anything newer than the old marker in them, to be drawn unread
    ///     for this visit. A group's newest notification says that for the whole group (#453).
    /// </returns>
    public async Task<Tally> Arrived(IReadOnlyList<NotificationGroup> shown)
    {
        var unread = await Unread();
        var wasUnread = shown.Where(group => unread.Counts(group.NewestId))
                             .Select(group => group.Key)
                             .ToHashSet(StringComparer.Ordinal);

        // Moved only where it moves past something unread on screen — or sets a marker the account never had. Nothing
        // unread is nothing to ask the instance for, and a marker already past the newest shown would be moved back.
        var reads = shown.Count > 0 && (!unread.HasMarker || wasUnread.Count > 0);

        if (!reads || !await ports.Notifications.MoveMarker(profile, shown[0].NewestId, cancellationToken))
        {
            return new Tally(unread.Badge, wasUnread);
        }

        // The newest group's newest notification is the newest shown, so what is newer still — and heads no group on
        // screen — arrived after the read. Anything older is in a group on screen, and read now the marker is past it.
        var drawn = shown.Select(group => group.NewestId).ToHashSet(StringComparer.Ordinal);
        var waiting = unread.Unread
                            .Where(notification => !drawn.Contains(notification.Id)
                                                   && NotificationIds.Newer(notification.Id, shown[0].NewestId))
                            .ToList();

        return new Tally(
            Of(DestinationKind.Notifications, waiting.Count, waiting.FirstOrDefault()?.Id),
            wasUnread);
    }

    /// <summary>The Messages badge on arriving there: counted off the list arrived with, which is all it costs.</summary>
    public Task<Tally> Arrived(IReadOnlyList<Conversation> listed) => Task.FromResult(new Tally(OffList(listed)));

    /// <summary>The Requests badge on arriving there: counted off the list arrived with, which is all it costs.</summary>
    public Task<Tally> Arrived(IReadOnlyList<Account> asking) => Task.FromResult(new Tally(OffList(asking)));

    /// <summary>
    ///     The notifications newer than the read marker — or, for an account with none, newer than its session marker,
    ///     where this session started counting it (story 8): nothing unread on the first read, which only sets it, and
    ///     whatever arrived after it on every read since. The account's marker is not touched; the reader has looked
    ///     at nothing.
    /// </summary>
    /// <remarks>
    ///     One more is asked for than is counted, so that a read of exactly as many as are counted is known to hold
    ///     everything unread — which is what lets a re-read put all of it on screen and move the marker past it.
    /// </remarks>
    private async Task<UnreadRead> Unread()
    {
        const int past = Arrival.CountedAtMost + 1;

        var unread = await ports.Notifications.ReadUnread(profile, past, cancellationToken);

        if (unread.HasMarker)
        {
            return UnreadRead.Of(unread.Since.Items, hasMarker: true, from: unread.Marker);
        }

        if (sessionMarkers.For(profile) is { } marker)
        {
            var since = marker.After is { } after
                ? await ports.Notifications.ReadNewer(profile, after, past, cancellationToken)
                : await ports.Notifications.Read(profile, past, cancellationToken);

            return UnreadRead.Of(since.Items, hasMarker: false, from: marker.After);
        }

        var newest = await ports.Notifications.Read(profile, 1, cancellationToken);

        sessionMarkers.Start(
            profile,
            newest.Items is [var first, ..] ? new SessionMarker(first.Id) : SessionMarker.FromTheBeginning);

        return UnreadRead.Nothing;
    }

    /// <summary>
    ///     The Messages badge, off the conversations listed: those the instance flags unread — the newest told apart
    ///     by its last post, since a conversation spoken in again keeps its own id.
    /// </summary>
    private static Found.Badge OffList(IReadOnlyList<Conversation> conversations)
    {
        var unread = conversations.Where(conversation => conversation.Unread).ToList();

        return Of(
            DestinationKind.Messages,
            unread.Count,
            unread.FirstOrDefault() is { } newest ? newest.Latest?.Id ?? newest.Id : null);
    }

    /// <summary>
    ///     The Requests badge, off the requests listed: every one, since a request waits until it is answered.
    /// </summary>
    private static Found.Badge OffList(IReadOnlyList<Account> asking) =>
        Of(DestinationKind.Requests, asking.Count, asking.FirstOrDefault()?.Id);

    /// <summary>
    ///     A badge of <paramref name="unread" />, which is full where it reached what one read counts up to — there
    ///     may be more past it, and the badge says so rather than claiming an exact number it cannot know.
    /// </summary>
    private static Found.Badge Of(DestinationKind kind, int unread, string? newest = null) =>
        new(kind, unread, Full: unread >= Arrival.CountedAtMost, Newest: newest);

    /// <summary>One read of the notifications unread.</summary>
    /// <param name="Unread">What is unread, newest first, up to what is counted.</param>
    /// <param name="HasMarker">Whether the account has a read marker of its own.</param>
    /// <param name="More">Whether more is unread past <paramref name="Unread" />, which no screen has been handed.</param>
    /// <param name="From">
    ///     The id it was counted from — the read marker, or the session marker — or <see langword="null" /> where every
    ///     notification counts, an account that had none when the session started.
    /// </param>
    /// <param name="Counting">Whether anything counts at all: not on the first read, which only starts the count.</param>
    private sealed record UnreadRead(
        IReadOnlyList<Notification> Unread,
        bool HasMarker,
        bool More,
        string? From,
        bool Counting)
    {
        /// <summary>What the first read of an account with no marker counts unread: nothing.</summary>
        public static UnreadRead Nothing { get; } = new([], HasMarker: false, More: false, From: null, Counting: false);

        /// <summary>
        ///     Whether the notification <paramref name="id" /> names is unread by this count — newer than where it was
        ///     counted from, however far past what <see cref="Unread" /> holds.
        /// </summary>
        public bool Counts(string id) => Counting && (From is null || NotificationIds.Newer(id, From));

        /// <summary>
        ///     The badge: how many, full only where more are unread past them — a read of exactly as many as are
        ///     counted holds them all, and says that many.
        /// </summary>
        public Found.Badge Badge =>
            new(DestinationKind.Notifications, Unread.Count, Full: More, Newest: Unread.FirstOrDefault()?.Id);

        /// <summary>What a read of one more than is counted came back with.</summary>
        public static UnreadRead Of(IReadOnlyList<Notification> read, bool hasMarker, string? from) =>
            new([.. read.Take(Arrival.CountedAtMost)], hasMarker, read.Count > Arrival.CountedAtMost, from, true);
    }

    /// <summary>What is waiting at an Inbox destination, as a re-read of its count found it.</summary>
    /// <param name="Badge">What the rail's badge says.</param>
    /// <param name="Found">What is waiting there now, for the screen in front to put at its top what is new.</param>
    public sealed record Waiting(Found.Badge Badge, InboxList Found);

    /// <summary>What arriving at an Inbox destination counted: its badge, and which of its rows to draw unread.</summary>
    /// <param name="Badge">What the rail's badge says.</param>
    /// <param name="Unread">
    ///     What names the rows to draw unread for this visit — a notification group's key — or
    ///     <see langword="null" /> for none. Only Notifications has any: arriving there moves the read marker past them, so the badge no longer says which were new, and
    ///     the screen does instead. A conversation carries its own unread flag, and a request waits until answered.
    /// </param>
    public sealed record Tally(Found.Badge Badge, IReadOnlySet<string>? Unread = null)
    {
        /// <summary>No row drawn unread.</summary>
        public static IReadOnlySet<string> NoneUnread { get; } = new HashSet<string>(StringComparer.Ordinal);
    }
}
