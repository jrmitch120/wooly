namespace Wooly.Core.Posts;

/// <summary>The post a reply answers, read back off the instance rather than assumed — see <see cref="Post.InReplyTo" />.</summary>
public sealed record PostReplyTarget
{
    /// <summary>The id of the post being answered.</summary>
    public required string PostId { get; init; }

    /// <summary>
    ///     The handle of the account being answered: the post's own author for a self-reply, or
    ///     <see langword="null" /> when the account being answered is not one the post itself names.
    /// </summary>
    public string? Handle { get; init; }
}
