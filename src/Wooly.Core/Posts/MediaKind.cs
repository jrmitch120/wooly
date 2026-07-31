namespace Wooly.Core.Posts;

/// <summary>
///     What kind of thing an instance says is attached to a post, which is what settles whether it can be drawn in a
///     terminal at all or only linked to.
/// </summary>
public enum MediaKind
{
    /// <summary>A still picture.</summary>
    Image,

    /// <summary>A soundless looping clip, which the wire calls a <c>gifv</c> whatever it was uploaded as.</summary>
    Animation,

    /// <summary>A video, with sound.</summary>
    Video,

    /// <summary>Sound with no picture.</summary>
    Audio,

    /// <summary>
    ///     Something this client has no word for — either the instance's own <c>unknown</c>, which is what it says
    ///     about an attachment it has not finished processing, or a kind added since this was written. Carried rather
    ///     than dropped: a post with an attachment nobody can name still has an attachment, and a reader shown nothing
    ///     would think the post was only its text.
    /// </summary>
    Unknown,
}
