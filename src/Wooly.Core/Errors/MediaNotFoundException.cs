using Wooly.Core.Posts;

namespace Wooly.Core.Errors;

/// <summary>
///     A file to be attached to a post is not where the author said it was — a mistyped path, most often, or a shell
///     that did not expand what looked like a wildcard. Raised before anything is uploaded, so a post with three
///     attachments and a typo in the third publishes nothing rather than something half composed.
/// </summary>
public sealed class MediaNotFoundException(string path) : WoolyException(MediaAttachment.Rejection(path))
{
    /// <summary>The path that had no file at it.</summary>
    public string Path { get; } = path;
}
