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
