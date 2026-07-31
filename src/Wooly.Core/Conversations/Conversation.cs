using Wooly.Core.Posts;

namespace Wooly.Core.Conversations;

/// <summary>
///     One direct-message thread as the instance keeps it: who it is with, whether anything in it is still unread, and
///     the last thing said in it.
///     <para>
///         A conversation is not a post and not a timeline. It has an id of its own — which is what shows it and what
///         marks it read — while what it holds are ordinary posts that happen to have gone out direct. Confusing the
///         two ids is the one mistake this noun exists to make impossible: marking a conversation read by the id of the
///         post in it clears nothing.
///     </para>
/// </summary>
public sealed record Conversation
{
    /// <summary>The instance's own id for the conversation, which is how it is shown and how it is marked read.</summary>
    public required string Id { get; init; }

    /// <summary>
    ///     The other accounts in it, as <c>username@instance</c>. The profile's own account is not among them: an
    ///     instance says who a conversation is with, not who is having it.
    /// </summary>
    public required IReadOnlyList<string> With { get; init; }

    /// <summary>Whether anything in it has arrived since the profile last read it.</summary>
    public required bool Unread { get; init; }

    /// <summary>
    ///     The last post in it, or <see langword="null" /> where the instance sent none — which is what a conversation
    ///     whose posts have all been deleted looks like. A listing has nothing to show for one of those but who it is
    ///     with, and that is worth showing rather than dropping: the conversation is still there to be read or removed.
    /// </summary>
    public Post? Latest { get; init; }
}
