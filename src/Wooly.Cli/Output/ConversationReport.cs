using Spectre.Console;
using Wooly.Core.Accounts;
using Wooly.Core.Conversations;
using Wooly.Core.Posts;

namespace Wooly.Cli.Output;

/// <summary>
///     Writes direct conversations for a person to read. The posts in them are written by
///     <see cref="PostReport.Write" /> rather than here, so that a direct message read in a thread and the same message
///     read anywhere else in this client cannot come to look like two different things.
///     <para>
///         Everything that came from an instance is written as text rather than markup: a display name is the account's
///         own, and a square bracket in one is not a colour tag.
///     </para>
/// </summary>
internal static class ConversationReport
{
    /// <summary>Writes the conversations, one after another with a blank line between them.</summary>
    public static void Write(IAnsiConsole console, ConversationFetch fetch)
    {
        if (fetch.Conversations.Count == 0)
        {
            // Only when there really are none. A listing a rate limit stopped before anything arrived is reported as
            // that failure, and saying "no conversations" as well would be saying the opposite of what happened.
            if (fetch.IsComplete)
            {
                console.MarkupLine("No direct conversations.");
            }

            return;
        }

        foreach (var conversation in fetch.Conversations)
        {
            Heading(console, conversation);

            if (conversation.Latest is { } latest)
            {
                PostReport.Write(console, latest);
            }
            else
            {
                // A conversation whose posts have all been taken down. Saying so is worth a line: the alternative is a
                // heading with nothing under it, which reads as output that went wrong.
                console.MarkupLine("  [dim]nothing left in this conversation[/]");
            }

            console.WriteLine();
        }
    }

    /// <summary>Writes one conversation in full: the heading, then everything said in it, oldest first.</summary>
    public static void WriteThread(IAnsiConsole console, ConversationThread thread)
    {
        Heading(console, thread.Conversation);
        console.WriteLine();

        if (thread.Posts.Count == 0)
        {
            console.MarkupLine("Nothing left in this conversation.");

            return;
        }

        foreach (var post in thread.Posts)
        {
            PostReport.Write(console, post);
            console.WriteLine();
        }
    }

    /// <summary>Reports the message that has just been sent, and who it went to.</summary>
    /// <remarks>
    ///     Who it reached rather than what visibility it went out at: <c>dm send</c> can only send direct, so saying so
    ///     tells the sender nothing they did not type, while the recipient is what they want confirmed. The id is here
    ///     for the reason it is on every published post — it is how every later command names this one.
    /// </remarks>
    public static void Sent(IAnsiConsole console, AccountAddress account, Post sent)
    {
        console.MarkupLineInterpolated($"Sent [bold]{sent.Id}[/] to [bold]{account}[/].");

        console.WriteAddress(sent.Url);
    }

    /// <summary>Reports the conversation whose unread mark has just been cleared.</summary>
    public static void MarkedRead(IAnsiConsole console, Conversation conversation) =>
        console.MarkupLineInterpolated($"Marked conversation [bold]{conversation.Id}[/] as read.");

    /// <summary>
    ///     The line that names a conversation: its id, who it is with, and whether it is still unread.
    /// </summary>
    /// <remarks>
    ///     The id leads, for the reason a notification's does: it is the one thing on the line that cannot be worked
    ///     out from the rest of it, and the one thing <c>dm show</c> and <c>dm read</c> ask the user to type.
    /// </remarks>
    private static void Heading(IAnsiConsole console, Conversation conversation)
    {
        // Escaped by hand rather than interpolated, because the unread mark is markup this client is adding and the
        // rest of the line is text an instance gave it. An interpolated line would have to be one or the other.
        console.MarkupLine(
            $"[bold]{Markup.Escape(conversation.Id)}[/]  with {Markup.Escape(With(conversation))}"
            + (conversation.Unread ? "  [yellow]unread[/]" : string.Empty));
    }

    /// <summary>
    ///     Who the conversation is with. An instance says who a conversation is with rather than who is having it, so a
    ///     conversation with nobody in it is one whose only other account has been taken down — said out loud rather
    ///     than printed as an empty space.
    /// </summary>
    private static string With(Conversation conversation) =>
        conversation.With.Count == 0 ? "nobody" : string.Join(", ", conversation.With);
}
