using Wooly.Core.Posts;
using Person = Wooly.Core.Accounts.Account;

namespace Wooly.Tui.Shell;

/// <summary>
///     What happened on the instance, as a verb reports it: a post changed, gone or sent, a tie made, something taken
///     off an inbox (CONTEXT.md, #234).
/// </summary>
/// <remarks>
///     A closed set of values that say what happened and nothing about what it makes stale. That is
///     <see cref="Arrival.Apply" />'s one table — which cached lists go, which badge moves — and each screen's own
///     <see cref="Screens.Screen.Heard" />; a verb that decided either for itself could only ever decide it about the
///     destination showing, which is how a post marked on Home stayed unmarked on a Local list held a minute.
///     <para>
///         Reported whether or not the reader is still where the verb was pressed, because a change is true of the
///         instance rather than of where anybody is standing.
///     </para>
/// </remarks>
public abstract record Change
{
    private Change()
    {
    }

    /// <summary>A post as the instance now has it, after a mark, an edit or a vote.</summary>
    public sealed record PostChanged(Post Post) : Change;

    /// <summary>A post deleted, by its id.</summary>
    public sealed record PostGone(string Id) : Change;

    /// <summary>A post just published, a reply included.</summary>
    /// <param name="Post">The post as the instance published it.</param>
    /// <param name="Conversation">
    ///     The id of the conversation it was written in, where it was a reply written inside one. Carried because an
    ///     instance says nothing on a post about which conversation it is in, and the list a thread was opened from
    ///     knows only each conversation's last post — so a reply to anything older would reach no row at all.
    /// </param>
    public sealed record PostSent(Post Post, string? Conversation = null) : Change;

    /// <summary>An account as the instance now has it, after a tie went on or came off.</summary>
    public sealed record Tied(Person Account) : Change;

    /// <summary>Notifications dismissed, by their own ids.</summary>
    public sealed record NotificationsGone(IReadOnlyList<string> Ids) : Change;

    /// <summary>Every notification cleared at once.</summary>
    public sealed record AllNotificationsGone : Change;

    /// <summary>A follow request accepted or turned away, by the id of whoever asked.</summary>
    public sealed record RequestAnswered(string Id) : Change;

    /// <summary>A suggestion the instance has been told to stop making, by the id of whoever it suggested.</summary>
    public sealed record SuggestionDismissed(string Id) : Change;

    /// <summary>A conversation that was unread, as the instance has it now it is marked read.</summary>
    public sealed record ConversationMarked(Core.Conversations.Conversation Conversation) : Change;
}
