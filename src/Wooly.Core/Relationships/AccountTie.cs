namespace Wooly.Core.Relationships;

/// <summary>
///     A tie the profile's own account can have with another one, each of which is on or off. Three ties rather than
///     six acts, for the reason ADR-0009 gives about a post's marks: un-following is not its own thing to do, it is
///     following undone, and modelling the six verbs as six methods is how <c>unmute</c> comes to behave unlike
///     <c>mute</c>.
/// </summary>
public enum AccountTie
{
    /// <summary>Reading an account's posts on the home timeline, and being counted among its followers.</summary>
    Follow,

    /// <summary>Refusing an account: it is unfollowed, cannot follow back, and neither sees the other.</summary>
    Block,

    /// <summary>Hiding an account without refusing it: still followed, still able to follow, simply not shown.</summary>
    Mute,
}
