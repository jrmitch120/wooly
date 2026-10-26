using Wooly.Core.Paging;
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

    /// <summary>
    ///     Reads the thread the post <paramref name="postId" /> names stands in: what it answers, and what has been
    ///     said in answer to it.
    /// </summary>
    /// <remarks>
    ///     Alongside <see cref="Show" /> rather than folded into it, because a caller that already has the post — which
    ///     is every caller that got here by pressing enter on one — would otherwise pay for reading it a second time.
    ///     A screen showing a post with its thread around it makes one call, not two.
    ///     <para>
    ///         Both halves rather than the answers alone, because the instance sends both for the one call either way:
    ///         a caller that wanted what a post answers used to have to ask again for what it already had thrown away
    ///         (#86).
    ///     </para>
    ///     <para>
    ///         What comes back below the post is the whole subtree flattened, oldest first, and not just the direct
    ///         answers: that is the shape Mastodon serves and the shape a thread reads in. A reply to a reply is still
    ///         an answer to the post, and dropping it would show a conversation with its middle missing.
    ///     </para>
    /// </remarks>
    /// <returns>
    ///     The thread around the post, whose two halves are empty rather than absent where nothing answered it and it
    ///     answers nothing.
    /// </returns>
    Task<PostThread> Thread(ActiveProfile profile, string postId, CancellationToken cancellationToken);

    /// <summary>Casts a vote in the poll <paramref name="post" /> carries.</summary>
    /// <remarks>
    ///     The post rather than its id, which is the one call here that takes one — because Mastodon votes on the
    ///     <em>poll</em>, whose id is not the post's and is only knowable from the post itself. A caller holding an id
    ///     alone reads the post first (<see cref="Show" />); one that already has it, which is every reader who can see
    ///     the options they are voting on, pays for nothing it already had.
    ///     <para>
    ///         Nothing is checked first. Whether the poll is still open, whether this account has already voted, and
    ///         whether more than one answer may be chosen are all the instance's to settle — and it refuses a second
    ///         vote outright rather than replacing the first, which is why a front end asks before sending one.
    ///     </para>
    /// </remarks>
    /// <param name="post">
    ///     The post carrying the poll, which is the post the answer is about. Never a boost: a caller holding one
    ///     names the post inside it, the same post every other call here answers about.
    /// </param>
    /// <param name="choices">
    ///     Which answers to cast, as indices into <see cref="PostPoll.Options" /> — the order the instance gave them
    ///     in, counted from zero. More than one only where the poll says so.
    /// </param>
    /// <returns>
    ///     The post with the poll as it now stands. Mastodon answers a vote with the complete updated poll, so the
    ///     post the caller passed in is brought up to date from that answer rather than read back a second time.
    /// </returns>
    /// <exception cref="Errors.VoteRefusedException">The instance would not take the vote, and said why.</exception>
    Task<Post> Vote(
        ActiveProfile profile,
        Post post,
        IReadOnlyList<int> choices,
        CancellationToken cancellationToken);

    /// <summary>
    ///     Reads up to <paramref name="limit" /> of one side of the post's <b>Actors</b>: who favorited it, or who
    ///     boosted it, as the instance lists them (#487).
    /// </summary>
    /// <remarks>
    ///     Here rather than beside the follow lists, because it is a question about a post: the post's own id is what
    ///     names it, and the answer is what favoriting and boosting it — the marks above — put there. The instance can
    ///     list only the actors it knows of, so the list may be shorter than the count the post carries.
    /// </remarks>
    /// <param name="postId">The post, never the boost carrying it: a boost has no favorites of its own worth asking for.</param>
    /// <param name="side">Which of the two lists.</param>
    /// <param name="limit">
    ///     How many actors are wanted. More than one page's worth is fetched by following the instance's link to the
    ///     next page, which the caller never sees (ADR-0007).
    /// </param>
    /// <returns>The actors, newest first, and where to read on from unless the list is whole.</returns>
    Task<Fetch<Accounts.Account>> ReadActors(
        ActiveProfile profile,
        string postId,
        ActorSide side,
        int limit,
        CancellationToken cancellationToken);

    /// <summary>
    ///     Reads on: up to <paramref name="limit" /> more of the same side of the same post's <b>Actors</b>, from where
    ///     a <see cref="ReadActors" /> of it said to read on from (#467).
    /// </summary>
    /// <param name="postId">The post <paramref name="from" /> was handed out for.</param>
    /// <param name="side">The side <paramref name="from" /> was handed out for.</param>
    /// <param name="from">Where to read on from, as an earlier read of this list said.</param>
    /// <param name="limit">How many more actors are wanted, paged as <see cref="ReadActors" /> pages.</param>
    /// <returns>
    ///     The actors, and where to read on from next unless the list is now whole — reported as
    ///     <see cref="ReadActors" /> reports, a rate limit included.
    /// </returns>
    Task<Fetch<Accounts.Account>> ReadActorsOn(
        ActiveProfile profile,
        string postId,
        ActorSide side,
        Onward from,
        int limit,
        CancellationToken cancellationToken);
}
