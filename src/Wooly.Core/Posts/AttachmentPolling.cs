namespace Wooly.Core.Posts;

/// <summary>
///     How long <see cref="PostAuthor.Attach" /> waits between asking an instance whether it has finished processing a
///     file (#375). A second, as Mastodon's own web client asks; a test sets none.
/// </summary>
/// <param name="Every">The wait between one ask and the next.</param>
public sealed record AttachmentPolling(TimeSpan Every)
{
    /// <summary>Once a second.</summary>
    public static readonly AttachmentPolling Default = new(TimeSpan.FromSeconds(1));
}
