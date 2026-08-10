namespace Wooly.Core.Posts;

/// <summary>
///     What a reply says it answers, in the one place every entry point can reach it. Two of them draw this mark —
///     the CLI's post report and the TUI's row above a byline — and it is the same fact about the same post, so the
///     two cannot be allowed to say it differently (#63).
/// </summary>
public static class PostReplyName
{
    /// <summary>
    ///     The mark for <paramref name="post" />, or <see langword="null" /> where it answers nothing.
    /// </summary>
    /// <remarks>
    ///     Three things it can say, settled by comparing the answered handle against the post's own account: the
    ///     common case names the account, a self-reply says the author is continuing their own thread instead, and an
    ///     answered account the post does not itself name falls back to the bare fact of replying. Costs no lookup —
    ///     the handle was resolved off the post's mentions as it crossed the wire (<see cref="PostWire" />), so a
    ///     whole timeline of replies is named for free.
    /// </remarks>
    public static string? Of(Post post)
    {
        if (post.InReplyTo is not { } answered)
        {
            return null;
        }

        return answered.Handle switch
        {
            null => "↳ reply",
            var handle => Answering(handle, mine: handle == post.Account),
        };
    }

    /// <summary>
    ///     The mark for a reply to <paramref name="handle" />, said by whoever already knows whose post is being
    ///     answered rather than working it back off a wire field.
    /// </summary>
    /// <remarks>
    ///     Split out for the compose screen (#82), which holds the whole post it answers and is told by
    ///     <c>Shell.IsMine</c> whether it is the profile's own — so it can say this without a post whose
    ///     <see cref="Post.InReplyTo" /> has been filled in, which a reply still being written has not got. The bare
    ///     "↳ reply" of <see cref="Of" /> has no caller here: that one is for an answered account a post does not
    ///     itself name, and compose is never missing the name.
    /// </remarks>
    /// <param name="handle">Whose post is being answered.</param>
    /// <param name="mine">Whether that post is the answering account's own, which makes this a thread continued.</param>
    public static string Answering(string handle, bool mine) => mine ? "↳ continuing" : $"↳ answering @{handle}";
}
