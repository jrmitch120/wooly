namespace Wooly.Core.Posts;

/// <summary>
///     One post, in this project's vocabulary rather than the API's (CONTEXT.md): a <c>status</c> is a post, a
///     <c>reblog</c> is a boost, and <c>favourites</c> are favorites. Nothing above this layer sees the wire names.
/// </summary>
public sealed record Post
{
    /// <summary>The instance's own id for the post, which is how anything else is asked about it.</summary>
    public required string Id { get; init; }

    /// <summary>Who posted it, as <c>username@instance</c>.</summary>
    public required string Account { get; init; }

    /// <summary>The name that account chose to be shown as, which is not unique and may be anything at all.</summary>
    public required string Author { get; init; }

    /// <summary>When it was posted, as UTC.</summary>
    public required DateTimeOffset PostedAt { get; init; }

    /// <summary>The post's text, with the API's HTML flattened to something a terminal can print.</summary>
    public required string Content { get; init; }

    /// <summary>
    ///     What the author put the post behind, or <see langword="null" /> if they put it behind nothing. Kept apart
    ///     from <see cref="Content" /> so a reader can honour the warning rather than print past it.
    /// </summary>
    public string? ContentWarning { get; init; }

    /// <summary>
    ///     Who can see it. Read back off a post rather than assumed, which is what lets a client confirm that the
    ///     post it just published went out as narrowly as it was asked to — an author who meant <c>private</c> and got
    ///     <c>public</c> has no way to undo that by the time they find out.
    /// </summary>
    public required PostVisibility Visibility { get; init; }

    /// <summary>How many accounts have boosted it.</summary>
    public required long Boosts { get; init; }

    /// <summary>How many accounts have favorited it.</summary>
    public required long Favorites { get; init; }

    /// <summary>How many replies it has drawn.</summary>
    public required long Replies { get; init; }

    /// <summary>
    ///     The post this one boosts, or <see langword="null" /> if it is not a boost. A boost carries no text of its
    ///     own — <see cref="Account" /> is who boosted, and everything worth reading is in here.
    /// </summary>
    public Post? Boosted { get; init; }

    /// <summary>Where to read it on the web, or <see langword="null" /> if the instance did not say.</summary>
    public string? Url { get; init; }

    /// <summary>Whether this post is a boost of somebody else's.</summary>
    public bool IsBoost => Boosted is not null;
}
