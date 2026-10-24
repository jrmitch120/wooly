using Wooly.Core.Accounts;
using Wooly.Core.Conversations;
using Wooly.Core.Notifications;

namespace Wooly.Tui.Shell;

/// <summary>
///     What a re-read of one <b>Inbox</b> destination's count came back with, besides the count: what is waiting there
///     now, newest first — for the screen in front to put at its top whatever of it is new (GLOSSARY.md, ADR-0027, #400).
/// </summary>
/// <remarks>
///     A closed set, one case per destination, told to the screen in front and to nothing else
///     (<see cref="Screens.Screen.PutOnTop" />): a screen not in front is told nothing, and its cached list is read again
///     on the next arrival, as any list is. Every re-read is one of these — the poll's and the stream's alike — so
///     anything that re-reads a count puts what it found on screen without saying so itself.
/// </remarks>
public abstract record InboxList
{
    private InboxList()
    {
    }

    /// <summary>The notifications newer than the read marker, newest first.</summary>
    /// <param name="Unread">What is unread, newest first, up to what is counted.</param>
    /// <param name="Groups">
    ///     The first page of <b>Notification groups</b> as the instance groups them now, newest first — read only where
    ///     something was unread and Notifications was in front to put it on, so that an arrival joins the group it
    ///     belongs in (#458) — or <see langword="null" /> where they were not read.
    /// </param>
    public sealed record Notifications(
        IReadOnlyList<Notification> Unread,
        IReadOnlyList<NotificationGroup>? Groups = null) : InboxList;

    /// <summary>The conversations, most recently spoken in first, each with its unread flag as the instance has it.</summary>
    public sealed record Messages(IReadOnlyList<Conversation> Listed) : InboxList;

    /// <summary>Whoever is waiting for a follow request to be answered, in the order the instance listed them.</summary>
    public sealed record Requests(IReadOnlyList<Account> Asking) : InboxList;
}
