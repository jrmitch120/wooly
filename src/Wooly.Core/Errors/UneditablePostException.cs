namespace Wooly.Core.Errors;

/// <summary>
///     A post was named for editing that this client cannot edit without losing part of it. Only polls fall in here:
///     Mastodon's edit replaces a post rather than amending it, and an edit that did not resend the poll would delete
///     it, while one that did resend it would restart voting. Refusing outright is the only answer that neither loses
///     the poll nor quietly changes it — and the author can still delete the post and say it again.
/// </summary>
public sealed class UneditablePostException(string postId)
    : WoolyException(
        $"Post {postId} carries a poll, and editing it here would remove the poll along with the votes cast in it. "
        + "Delete the post and publish it again instead.")
{
    /// <summary>The post that was named.</summary>
    public string PostId { get; } = postId;
}
