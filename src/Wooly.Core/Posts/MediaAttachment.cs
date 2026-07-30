namespace Wooly.Core.Posts;

/// <summary>
///     A file to be attached to a post, and what it should be described as for anyone who cannot see it. A path rather
///     than bytes: nothing above the adapter opens the file, so nothing above the adapter has to remember to close it.
/// </summary>
public sealed record MediaAttachment
{
    /// <summary>Where the file is on this machine.</summary>
    public required string Path { get; init; }

    /// <summary>
    ///     What the attachment shows, for a reader using a screen reader or a slow connection, or
    ///     <see langword="null" /> if the author gave none. Left optional rather than required because an instance
    ///     accepts an attachment without one, and a client that refused to post one would be inventing a rule Mastodon
    ///     does not have.
    /// </summary>
    public string? AltText { get; init; }

    /// <summary>
    ///     How a path with no file at it is described, shared so that the command rejecting one before anything is
    ///     published and the adapter rejecting one that got past it cannot say different things about the same path.
    /// </summary>
    public static string Rejection(string path) => $"There is no file at '{path}' to attach.";
}
