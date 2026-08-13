namespace Wooly.Core.Relationships;

/// <summary>
///     Which side of an account's follows is being listed. Following is not the reverse of followers — the two lists
///     overlap only where a follow was returned — so which one is asked for is a fact a caller has to carry, not one
///     that can be worked out from the other.
/// </summary>
public enum FollowSide
{
    /// <summary>The accounts that follow the one being listed.</summary>
    Followers,

    /// <summary>The accounts the one being listed follows.</summary>
    Following,
}

/// <summary>Choosing between the two sides, which is the only thing anything ever does with one.</summary>
public static class FollowSides
{
    /// <summary>
    ///     Whichever of the two <paramref name="side" /> names.
    /// </summary>
    /// <remarks>
    ///     Here rather than as a <c>switch</c> at each of the three places that pick — the endpoint to read, the word
    ///     to print, the word to write as JSON — because each of those had to spell out what a third side would mean,
    ///     and three copies of "there is no third side" is two of them to keep in step with an enum that grows one.
    /// </remarks>
    public static T Either<T>(this FollowSide side, T followers, T following) => side switch
    {
        FollowSide.Followers => followers,
        FollowSide.Following => following,
        _ => throw new ArgumentOutOfRangeException(nameof(side), side, "Not a side of a follow this client lists."),
    };
}
