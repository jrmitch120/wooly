using Wooly.Core.Profiles;

namespace Wooly.Core.Posts;

/// <summary>
///     What a profile does with a post rather than to its text: boosts it, favorites it, pins it, takes any of those
///     back, and reads one post on its own. The narrow port ADR-0005 asks for over Mastonet's whole REST surface, and
///     the third alongside <see cref="Timelines.ITimelineReader" /> and <see cref="IPostAuthor" /> — front ends depend on this,
///     and their tests fake this rather than the network.
///     <para>
///         Reading one post lives here rather than in a port of its own because it is the same answer these marks give:
///         every call below hands back the post as it now stands, and a caller that wants that without changing anything
///         is asking the same question. A second port over the same endpoint returning the same
///         <see cref="Post" /> would be a seam with no decision behind it.
///     </para>
/// </summary>
public interface IPostEngagement
{
    /// <summary>Puts <paramref name="mark" /> on the post <paramref name="postId" /> names, or takes it off.</summary>
    /// <param name="wanted">
    ///     Whether the mark should end up on the post. Nothing here reads the post first to find out what it already
    ///     carries: whether asking twice is harmless is the instance's to answer, and it answers differently by mark —
    ///     boosting and favoriting something already boosted or favorited pass, where pinning something already pinned
    ///     is refused. This client does not paper over that difference, because doing so would mean holding a copy of
    ///     each instance's rules and getting them wrong quietly.
    /// </param>
    /// <returns>
    ///     The post that was marked, as it now stands — the post <paramref name="postId" /> named, never the boost that
    ///     carries it. A caller asked about one post and is answered about that post, so the id it gets back is the id
    ///     everything else names that post by.
    /// </returns>
    Task<Post> Mark(
        ActiveProfile profile,
        string postId,
        PostMark mark,
        bool wanted,
        CancellationToken cancellationToken);

    /// <summary>Reads the single post <paramref name="postId" /> names.</summary>
    /// <returns>
    ///     The post as the instance has it. A post that is a boost stays a boost here — the caller named it, so it is
    ///     what they get, shown the way a timeline shows one.
    /// </returns>
    Task<Post> Show(ActiveProfile profile, string postId, CancellationToken cancellationToken);

    /// <summary>Reads what has been said in answer to the post <paramref name="postId" /> names.</summary>
    /// <remarks>
    ///     Alongside <see cref="Show" /> rather than folded into it, because a caller that already has the post — which
    ///     is every caller that got here by pressing enter on one — would otherwise pay for reading it a second time.
    ///     A screen showing a post with its replies makes one call, not two.
    ///     <para>
    ///         What comes back is the whole subtree flattened, oldest first, and not just the direct answers: that is
    ///         the shape Mastodon serves and the shape a thread reads in. A reply to a reply is still an answer to the
    ///         post, and dropping it would show a conversation with its middle missing.
    ///     </para>
    /// </remarks>
    /// <returns>The replies, or an empty list where nothing has answered it.</returns>
    Task<IReadOnlyList<Post>> Replies(ActiveProfile profile, string postId, CancellationToken cancellationToken);
}
