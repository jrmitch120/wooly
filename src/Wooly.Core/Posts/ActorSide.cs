namespace Wooly.Core.Posts;

/// <summary>
///     Which of a post's two lists of <b>Actors</b> is being read: who favorited it, or who boosted it. The instance
///     keeps the two apart and so does this client, which never merges them — for the reason ADR-0032 gives against
///     grouping on the client (#487).
/// </summary>
public enum ActorSide
{
    /// <summary>The accounts that favorited the post.</summary>
    Favorited,

    /// <summary>The accounts that boosted the post.</summary>
    Boosted,
}

/// <summary>Choosing between the two sides, which is the only thing anything ever does with one.</summary>
public static class ActorSides
{
    /// <summary>Whichever of the two <paramref name="side" /> names.</summary>
    /// <remarks>Here rather than a <c>switch</c> at each place that picks, as <see cref="Relationships.FollowSides" /> is.</remarks>
    public static T Either<T>(this ActorSide side, T favorited, T boosted) => side switch
    {
        ActorSide.Favorited => favorited,
        ActorSide.Boosted => boosted,
        _ => throw new ArgumentOutOfRangeException(nameof(side), side, "Not a side of a post's actors this client lists."),
    };
}
