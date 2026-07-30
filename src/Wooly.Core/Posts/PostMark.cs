namespace Wooly.Core.Posts;

/// <summary>
///     Something an account can put on a post and take back off it again. The three of them are separate marks rather
///     than degrees of one thing: boosting re-shares a post without saying anything about liking it, favoriting says the
///     opposite, and pinning is about where a post sits on the account's own profile (CONTEXT.md).
///     <para>
///         Written as three marks that are on or off rather than six acts, because un-boosting is not its own thing to
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
}
