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
            var handle when handle == post.Account => "↳ continuing",
            var handle => $"↳ answering @{handle}",
        };
    }
}
