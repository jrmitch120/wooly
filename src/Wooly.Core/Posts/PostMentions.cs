namespace Wooly.Core.Posts;

/// <summary>
///     Who a handle written in a post's text is, as far as the post itself says — the crossing from the <c>@maria</c>
///     a reader walked to in <see cref="Post.Content" /> to the account an instance can be asked about (#85).
/// </summary>
/// <remarks>
///     Off <see cref="Post.Mentions" /> rather than out of a fetch: an instance sends everyone a post names along with
///     the post, so the account is already in hand — and a handle written bare is only somebody in particular because
///     the post says so.
/// </remarks>
public static class PostMentions
{
    /// <summary>
    ///     The account <paramref name="written" /> names, in full, or <see langword="null" /> where
    ///     <paramref name="post" /> names nobody by it.
    /// </summary>
    /// <param name="post">
    ///     The post the handle was written in. A boost is read as the post it boosts, which is where its text — and so
    ///     the handle — came from.
    /// </param>
    /// <param name="written">The handle as it appears in the text, with or without the <c>@</c> it is written with.</param>
    /// <remarks>
    ///     Two accounts with the same username on different instances are told apart by nothing a bare handle carries,
    ///     so the first the post lists wins — the order the instance sent them in. Refusing to answer instead would
    ///     turn the common case (a post naming one <c>@maria</c>) into no answer for the sake of the rare one.
    /// </remarks>
    public static string? Named(Post post, string? written)
    {
        var handle = written?.Trim().TrimStart('@');

        if (string.IsNullOrEmpty(handle))
        {
            return null;
        }

        var mentions = (post.Boosted ?? post).Mentions;

        // Written in full: the one form that means the same account read from two instances, so it is matched first
        // and matched whole.
        var named = mentions.FirstOrDefault(
            mention => string.Equals(mention, handle, StringComparison.OrdinalIgnoreCase));

        if (named is not null || handle.Contains('@'))
        {
            // A handle that named an instance and matched nobody is nobody: the instance it named is the instance it
            // meant, and no username on its own can stand in for that.
            return named;
        }

        return mentions.FirstOrDefault(
            mention => string.Equals(Username(mention), handle, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>The username half of a handle, which is all a bare mention has to go on.</summary>
    private static string Username(string mention) => mention.Split('@')[0];
}
