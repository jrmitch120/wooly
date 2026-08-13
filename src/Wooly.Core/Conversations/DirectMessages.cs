using Mastonet;
using Wooly.Core.Errors;
using Wooly.Core.Paging;
using Wooly.Core.Posts;
using Wooly.Core.Profiles;

namespace Wooly.Core.Conversations;

/// <summary>
///     Reads conversations through Mastonet. Listing them pages through <see cref="PagedReading" />, the same loop a
///     timeline and an inbox are read down, so three lists cannot come to disagree about where a list ends.
///     <para>
///         Showing one is the part with a shape of its own, because Mastodon has no endpoint that serves a single
///         conversation by id. The only way to one is down the list, so the list is walked until the id turns up — and
///         it is walked no further than <see cref="ConversationsSearched" />, since a client that pages an account's
///         entire history looking for something that is not there is a rate limit spent on a typo. What comes back
///         from that walk is a conversation carrying only its last post, so the thread itself is read from that post's
///         context: one call for the conversation's place in the list, one for what was said in it.
///     </para>
///     Nothing here retries and nothing here waits. A conversation the instance has marked read is never marked again,
///     because ADR-0006 resends nothing an instance has already taken, and a rate limit is reported rather than slept
///     off.
/// </summary>
public sealed class DirectMessages(IMastodonClientFactory clientFactory) : IDirectMessages
{
    /// <summary>
    ///     The most conversations Mastodon serves in one call — a timeline's page rather than an inbox's, which is what
    ///     this endpoint allows. Asking for more than an endpoint gives makes every full page look short, which is the
    ///     one thing the paging loop reads as the end of a list.
    /// </summary>
    private const int PageSize = 40;

    /// <summary>
    ///     How far down the list of conversations an id is looked for. Far enough for anything a user is realistically
    ///     naming, and short enough that an id that names nothing costs a handful of calls rather than every
    ///     conversation the account has ever had. A <c>list</c> asked for more than this can print a conversation this
    ///     cannot then find, which is why the refusal says how far it looked rather than claiming the id is wrong.
    /// </summary>
    private const int ConversationsSearched = 200;

    /// <inheritdoc />
    public async Task<Fetch<Conversation>> List(ActiveProfile profile, int limit, CancellationToken cancellationToken)
    {
        var client = clientFactory.CreateClient(profile.Instance, profile.AccessToken);

        return await PagedReading.Collect(
            limit,
            PageSize,
            client.GetConversations,
            conversation => ConversationWire.ToConversation(conversation, profile.Instance),
            conversation => conversation.Id,
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task<ConversationThread> Show(
        ActiveProfile profile,
        string conversationId,
        CancellationToken cancellationToken)
    {
        var client = clientFactory.CreateClient(profile.Instance, profile.AccessToken);

        var read = await PagedReading.Collect(
            ConversationsSearched,
            PageSize,
            client.GetConversations,
            conversation => ConversationWire.ToConversation(conversation, profile.Instance),
            conversation => conversation.Id,
            cancellationToken,
            stopWhen: conversation => conversation.Id == conversationId);

        // A rate limit part way down the list is not "no such conversation": what stopped the search is reported as
        // itself, so a caller is never told an id is wrong when what happened is that the looking stopped.
        if (read.StoppedBy is not null)
        {
            throw read.StoppedBy;
        }

        var found = read.Items.FirstOrDefault(conversation => conversation.Id == conversationId)
                    ?? throw new UnknownConversationException(conversationId, ConversationsSearched);

        return new ConversationThread
        {
            Conversation = found,
            Posts = await Thread(client, found, profile.Instance, cancellationToken),
        };
    }

    /// <inheritdoc />
    public async Task<Conversation> MarkRead(
        ActiveProfile profile,
        string conversationId,
        CancellationToken cancellationToken)
    {
        // Mastonet's own calls take no cancellation token, so a Ctrl-C lands before the call rather than during it.
        cancellationToken.ThrowIfCancellationRequested();

        var client = clientFactory.CreateClient(profile.Instance, profile.AccessToken);

        // Named by the conversation's id and reached without the list being walked first, unlike Show: marking a
        // conversation read needs nothing about it except that the instance knows the id, and an id it does not know
        // is a refusal the instance words better than a search through the list could.
        return ConversationWire.ToConversation(await client.MarkAsRead(conversationId), profile.Instance);
    }

    /// <summary>
    ///     Everything said in <paramref name="conversation" />, oldest first, read from the context of the last post in
    ///     it.
    /// </summary>
    /// <remarks>
    ///     A conversation with no last post has had everything in it taken down; there is no post to ask the context
    ///     of, and no call is made. Descendants are asked for as well as ancestors even though the post being asked
    ///     about is the conversation's latest: what an instance has already delivered and what it has listed are two
    ///     different moments, and a reply that arrived between them belongs in the thread rather than below the fold.
    ///     <para>
    ///         What this cannot reach is a second thread in the same conversation. Mastodon groups a conversation by
    ///         who is in it rather than by what answers what, so two messages to the same account that answer nothing
    ///         share a conversation while sharing no context — and the API has no call for "the posts of conversation
    ///         X". The newest thread is what a reader wants nearly always, and the rest is reachable by post id.
    ///     </para>
    /// </remarks>
    private static async Task<IReadOnlyList<Post>> Thread(
        IMastodonClient client,
        Conversation conversation,
        string instance,
        CancellationToken cancellationToken)
    {
        if (conversation.Latest is not { } latest)
        {
            return [];
        }

        cancellationToken.ThrowIfCancellationRequested();

        var context = await client.GetStatusContext(latest.Id);

        return
        [
            .. context.Ancestors.Select(status => PostWire.ToPost(status, instance)),
            latest,
            .. context.Descendants.Select(status => PostWire.ToPost(status, instance)),
        ];
    }
}
