namespace Wooly.Core.Posts;

/// <summary>
///     Which of the three marks the profile reading a post has already put on it. The counts on a
///     <see cref="Post" /> say how many accounts boosted or favorited it; this says whether one of them was you, which
///     is a different question and the one a screen has to answer to draw a lit star rather than an empty one.
///     <para>
///         Kept together rather than as three fields on <see cref="Post" /> so that a caller can ask about a mark it was
///         handed — <c>marks.Has(mark)</c> — instead of switching over the three. That is what lets one keypress
///         handler serve boost, favorite and pin, which is where a client otherwise grows three of them that age apart.
///     </para>
/// </summary>
public sealed record PostMarks
{
    /// <summary>Whether this profile has boosted the post.</summary>
    public required bool Boosted { get; init; }

    /// <summary>Whether this profile has favorited the post.</summary>
    public required bool Favorited { get; init; }

    /// <summary>Whether this profile has pinned the post, which only its own posts can be.</summary>
    public required bool Pinned { get; init; }

    /// <summary>A post this profile has done none of the three to.</summary>
    public static PostMarks None { get; } = new() { Boosted = false, Favorited = false, Pinned = false };

    /// <summary>Whether <paramref name="mark" /> is one of the marks this profile has on the post.</summary>
    public bool Has(PostMark mark) => mark switch
    {
        PostMark.Boost => Boosted,
        PostMark.Favorite => Favorited,
        PostMark.Pin => Pinned,
        _ => throw new ArgumentOutOfRangeException(nameof(mark), mark, "Not a mark this client puts on a post."),
    };
}
