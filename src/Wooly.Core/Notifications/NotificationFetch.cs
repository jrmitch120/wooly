using Wooly.Core.Errors;

namespace Wooly.Core.Notifications;

/// <summary>
///     What a read of the notifications came back with, and whether that is all of what was asked for. ADR-0007's second
///     decision, inherited: a fetch a rate limit stopped part way through may hold nothing at all, and a caller unable to
///     tell that from an account with nothing waiting would report "no notifications" to somebody who has plenty.
/// </summary>
public sealed record NotificationFetch
{
    /// <summary>The notifications that arrived, newest first.</summary>
    public required IReadOnlyList<Notification> Notifications { get; init; }

    /// <summary>
    ///     The rate limit that cut the fetch short, or <see langword="null" /> if nothing did. Held as the exception
    ///     itself so a front end that treats this as a failure — the CLI does, per ADR-0006 — can throw the instance's
    ///     own answer rather than a second-hand copy of it.
    /// </summary>
    public required RateLimitedException? StoppedBy { get; init; }

    /// <summary>Whether this is everything the caller asked for, as far as the notifications go.</summary>
    public bool IsComplete => StoppedBy is null;

    /// <summary>A fetch that ran to the end of what was asked for.</summary>
    public static NotificationFetch Complete(IReadOnlyList<Notification> notifications) =>
        new() { Notifications = notifications, StoppedBy = null };

    /// <summary>A fetch the instance's rate limit stopped, holding whatever had already arrived.</summary>
    public static NotificationFetch StoppedShort(
        IReadOnlyList<Notification> notifications,
        RateLimitedException rateLimit) =>
        new() { Notifications = notifications, StoppedBy = rateLimit };
}
