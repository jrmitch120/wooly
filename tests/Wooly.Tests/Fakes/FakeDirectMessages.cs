using Wooly.Core.Conversations;
using Wooly.Core.Errors;
using Wooly.Core.Profiles;

namespace Wooly.Tests.Fakes;

/// <summary>
///     A profile's direct conversations without the instance. ADR-0005's primary seam for anything above the API layer:
///     a command test says what conversations there are and then asks what was listed, shown or marked read, and never
///     fakes HTTP to do it.
/// </summary>
internal sealed class FakeDirectMessages(
    ConversationFetch fetch,
    ConversationThread? thread = null,
    Exception? refusal = null) : IDirectMessages
{
    /// <summary>Every listing it was asked for, in order — where a test proves what a command went looking for.</summary>
    public List<Call> Listings { get; } = [];

    /// <summary>Every conversation it was asked to show, in order.</summary>
    public List<Named> Shown { get; } = [];

    /// <summary>Every conversation it was asked to mark read, in order.</summary>
    public List<Named> MarkedRead { get; } = [];

    /// <summary>Conversations there to be listed, read to the end of whatever was asked for.</summary>
    public static FakeDirectMessages Holding(params Conversation[] conversations) =>
        new(ConversationFetch.Complete(conversations));

    /// <summary>An instance whose rate limit stopped the listing with <paramref name="conversations" /> in hand.</summary>
    public static FakeDirectMessages RateLimitedAfter(params Conversation[] conversations) =>
        new(ConversationFetch.StoppedShort(
            conversations,
            new RateLimitedException("mastodon.social", new DateTimeOffset(2026, 7, 29, 13, 0, 0, TimeSpan.Zero))));

    /// <summary>An instance answering a request to show one conversation with <paramref name="thread" />.</summary>
    public static FakeDirectMessages Threading(ConversationThread thread) =>
        new(ConversationFetch.Complete([thread.Conversation]), thread);

    /// <summary>An instance that refuses everything with <paramref name="refusal" />, having recorded the attempt.</summary>
    public static FakeDirectMessages Refusing(Exception refusal) =>
        new(ConversationFetch.Complete([]), thread: null, refusal);

    public Task<ConversationFetch> List(ActiveProfile profile, int limit, CancellationToken cancellationToken)
    {
        Listings.Add(new Call(profile.Name, limit));

        return refusal is null ? Task.FromResult(fetch) : Task.FromException<ConversationFetch>(refusal);
    }

    public Task<ConversationThread> Show(
        ActiveProfile profile,
        string conversationId,
        CancellationToken cancellationToken)
    {
        Shown.Add(new Named(profile.Name, conversationId));

        if (refusal is not null)
        {
            return Task.FromException<ConversationThread>(refusal);
        }

        return Task.FromResult(thread ?? AConversation.Thread());
    }

    public Task<Conversation> MarkRead(
        ActiveProfile profile,
        string conversationId,
        CancellationToken cancellationToken)
    {
        MarkedRead.Add(new Named(profile.Name, conversationId));

        if (refusal is not null)
        {
            return Task.FromException<Conversation>(refusal);
        }

        // What the instance answers with is the conversation as it now stands, which is the point of marking it.
        return Task.FromResult(AConversation.With(id: conversationId, unread: false));
    }

    /// <summary>One listing: which profile it was made as, and how many conversations it wanted.</summary>
    internal sealed record Call(string Profile, int Limit);

    /// <summary>One act on a single conversation: which profile made it, and which conversation it named.</summary>
    internal sealed record Named(string Profile, string ConversationId);
}
