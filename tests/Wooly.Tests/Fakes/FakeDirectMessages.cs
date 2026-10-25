using Wooly.Core.Conversations;
using Wooly.Core.Errors;
using Wooly.Core.Paging;
using Wooly.Core.Profiles;

namespace Wooly.Tests.Fakes;

/// <summary>
///     A profile's direct conversations without the instance. ADR-0005's primary seam for anything above the API layer:
///     a command test says what conversations there are and then asks what was listed, shown or marked read, and never
///     fakes HTTP to do it.
/// </summary>
internal sealed class FakeDirectMessages(
    Fetch<Conversation> fetch,
    ConversationThread? thread = null,
    Exception? refusal = null) : IDirectMessages
{
    private Fetch<Conversation> _fetch = fetch;

    /// <summary>Every conversation there is, where the listing reads on (<see cref="ReadingOn" />).</summary>
    private IReadOnlyList<Conversation>? _onward;

    /// <summary>The access token every call was made with, in order — where a test proves who it was made as.</summary>
    public List<string> Tokens { get; } = [];

    /// <summary>Every listing it was asked for, in order — where a test proves what a command went looking for.</summary>
    public List<Call> Listings { get; } = [];

    /// <summary>Every read on it was asked for, in order — where a test proves where a read went on from.</summary>
    public List<OnwardCall> ReadsOn { get; } = [];

    /// <summary>Every conversation it was asked to show, in order.</summary>
    public List<Named> Shown { get; } = [];

    /// <summary>Every conversation it was asked to mark read, in order.</summary>
    public List<Named> MarkedRead { get; } = [];

    /// <summary>Conversations there to be listed, read to the end of whatever was asked for.</summary>
    public static FakeDirectMessages Holding(params Conversation[] conversations) =>
        new(Fetch<Conversation>.Complete(conversations));

    /// <summary>
    ///     Conversations there to be listed, most recently spoken in first, which read on (#466): every listing hands
    ///     out as many as were asked for and where to read on from, until they run out, where the listing is whole.
    /// </summary>
    public static FakeDirectMessages ReadingOn(IReadOnlyList<Conversation> conversations) =>
        new(Fetch<Conversation>.Complete([])) { _onward = conversations };

    /// <summary>An instance whose rate limit stopped the listing with <paramref name="conversations" /> in hand.</summary>
    public static FakeDirectMessages RateLimitedAfter(params Conversation[] conversations) =>
        new(Fetch<Conversation>.StoppedShort(
            conversations,
            new RateLimitedException("mastodon.social", new DateTimeOffset(2026, 7, 29, 13, 0, 0, TimeSpan.Zero))));

    /// <summary>An instance answering a request to show one conversation with <paramref name="thread" />.</summary>
    public static FakeDirectMessages Threading(ConversationThread thread) =>
        new(Fetch<Conversation>.Complete([thread.Conversation]), thread);

    /// <summary>An instance that refuses everything with <paramref name="refusal" />, having recorded the attempt.</summary>
    public static FakeDirectMessages Refusing(Exception refusal) =>
        new(Fetch<Conversation>.Complete([]), thread: null, refusal);

    /// <summary>
    ///     What every listing holds from here on — what arrived while the reader was elsewhere, which a poll is asked to
    ///     notice (<see cref="FakeNotificationInbox.NowHolding" /> for notifications).
    /// </summary>
    /// <remarks>Read on from, too, where the listing reads on.</remarks>
    public void NowHolding(params Conversation[] conversations)
    {
        if (_onward is not null)
        {
            _onward = conversations;
        }
        else
        {
            _fetch = Fetch<Conversation>.Complete(conversations);
        }
    }

    public Task<Fetch<Conversation>> List(ActiveProfile profile, int limit, CancellationToken cancellationToken)
    {
        Listings.Add(new Call(profile.Name, limit));
        Tokens.Add(profile.AccessToken);

        return refusal is not null ? Task.FromException<Fetch<Conversation>>(refusal)
            : _onward is { } all ? Task.FromResult(OnwardRun.Of(all, conversation => conversation.Id, null, limit))
            : Task.FromResult(_fetch);
    }

    public Task<Fetch<Conversation>> ReadOn(
        ActiveProfile profile,
        Onward from,
        int limit,
        CancellationToken cancellationToken)
    {
        ReadsOn.Add(new OnwardCall(from, limit));
        Tokens.Add(profile.AccessToken);

        // A listing nobody said reads on has nothing behind what it handed out, which is a list read whole.
        return refusal is not null ? Task.FromException<Fetch<Conversation>>(refusal)
            : Task.FromResult(_onward is { } all
                ? OnwardRun.Of(all, conversation => conversation.Id, from, limit)
                : Fetch<Conversation>.Complete([]));
    }

    public Task<ConversationThread> Show(
        ActiveProfile profile,
        string conversationId,
        CancellationToken cancellationToken)
    {
        Shown.Add(new Named(profile.Name, conversationId));
        Tokens.Add(profile.AccessToken);

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
        Tokens.Add(profile.AccessToken);

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
