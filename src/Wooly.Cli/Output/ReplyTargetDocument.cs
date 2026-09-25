using System.Text.Json.Serialization;
using Wooly.Core.Posts;

namespace Wooly.Cli.Output;

/// <summary>
///     What a reply answers, spelled for another program to read — the fact the human output writes as
///     <c>↳ replying to @x</c> (#211). Written out by hand for the reason <see cref="PostDocument" /> gives.
/// </summary>
/// <param name="Post">The id of the post answered, which <c>post show</c> takes.</param>
/// <param name="Account">
///     Who wrote it, in full as <c>user@host</c>, and left out where the reply does not itself name who it answers —
///     an instance says only an id for them then, and an id means nothing on any other instance.
/// </param>
internal sealed record ReplyTargetDocument(
    [property: JsonPropertyName("post")] string Post,
    [property: JsonPropertyName("account")] string? Account)
{
    /// <summary>
    ///     How <paramref name="target" /> is written down, or <see langword="null" /> on a post that answers nothing —
    ///     which <see cref="JsonOutput" /> leaves out altogether rather than writing as a null.
    /// </summary>
    public static ReplyTargetDocument? Of(PostReplyTarget? target) =>
        target is null ? null : new ReplyTargetDocument(target.PostId, target.Handle);
}
