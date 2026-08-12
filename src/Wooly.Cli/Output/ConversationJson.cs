using Spectre.Console;
using Wooly.Core.Conversations;
using Wooly.Core.Paging;

namespace Wooly.Cli.Output;

/// <summary>
///     Writes conversations for another program to read. A listing goes in <see cref="ListDocument" />'s envelope, per
///     ADR-0007. A single conversation needs no such envelope, for the reason ADR-0008 gives about a single post —
///     there is no partial version of it.
/// </summary>
internal static class ConversationJson
{
    /// <summary>Writes the conversations the profile is in.</summary>
    public static void Write(IAnsiConsole console, Fetch<Conversation> fetch) =>
        ListDocument.Write(console, fetch, ConversationDocument.Of, "conversations");

    /// <summary>Writes one conversation and everything said in it.</summary>
    public static void WriteThread(IAnsiConsole console, ConversationThread thread) =>
        JsonOutput.Write(console, ConversationDocument.Of(thread));

    /// <summary>Writes one conversation on its own, as it stands.</summary>
    public static void WriteConversation(IAnsiConsole console, Conversation conversation) =>
        JsonOutput.Write(console, ConversationDocument.Of(conversation));
}
