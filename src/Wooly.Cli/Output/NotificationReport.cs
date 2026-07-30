using Spectre.Console;
using Wooly.Core.Notifications;

namespace Wooly.Cli.Output;

/// <summary>
///     Writes notifications for a person to read: a line saying who did what and when, and — where there is one — the
///     post it was done to or on, written by <see cref="PostReport.Write" /> rather than here, so that a post read in a
///     notification and the same post read on a timeline cannot come to look like two different posts.
/// </summary>
internal static class NotificationReport
{
    /// <summary>
    ///     What each kind is called in a sentence about who did it. One table, so that the four kinds this client names
    ///     cannot come to be described in more than four ways.
    /// </summary>
    private static readonly Dictionary<NotificationKind, string> Words = new()
    {
        [NotificationKind.Mention] = "mentioned you",
        [NotificationKind.Follow] = "followed you",
        [NotificationKind.Boost] = "boosted your post",
        [NotificationKind.Favorite] = "favorited your post",
    };

    /// <summary>Writes what is waiting, one notification after another with a blank line between them.</summary>
    public static void Write(IAnsiConsole console, NotificationFetch fetch)
    {
        if (fetch.Notifications.Count == 0)
        {
            // Only when the inbox really is empty. A fetch a rate limit stopped before anything arrived is reported as
            // that failure, and saying "nothing waiting" as well would be saying the opposite of what happened.
            if (fetch.IsComplete)
            {
                console.MarkupLine("No notifications.");
            }

            return;
        }

        foreach (var notification in fetch.Notifications)
        {
            Write(console, notification);
        }
    }

    /// <summary>Reports the notification that has just been cleared.</summary>
    public static void Dismissed(IAnsiConsole console, string notificationId) =>
        console.MarkupLineInterpolated($"Dismissed notification [bold]{notificationId}[/].");

    /// <summary>Reports an inbox that has just been emptied.</summary>
    public static void Cleared(IAnsiConsole console) => console.MarkupLine("Cleared every notification.");

    /// <summary>Reports an inbox left as it was, because the person asked said no.</summary>
    public static void LeftAlone(IAnsiConsole console) => console.MarkupLine("Left your notifications alone.");

    private static void Write(IAnsiConsole console, Notification notification)
    {
        // The id leads, because it is the one thing on this line that cannot be worked out from the rest of it, and
        // the one thing the next command — notification dismiss — asks the user to type.
        console.MarkupLineInterpolated(
            $"[bold]{notification.Id}[/]  {notification.Account} {WhatWasDone(notification.Kind)}  [dim]{LocalMoment.Of(notification.ReceivedAt)}[/]");

        // A follow is somebody arriving rather than something they wrote, and has nothing to show underneath.
        if (notification.Post is not null)
        {
            PostReport.Write(console, notification.Post);
        }

        console.WriteLine();
    }

    /// <summary>
    ///     What the account did, said the way this project says it. A kind this client has no word for keeps the
    ///     instance's own word, which is more than it could say by dropping the notification altogether.
    /// </summary>
    private static string WhatWasDone(NotificationKind kind) =>
        Words.TryGetValue(kind, out var words) ? words : $"notified you ({kind.Name})";
}
