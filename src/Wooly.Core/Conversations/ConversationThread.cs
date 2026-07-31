using Wooly.Core.Posts;

namespace Wooly.Core.Conversations;

/// <summary>
///     One conversation and everything said in it, which is what reading a single conversation comes back with. A
///     listing carries only the last post of each — enough to tell one thread from another — and this is the shape that
///     carries the rest.
/// </summary>
public sealed record ConversationThread
{
    /// <summary>The conversation itself: its id, who it is with, and whether it is still unread.</summary>
    public required Conversation Conversation { get; init; }

    /// <summary>
    ///     Every post in it, oldest first, which is the order it was said in. A timeline is read newest first because a
    ///     reader is catching up; a conversation is read the way it happened, because a reply after the thing it answers
    ///     is the only order it makes sense in.
    /// </summary>
    public required IReadOnlyList<Post> Posts { get; init; }
}
