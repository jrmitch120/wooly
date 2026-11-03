namespace Wooly.Core.Posts;

/// <summary>
///     Something an account can put on a post and take back off it again. The four of them are separate marks rather
///     than degrees of one thing: boosting re-shares a post without saying anything about liking it, favoriting says the
///     opposite, pinning is about where a post sits on the account's own profile, and bookmarking keeps it to come back
///     to without anybody else hearing of it (GLOSSARY.md).
///     <para>
///         Written as four marks that are on or off rather than eight acts, because un-boosting is not its own thing to
///         do — it is boosting undone, and modelling it as a separate act is how a client comes to boost through one
///         code path and un-boost through another that ages differently.
///     </para>
/// </summary>
public enum PostMark
{
    /// <summary>Re-shared to the account's own followers.</summary>
    Boost,

    /// <summary>Marked as liked, without re-sharing it.</summary>
    Favorite,

    /// <summary>Held at the top of the account's own profile. Only an account's own posts can carry this.</summary>
    Pin,

    /// <summary>
    ///     Kept to come back to. Seen by nobody but the account that kept it: the author is not told and no count
    ///     moves.
    /// </summary>
    Bookmark,
}
