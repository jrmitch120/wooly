using Mastonet;
using Mastonet.Entities;
using Wooly.Core.Errors;
using Wooly.Core.Profiles;

namespace Wooly.Core.Posts;

/// <summary>
///     Marks, reads and votes on posts through Mastonet. Thin by design — an instance settles every question these
///     calls raise, including the ones this client could be tempted to answer itself: whether a post is already
///     boosted, whether a post is the account's own to pin, and whether this account has already voted in a poll.
///     Asking first would cost a round trip to arrive at a worse answer than the instance's own, which knows its rules
///     and says them in its own words.
///     <para>
///         The one thing not passed straight through is what a boost answers with. Mastodon's reblog endpoint hands back
///         the boost — a post of the booster's own, carrying the post that was boosted — so it is unwrapped here, and a
///         caller that asked about one post is answered about that post. Nowhere above this layer has to know that
///         boosting is the one mark that makes a post.
///     </para>
///     Nothing here retries and nothing here waits. A mark the instance answered is never sent again, because ADR-0006
///     resends nothing an instance has already taken, and a rate limit is reported rather than slept off — for the read
///     as much as for the marks.
/// </summary>
public sealed class PostEngagement(IMastodonClientFactory clientFactory) : IPostEngagement
{
    /// <inheritdoc />
    public async Task<Post> Mark(
        ActiveProfile profile,
        string postId,
        PostMark mark,
        bool wanted,
        CancellationToken cancellationToken)
    {
        // Mastonet's own calls take no cancellation token, so a Ctrl-C lands before the call rather than during it.
        cancellationToken.ThrowIfCancellationRequested();

        var client = clientFactory.CreateClient(profile.Instance, profile.AccessToken);
        var marked = await Apply(client, postId, mark, wanted);

        // Boosting answers with the boost rather than the post boosted; every other mark answers with the post itself.
        return PostWire.ToPost(marked.Reblog ?? marked, profile.Instance);
    }

    /// <inheritdoc />
    public async Task<Post> Show(ActiveProfile profile, string postId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var client = clientFactory.CreateClient(profile.Instance, profile.AccessToken);

        return PostWire.ToPost(await client.GetStatus(postId), profile.Instance);
    }

    /// <inheritdoc />
    public async Task<PostThread> Thread(ActiveProfile profile, string postId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var client = clientFactory.CreateClient(profile.Instance, profile.AccessToken);

        // Mastodon answers with what came before the post as well as what came after it, on the one call. Both are
        // kept: what a post answers is drawn above it and what answered it below, and the chain arrives whole — as
        // far back as the instance knows the thread — rather than cut to the nearest parent (#86).
        var context = await client.GetStatusContext(postId);

        return new PostThread(Posts(context.Ancestors), Posts(context.Descendants));

        IReadOnlyList<Post> Posts(IEnumerable<Status> statuses) =>
            [.. statuses.Select(status => PostWire.ToPost(status, profile.Instance))];
    }

    /// <inheritdoc />
    public async Task<Post> Vote(
        ActiveProfile profile,
        Post post,
        IReadOnlyList<int> choices,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (post.Poll is not { } poll)
        {
            // A defect rather than a refusal: nothing above this can offer a vote it has no options to show, so a
            // post with no poll arriving here is a caller that lost track of what it was holding.
            throw new ArgumentException($"Post {post.Id} carries no poll to vote in.", nameof(post));
        }

        var client = clientFactory.CreateClient(profile.Instance, profile.AccessToken);

        try
        {
            // The poll's own id, not the post's: Mastodon votes on the poll. And the answer is the whole poll as it
            // now stands, so the post the caller already holds is brought up to date from it rather than read again.
            return post with { Poll = PostWire.ToPoll(await client.Vote(poll.Id, choices)) };
        }
        catch (ServerErrorException refusal)
        {
            // The one refusal here that is turned into something this client names. Unlike a mark, a vote cannot be
            // tried again differently — the instance has settled it — and both front ends have to be able to say so
            // in the instance's own words rather than fall over on somebody else's exception type.
            throw new VoteRefusedException(refusal);
        }
    }

    /// <summary>
    ///     The one crossing between this client's three marks and Mastodon's six endpoints. Written out rather than
    ///     built from the mark's name, so that a mark renamed here cannot quietly start calling an endpoint that is not
    ///     there — or, worse, one that is.
    /// </summary>
    private static Task<Status> Apply(IMastodonClient client, string postId, PostMark mark, bool wanted) =>
        (mark, wanted) switch
        {
            (PostMark.Boost, true) => client.Reblog(postId),
            (PostMark.Boost, false) => client.Unreblog(postId),
            (PostMark.Favorite, true) => client.Favourite(postId),
            (PostMark.Favorite, false) => client.Unfavourite(postId),
            (PostMark.Pin, true) => client.Pin(postId),
            (PostMark.Pin, false) => client.Unpin(postId),
            _ => throw new ArgumentOutOfRangeException(nameof(mark), mark, "Not a mark this client puts on a post."),
        };
}
