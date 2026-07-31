using System.Text.Json.Serialization;
using Spectre.Console;
using Wooly.Core.Conversations;

namespace Wooly.Cli.Output;

/// <summary>
///     Writes conversations for another program to read. A listing is an object rather than a bare array, per ADR-0007:
///     one cut short by a rate limit and one with nothing in it would otherwise both be <c>[]</c>, and under a pipe the
///     exit code is gone by the time the JSON is parsed. A single conversation needs no such envelope, for the reason
///     ADR-0008 gives about a single post — there is no partial version of it.
/// </summary>
internal static class ConversationJson
{
    /// <summary>Writes the conversations the profile is in.</summary>
    public static void Write(IAnsiConsole console, ConversationFetch fetch) =>
        JsonOutput.Write(
            console,
            new ConversationListDocument(
                fetch.IsComplete,
                RateLimitDocument.Of(fetch.StoppedBy),
                fetch.Conversations.Select(ConversationDocument.Of).ToList()));

    /// <summary>Writes one conversation and everything said in it.</summary>
    public static void WriteThread(IAnsiConsole console, ConversationThread thread) =>
        JsonOutput.Write(console, ConversationDocument.Of(thread));

    /// <summary>Writes one conversation on its own, as it stands.</summary>
    public static void WriteOne(IAnsiConsole console, Conversation conversation) =>
        JsonOutput.Write(console, ConversationDocument.Of(conversation));

    /// <param name="Complete">
    ///     Whether every conversation asked for was read. False says the rest was cut short, which an empty
    ///     <c>conversations</c> otherwise could not be told from a profile nobody is writing to.
    /// </param>
    private sealed record ConversationListDocument(
        [property: JsonPropertyName("complete")] bool Complete,
        [property: JsonPropertyName("rateLimit")] RateLimitDocument? RateLimit,
        [property: JsonPropertyName("conversations")] IReadOnlyList<ConversationDocument> Conversations);
}
