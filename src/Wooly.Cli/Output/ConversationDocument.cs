using System.Text.Json.Serialization;
using Wooly.Core.Conversations;

namespace Wooly.Cli.Output;

/// <summary>
///     How a conversation is spelled for another program to read. The field names below are a contract with whatever is
///     parsing them, which is why they are written out here rather than derived from <see cref="Conversation" /> — a
///     rename in the domain must not silently rename somebody's <c>jq</c> filter.
///     <para>
///         One record, shared by listing conversations, showing one and marking one read, so a script never has to know
///         which command wrote the conversation it is reading. <c>posts</c> is the one field that depends on which: a
///         listing carries only the last post of each conversation, in <c>latest</c>, while showing one carries the
///         whole thread and no <c>latest</c> — the last post is in the thread already, and saying it twice would leave a
///         reader wondering whether the two were the same post.
///     </para>
/// </summary>
/// <param name="Unread">Whether anything in it has arrived since the profile last read it.</param>
/// <param name="With">The other accounts in it, as <c>username@instance</c>.</param>
internal sealed record ConversationDocument(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("unread")] bool Unread,
    [property: JsonPropertyName("with")] IReadOnlyList<string> With,
    [property: JsonPropertyName("latest")] PostDocument? Latest,
    [property: JsonPropertyName("posts")] IReadOnlyList<PostDocument>? Posts)
{
    /// <summary>How <paramref name="conversation" /> is written down where only its last post is in hand.</summary>
    public static ConversationDocument Of(Conversation conversation) => new(
        conversation.Id,
        conversation.Unread,
        conversation.With,
        conversation.Latest is null ? null : PostDocument.Of(conversation.Latest),
        Posts: null);

    /// <summary>How <paramref name="thread" /> is written down: the conversation, and everything said in it.</summary>
    public static ConversationDocument Of(ConversationThread thread) => new(
        thread.Conversation.Id,
        thread.Conversation.Unread,
        thread.Conversation.With,
        Latest: null,
        thread.Posts.Select(PostDocument.Of).ToList());
}
