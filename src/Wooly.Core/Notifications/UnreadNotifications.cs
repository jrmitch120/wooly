using Wooly.Core.Paging;

namespace Wooly.Core.Notifications;

/// <summary>
///     The notifications an account has not read: those newer than its <b>Read marker</b>, which the instance keeps and
///     every client of the account shares (CONTEXT.md, ADR-0027). Unread is never a flag on a notification — it is
///     where the notification stands against the marker.
/// </summary>
/// <param name="Marker">
///     The id of the newest notification the account has read, or <see langword="null" /> for an account that has
///     never had a marker — which has nothing this client can prove is unread, and so nothing unread.
/// </param>
/// <param name="Since">
///     The notifications newer than the marker, newest first, and whether the read ran to the end of what was asked
///     for. Empty where there is no marker.
/// </param>
public sealed record UnreadNotifications(string? Marker, Fetch<Notification> Since)
{
    /// <summary>What an account with no marker has unread: nothing.</summary>
    public static UnreadNotifications NoMarker { get; } = new(null, Fetch<Notification>.Complete([]));

    /// <summary>Whether the account has a marker at all.</summary>
    public bool HasMarker => Marker is not null;
}
