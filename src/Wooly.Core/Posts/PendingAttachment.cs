namespace Wooly.Core.Posts;

/// <summary>
///     A <b>pending attachment</b>: a file sent up to an instance while its post is still being composed, which the
///     instance has finished processing and holds under <paramref name="Id" /> until a post names it (ADR-0026, #375).
///     What <see cref="IPostAuthor.Attach" /> hands back, and what a draft published by
///     <see cref="IPostAuthor.PublishAttached" /> names.
/// </summary>
/// <param name="Id">The instance's id for it, which is how a post carries it.</param>
/// <param name="Kind">What the instance made of it: a picture, an animation, a video or a sound.</param>
public sealed record PendingAttachment(string Id, MediaKind Kind);
