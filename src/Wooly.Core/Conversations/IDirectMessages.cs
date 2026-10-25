using Wooly.Core.Paging;
using Wooly.Core.Profiles;

namespace Wooly.Core.Conversations;

/// <summary>
///     The direct conversations a profile is in: the ones it has, one of them in full, and marking one read. The narrow
///     port ADR-0005 asks for over Mastonet's whole REST surface, alongside <see cref="Timelines.ITimelineReader" />,
///     <see cref="Posts.IPostAuthor" /> and <see cref="Notifications.INotificationInbox" /> — front ends depend on this,
///     and their tests fake this rather than the network.
/// </summary>
/// <remarks>
///     Nothing here sends a message, and that absence is the shape of this feature rather than an omission (ADR-0013).
///     A direct message is a post that went out direct, so sending one is <see cref="Posts.IPostAuthor.Publish" /> with
///     the audience settled — the same call, the same validation, the same attachments. A <c>Send</c> here would be a
///     second way to compose a post, and the second way is the one that ends up missing the newest option.
/// </remarks>
public interface IDirectMessages
{
    /// <summary>Lists up to <paramref name="limit" /> conversations, most recently spoken in first.</summary>
    /// <param name="profile">The profile to read as — which instance to ask, and the token to ask with.</param>
    /// <param name="limit">
    ///     How many conversations are wanted. More than one page's worth is fetched by paging, which the caller never
    ///     sees (ADR-0007).
    /// </param>
    /// <returns>
    ///     The conversations, and whether the listing ran to the end of what was asked for — a rate limit part way
    ///     through stops it and is reported alongside what had already arrived, rather than losing it.
    /// </returns>
    Task<Fetch<Conversation>> List(ActiveProfile profile, int limit, CancellationToken cancellationToken);

    /// <summary>
    ///     Reads on: up to <paramref name="limit" /> more conversations, from where a listing said to read on from — the
    ///     ones spoken in longer ago than the last it held, never anything newer (#466).
    /// </summary>
    /// <param name="profile">The profile to read as.</param>
    /// <param name="from">Where to read on from, as an earlier listing said.</param>
    /// <param name="limit">How many more conversations are wanted, paged as <see cref="List" /> pages.</param>
    /// <returns>
    ///     The conversations, and where to read on from next unless the listing is now whole — reported as
    ///     <see cref="List" /> reports, a rate limit included.
    /// </returns>
    Task<Fetch<Conversation>> ReadOn(
        ActiveProfile profile,
        Onward from,
        int limit,
        CancellationToken cancellationToken);

    /// <summary>Reads one conversation: the thread its last post belongs to, oldest first.</summary>
    /// <param name="conversationId">
    ///     The conversation's own id, as a listing reports it — not the id of any post in it.
    /// </param>
    /// <remarks>
    ///     "The thread its last post belongs to" is narrower than "everything in the conversation", and the difference
    ///     is Mastodon's rather than this client's. An instance groups a conversation by who is in it, not by what
    ///     answers what, so two messages sent to the same account that answer nothing are one conversation holding two
    ///     unrelated threads — and the API offers no way to ask for the posts of a conversation, only for the context of
    ///     a post. What comes back is therefore the newest thread; an older one is still reachable by its own post id
    ///     through <c>post show</c>. See ADR-0013.
    /// </remarks>
    /// <exception cref="Errors.UnknownConversationException">
    ///     No conversation this profile has recently is named by that id.
    /// </exception>
    Task<ConversationThread> Show(ActiveProfile profile, string conversationId, CancellationToken cancellationToken);

    /// <summary>Clears the unread mark on the conversation <paramref name="conversationId" /> names.</summary>
    /// <remarks>
    ///     The conversation is what carries the mark, so the conversation is what takes it off. Reading the posts in it
    ///     does not: <see cref="Show" /> leaves the mark exactly as it found it, because a client that cleared it on
    ///     the way past would make "what have I not read" unanswerable for anything that looked.
    /// </remarks>
    /// <returns>The conversation as it now stands, so a caller can say what was marked rather than which id was.</returns>
    Task<Conversation> MarkRead(ActiveProfile profile, string conversationId, CancellationToken cancellationToken);
}
