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

    /// <summary>Orders post ids as the numbers Mastodon makes them, though they travel as strings.</summary>
    private static readonly Comparer<string> ByIdAsNumber = Comparer<string>.Create((one, other) =>
        one.Length != other.Length ? one.Length.CompareTo(other.Length) : string.CompareOrdinal(one, other));

    /// <summary>
    ///     This thread with <paramref name="reply" /> among its replies, placed where the instance would place it on
    ///     the next read — or <see langword="null" /> where it answers nothing the thread holds, and so is not this
    ///     thread's to take in (#482).
    /// </summary>
    /// <remarks>
    ///     The replies held keep the order they were served in, and only the new one is placed. Mastodon orders a
    ///     thread's answers over every post in it, the ones the viewer cannot see included, and leaves those out
    ///     afterwards — so a reply whose answered post is hidden from the viewer stands where that post would, which
    ///     nothing held here could work out again. The new reply goes where Mastodon puts it: depth first, each reply
    ///     followed by its own answers, and the answers to one post oldest first — so straight after the last of its
    ///     answered post's own answers — and then with every reply answering its own author lifted ahead of the rest,
    ///     so that a thread of your own reads ahead of everyone else's answers (#483). Placed rather than read again,
    ///     because a read would reach only the screen in front and a send reaches every screen holding the thread.
    ///     <para>
    ///         An ancestor is not the thread's to answer: a reply to something above the post is in another thread,
    ///         which the next read of this one would not show. A reply already held is put where it goes rather than
    ///         held twice. A reply held whose answered post the thread does not hold — one the instance left out — is
    ///         left where it was served, and the new reply goes ahead of any such that follow the place it is put.
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

        List<Post> replies = [.. Replies.Where(held => held.Id != reply.Id)];

        replies.Insert(Place(around, answered, replies, reply), reply);

        return this with { Replies = replies };
    }

    /// <summary>
    ///     Where among <paramref name="replies" /> the instance would put <paramref name="reply" />, which answers
    ///     <paramref name="answered" />: straight after the reply on its own side of the lift that comes last before it
    ///     depth first, or first on that side where none does.
    /// </summary>
    /// <remarks>
    ///     The lift keeps each side in depth-first order, so the reply goes after whichever of its own side would come
    ///     before it in a depth-first walk, found by walking back from it along the tree the replies held make: from a
    ///     reply to the last of its next older sibling's part, or to what it answers where it has no older sibling.
    ///     Only the replies whose answered post is held are on that tree; the rest are not walked, and stay put.
    /// </remarks>
    private static int Place(string around, string answered, List<Post> replies, Post reply)
    {
        var held = replies.DistinctBy(post => post.Id).ToDictionary(post => post.Id, StringComparer.Ordinal);
        var answers = replies
            .Where(post => post.InReplyTo?.PostId is { } target && (target == around || held.ContainsKey(target)))
            .ToLookup(post => post.InReplyTo!.PostId!, StringComparer.Ordinal);

        var lifted = AnswersItsOwnAuthor(reply);

        // Each walk is bounded by the replies there are, so that a chain looping back on itself, which no instance
        // serves but nothing here can rule out, ends.
        var steps = replies.Count;

        var before = Before(answered, reply.Id);

        while (before is not null && AnswersItsOwnAuthor(before) != lifted)
        {
            before = steps-- > 0 && before.InReplyTo?.PostId is { } above ? Before(above, before.Id) : null;
        }

        if (before is not null)
        {
            return replies.FindIndex(post => ReferenceEquals(post, before)) + 1;
        }

        var first = lifted ? 0 : replies.FindIndex(post => !AnswersItsOwnAuthor(post));

        return first >= 0 ? first : replies.Count;

        // What comes straight before the answer to target with this id, depth first: the last of the part of its
        // next older sibling, else target itself — or nothing, where target is the post or is not held.
        Post? Before(string target, string id)
        {
            var older = answers[target]
                .Where(sibling => ByIdAsNumber.Compare(sibling.Id, id) < 0)
                .MaxBy(sibling => sibling.Id, ByIdAsNumber);

            return older is not null ? LastOf(older) : target != around ? held.GetValueOrDefault(target) : null;
        }

        // The last post of the part a reply heads, depth first: its newest answer's last, all the way down.
        Post LastOf(Post post)
        {
            for (var down = replies.Count;
                 down > 0 && answers[post.Id].MaxBy(answer => answer.Id, ByIdAsNumber) is { } newest;
                 down--)
            {
                post = newest;
            }

            return post;
        }
    }

    /// <summary>
    ///     Whether <paramref name="reply" /> answers a post by whoever wrote it, which its answered account records
    ///     (<see cref="PostReplyTarget.Handle" />) — one of the replies Mastodon lifts ahead of the rest.
    /// </summary>
    private static bool AnswersItsOwnAuthor(Post reply) =>
        string.Equals(reply.InReplyTo?.Handle, reply.Account, StringComparison.Ordinal);
}
