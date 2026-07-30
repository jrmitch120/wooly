using Mastonet;
using Mastonet.Entities;
using Wooly.Core.Profiles;

namespace Wooly.Core.Posts;

/// <summary>
///     Marks and reads posts through Mastonet. Thin by design — an instance settles every question these calls raise,
///     including the ones this client could be tempted to answer itself: whether a post is already boosted, and whether
///     a post is the account's own to pin. Asking first would cost a round trip to arrive at a worse answer than the
///     instance's own, which knows its rules and says them in its own words.
///     <para>
///         The one thing not passed straight through is what a boost answers with. Mastodon's reblog endpoint hands back
///         the boost — a post of the booster's own, carrying the post that was boosted — so it is unwrapped here, and a
///         caller that asked about one post is answered about that post. Nowhere above this layer has to know that
///         boosting is the one mark that makes a post.
///     </para>
///     Nothing here retries and nothing here waits: these are writes, which ADR-0006 never resends, and a rate limit is
///     reported rather than slept off.
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
