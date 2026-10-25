using Wooly.Core.Paging;
using Wooly.Core.Profiles;

namespace Wooly.Core.Notifications;

/// <summary>
///     What is waiting for a profile, and how it is cleared: read the notifications, dismiss one, or clear the lot. The
///     narrow port ADR-0005 asks for over Mastonet's whole REST surface, alongside
///     <see cref="Timelines.ITimelineReader" />, <see cref="Posts.IPostAuthor" /> and <see cref="Posts.IPostEngagement" /> —
///     front ends depend on this, and their tests fake this rather than the network.
///     <para>
///         Reading and clearing sit on one port because they are one inbox: the id a caller dismisses is the id the read
///         handed it, and a screen that lists notifications is the same screen that clears them. Splitting them would put
///         a seam through the middle of a single conversation with an instance.
///     </para>
/// </summary>
public interface INotificationInbox
{
    /// <summary>Reads up to <paramref name="limit" /> notifications, newest first.</summary>
    /// <param name="profile">The profile to read as — which instance to ask, and the token to ask with.</param>
    /// <param name="limit">
    ///     How many notifications are wanted. More than one page's worth is fetched by paging, which the caller never
    ///     sees (ADR-0007).
    /// </param>
    /// <returns>
    ///     The notifications, and whether the fetch ran to the end of what was asked for — a rate limit part way through
    ///     stops it and is reported alongside what had already arrived, rather than losing it.
    /// </returns>
    Task<Fetch<Notification>> Read(ActiveProfile profile, int limit, CancellationToken cancellationToken);

    /// <summary>
    ///     Reads up to <paramref name="limit" /> <b>Notification groups</b>, newest first, grouped as the instance groups
    ///     them — or, on an instance too old to group, every notification a group of one (ADR-0032).
    /// </summary>
    /// <param name="profile">The profile to read as.</param>
    /// <param name="limit">
    ///     How many groups are wanted. More than one page's worth is fetched by following the instance's link to the
    ///     next page, which the caller never sees (ADR-0007).
    /// </param>
    /// <returns>The groups, and whether a rate limit stopped the read part way, as <see cref="Read" /> reports it.</returns>
    Task<Fetch<NotificationGroup>> ReadGroups(ActiveProfile profile, int limit, CancellationToken cancellationToken);

    /// <summary>
    ///     Reads up to <paramref name="limit" /> of the <b>Actors</b> of the notification group <paramref name="groupKey" />
    ///     names, newest first, from the group's own accounts: exactly the group the instance counted, and not everyone
    ///     who has ever done the same to its post (#455).
    /// </summary>
    /// <param name="profile">The profile to read as.</param>
    /// <param name="groupKey">The group's key, as <see cref="ReadGroups" /> handed it over.</param>
    /// <param name="limit">
    ///     How many actors are wanted. More than one page's worth is fetched by following the instance's link to the
    ///     next page, which the caller never sees (ADR-0007).
    /// </param>
    /// <returns>The actors, and whether a rate limit stopped the read part way, as <see cref="Read" /> reports it.</returns>
    Task<Fetch<Accounts.Account>> ReadGroupActors(
        ActiveProfile profile,
        string groupKey,
        int limit,
        CancellationToken cancellationToken);

    /// <summary>
    ///     Reads on: up to <paramref name="limit" /> more of the <b>Actors</b> of the group <paramref name="groupKey" />
    ///     names, from where a <see cref="ReadGroupActors" /> of it said to read on from (#467).
    /// </summary>
    /// <param name="profile">The profile to read as.</param>
    /// <param name="groupKey">The group <paramref name="from" /> was handed out for.</param>
    /// <param name="from">Where to read on from, as an earlier read of the group's actors said.</param>
    /// <param name="limit">How many more actors are wanted, paged as <see cref="ReadGroupActors" /> pages.</param>
    /// <returns>
    ///     The actors, and where to read on from next unless the group is now whole — reported as
    ///     <see cref="ReadGroupActors" /> reports, a rate limit included.
    /// </returns>
    Task<Fetch<Accounts.Account>> ReadGroupActorsOn(
        ActiveProfile profile,
        string groupKey,
        Onward from,
        int limit,
        CancellationToken cancellationToken);

    /// <summary>
    ///     Reads up to <paramref name="limit" /> of the notifications newer than the account's read marker, newest
    ///     first — every kind, as <see cref="Read" /> reads them (ADR-0010) — or learns that the account has no marker,
    ///     and so nothing unread (ADR-0027).
    /// </summary>
    /// <param name="profile">The profile to read as.</param>
    /// <param name="limit">
    ///     The most that are wanted. A read that comes back with this many is all the caller can say for certain:
    ///     there may be more past it.
    /// </param>
    Task<UnreadNotifications> ReadUnread(ActiveProfile profile, int limit, CancellationToken cancellationToken);

    /// <summary>
    ///     Reads up to <paramref name="limit" /> of the notifications newer than <paramref name="notificationId" />,
    ///     newest first, every kind — the read <see cref="ReadUnread" /> makes past the marker, from an id the caller
    ///     names instead. What an account with no marker counts what arrives from (ADR-0027).
    /// </summary>
    /// <param name="profile">The profile to read as.</param>
    /// <param name="notificationId">The notification to read from, by its own id; it is not among what comes back.</param>
    /// <param name="limit">The most that are wanted; a read that comes back with this many may have more past it.</param>
    Task<Fetch<Notification>> ReadNewer(
        ActiveProfile profile,
        string notificationId,
        int limit,
        CancellationToken cancellationToken);

    /// <summary>
    ///     Moves the account's read marker to <paramref name="notificationId" />, so that it and everything older count
    ///     as read — for every client of the account, since the marker is the instance's (ADR-0027). Nothing is
    ///     dismissed: every notification stays where a read finds it.
    /// </summary>
    /// <param name="profile">The profile whose marker it is.</param>
    /// <param name="notificationId">The newest notification read, by its own id.</param>
    /// <returns>
    ///     Whether the instance took the move. One it turned down, or never heard because a rate limit or the network
    ///     stopped it, is <see langword="false" /> rather than a failure: the marker only clears a badge, and a badge
    ///     left standing is the whole of what went wrong.
    /// </returns>
    Task<bool> MoveMarker(ActiveProfile profile, string notificationId, CancellationToken cancellationToken);

    /// <summary>Clears the single notification <paramref name="notificationId" /> names.</summary>
    /// <param name="notificationId">
    ///     The notification's own id, as a read reports it — not the id of the post it is about.
    /// </param>
    Task Dismiss(ActiveProfile profile, string notificationId, CancellationToken cancellationToken);

    /// <summary>
    ///     Clears every notification in <paramref name="group" />, in one call, by the group's key — or, on an instance
    ///     too old to group, where every group is a single notification, that notification by its own id (ADR-0032).
    /// </summary>
    /// <param name="group">The group as <see cref="ReadGroups" /> handed it over.</param>
    Task DismissGroup(ActiveProfile profile, NotificationGroup group, CancellationToken cancellationToken);

    /// <summary>Clears every notification this profile has, in one call.</summary>
    /// <remarks>
    ///     One call rather than a dismissal each: an account with a hundred notifications would otherwise spend a
    ///     hundred requests against the instance's rate limit to empty a list Mastodon empties in one.
    /// </remarks>
    Task Clear(ActiveProfile profile, CancellationToken cancellationToken);
}
