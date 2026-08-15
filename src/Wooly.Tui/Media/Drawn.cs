using Wooly.Core.Posts;

namespace Wooly.Tui.Media;

/// <summary>
///     A picture the TUI draws in place: which one it is, and where its pixels are fetched from.
/// </summary>
/// <remarks>
///     The key the whole picture path turns on — what is held, what is sent for, and what a box on a row stands in
///     for. Named separately from what it was made of because two different things are drawn now and only one of them
///     is an <c>Attachment</c> in this project's vocabulary (CONTEXT.md): a post's picture hangs off the post, and an
///     author's avatar hangs off the author. Keying on <see cref="PostMedia" /> and passing an avatar off as one would
///     have made "something attached to a post" mean something else wherever the avatar went.
/// </remarks>
/// <param name="Id">
///     What tells one picture from another, for as long as it is held. Namespaced by where it came from, so that an
///     attachment's id and an account's handle cannot collide in the one cache they share.
/// </param>
/// <param name="Address">Where the pixels are, which is what is actually fetched.</param>
public sealed record Drawn(string Id, string Address)
{
    /// <summary>
    ///     An attachment on a post, at the smaller copy where the instance offered one. A terminal draws a few hundred
    ///     pixels across at most, and fetching a photograph at full size to throw nine tenths of it away is somebody's
    ///     data allowance.
    /// </summary>
    /// <remarks>
    ///     Falling back to the attachment's own file is only ever a still picture's fallback, and the caller is what
    ///     holds that: <see cref="PostMedia.IsDrawable" /> is false for a video or an animation the instance offered no
    ///     preview of, so nothing reaches here that would send for a whole video only to fail to decode it (#110).
    /// </remarks>
    public static Drawn Attached(PostMedia media) => new(media.Id, media.Preview ?? media.Url);

    /// <summary>
    ///     An account's avatar, named by the handle rather than by the address, so that the same author's avatar is
    ///     fetched once for a whole feed of their posts however many times the instance spells the URL.
    /// </summary>
    /// <param name="account">Whose avatar it is, as <c>username@instance</c>.</param>
    /// <param name="address">Where to fetch it.</param>
    public static Drawn Avatar(string account, string address) => new($"avatar:{account}", address);
}
