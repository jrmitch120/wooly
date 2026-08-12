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

    /// <summary>Clears the single notification <paramref name="notificationId" /> names.</summary>
    /// <param name="notificationId">
    ///     The notification's own id, as a read reports it — not the id of the post it is about.
    /// </param>
    Task Dismiss(ActiveProfile profile, string notificationId, CancellationToken cancellationToken);

    /// <summary>Clears every notification this profile has, in one call.</summary>
    /// <remarks>
    ///     One call rather than a dismissal each: an account with a hundred notifications would otherwise spend a
    ///     hundred requests against the instance's rate limit to empty a list Mastodon empties in one.
    /// </remarks>
    Task Clear(ActiveProfile profile, CancellationToken cancellationToken);
}
