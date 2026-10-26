namespace Wooly.Core.Posts;

/// <summary>
///     What stands either side of one post on its instance: the chain it answers, back to the root of the thread, and
///     everything said in answer to it. One thing rather than two lists a caller carries about separately, because
///     Mastodon serves both halves on one call and a screen showing a post draws both (#86).
/// </summary>
/// <remarks>
///     The post itself is not on it. Whoever asks for a thread already holds the post they asked about — that is how
///     they came to name it — and putting a copy in the middle would give a screen two answers to which post it is
///     about, one of which is a round trip older than the other.
/// </remarks>
/// <param name="Ancestors">
///     What the post answers, root first and its immediate parent last, uncapped. Empty where it answers nothing, or
///     where what it answered is no longer there for the instance to send.
/// </param>
/// <param name="Replies">
///     What has been said in answer to it, the whole subtree flattened, which is the shape Mastodon serves and the
///     shape a thread reads in: depth first and oldest first, with every reply answering its own author lifted ahead
///     of the rest.
/// </param>
public sealed record PostThread(IReadOnlyList<Post> Ancestors, IReadOnlyList<Post> Replies)
{
    /// <summary>A post standing on its own: nothing above it and nothing under it.</summary>
    public static PostThread Alone { get; } = new([], []);

    /// <summary>How two post ids order: as the numbers Mastodon makes them, though they travel as strings.</summary>
    private static readonly Comparer<string> Oldest = Comparer<string>.Create((one, other) =>
        one.Length != other.Length ? one.Length.CompareTo(other.Length) : string.CompareOrdinal(one, other));

    /// <summary>
    ///     This thread with <paramref name="reply" /> among its replies, placed where the instance would place it on the
    ///     next read — or <see langword="null" /> where it answers nothing the thread holds, and so is not this thread's
    ///     to take in (#482).
    /// </summary>
    /// <remarks>
    ///     Worked out from every reply held plus the new one, in the order Mastodon serves a thread's answers: depth
    ///     first, each reply followed by its own answers, and the answers to one post oldest first — so a new reply
    ///     goes straight after the last of its answered post's own answers — and then every reply answering its own
    ///     author lifted ahead of the rest, so that a thread of your own reads ahead of everyone else's answers. Placed rather than read again, because a
    ///     read would reach only the screen in front and a send reaches every screen holding the thread.
    ///     <para>
    ///         An ancestor is not the thread's to answer: a reply to something above the post is in another thread,
    ///         which the next read of this one would not show. A reply already held is put where it goes rather than
    ///         held twice. A reply whose answered post the thread does not hold — one the instance left out — is taken
    ///         as answering the post itself, the nearest place there is for it.
    ///     </para>
    /// </remarks>
    /// <param name="around">The id of the post this thread is around, which the thread does not hold.</param>
    /// <param name="reply">The reply just sent.</param>
    public PostThread? TakeIn(string around, Post reply)
    {
        if (reply.InReplyTo?.PostId is not { } answered
            || (answered != around && Replies.All(held => held.Id != answered)))
        {
            return null;
        }

        return this with
        {
            Replies = Lifted(DepthFirst(around, [.. Replies.Where(held => held.Id != reply.Id), reply])),
        };
    }

    /// <summary>
    ///     <paramref name="replies" /> with every one that answers its own author lifted ahead of every one that doesn't,
    ///     each side keeping its order — Mastodon's own last step on a thread's answers (#483).
    /// </summary>
    /// <remarks>
    ///     Mastodon moves each such reply up to where the first reply answering someone else stands, which comes to
    ///     the same as this. A self-reply is lifted on its own, away from the reply it answers where that one is not
    ///     lifted too, because that is how the instance serves it.
    /// </remarks>
    private static List<Post> Lifted(List<Post> replies) =>
        [.. replies.Where(AnswersItsOwnAuthor), .. replies.Where(reply => !AnswersItsOwnAuthor(reply))];

    /// <summary>
    ///     Whether <paramref name="reply" /> answers a post by whoever wrote it, which its answered account records
    ///     (<see cref="PostReplyTarget.Handle" />).
    /// </summary>
    private static bool AnswersItsOwnAuthor(Post reply) =>
        string.Equals(reply.InReplyTo?.Handle, reply.Account, StringComparison.Ordinal);

    /// <summary>
    ///     <paramref name="replies" /> in depth-first order under <paramref name="around" />, each followed by its own
    ///     answers, and the answers to one post oldest first.
    /// </summary>
    private static List<Post> DepthFirst(string around, IReadOnlyList<Post> replies)
    {
        var held = replies.Select(reply => reply.Id).ToHashSet(StringComparer.Ordinal);
        var answers = replies.ToLookup(reply =>
            reply.InReplyTo?.PostId is { } answered && held.Contains(answered) ? answered : around);

        var order = new List<Post>(replies.Count);
        var placed = new HashSet<string>(StringComparer.Ordinal);

        Walk(around);

        // Nothing is lost to a chain that loops back on itself, which no instance serves but nothing here can rule out.
        order.AddRange(replies.Where(reply => !placed.Contains(reply.Id)));

        return order;

        void Walk(string answered)
        {
            foreach (var answer in answers[answered].OrderBy(answer => answer.Id, Oldest))
            {
                if (placed.Add(answer.Id))
                {
                    order.Add(answer);
                    Walk(answer.Id);
                }
            }
        }
    }
}
