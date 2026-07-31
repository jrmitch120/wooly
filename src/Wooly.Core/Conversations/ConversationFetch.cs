using Wooly.Core.Errors;

namespace Wooly.Core.Conversations;

/// <summary>
///     What a listing of conversations came back with, and whether that is all of what was asked for. ADR-0007's second
///     decision, inherited the same way a timeline and an inbox inherit it: a listing a rate limit stopped part way
///     through may hold nothing at all, and a caller unable to tell that from an account with nobody writing to it
///     would report "no conversations" to somebody with plenty.
/// </summary>
public sealed record ConversationFetch
{
    /// <summary>The conversations that arrived, most recently spoken in first.</summary>
    public required IReadOnlyList<Conversation> Conversations { get; init; }

    /// <summary>
    ///     The rate limit that cut the listing short, or <see langword="null" /> if nothing did. Held as the exception
    ///     itself so a front end that treats this as a failure — the CLI does, per ADR-0006 — can throw the instance's
    ///     own answer rather than a second-hand copy of it.
    /// </summary>
    public required RateLimitedException? StoppedBy { get; init; }

    /// <summary>Whether this is everything the caller asked for, as far as the conversations go.</summary>
    public bool IsComplete => StoppedBy is null;

    /// <summary>A listing that ran to the end of what was asked for.</summary>
    public static ConversationFetch Complete(IReadOnlyList<Conversation> conversations) =>
        new() { Conversations = conversations, StoppedBy = null };

    /// <summary>A listing the instance's rate limit stopped, holding whatever had already arrived.</summary>
    public static ConversationFetch StoppedShort(
        IReadOnlyList<Conversation> conversations,
        RateLimitedException rateLimit) =>
        new() { Conversations = conversations, StoppedBy = rateLimit };
}
