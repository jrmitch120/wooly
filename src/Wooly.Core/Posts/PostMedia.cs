namespace Wooly.Core.Posts;

/// <summary>
///     Something attached to a post an instance has served, as read back off it. The counterpart to
///     <see cref="MediaAttachment" />, which is the same subject from the other end: that one is a file on this machine
///     on its way up, this one is what came down, and they share no field but the description.
///     <para>
///         Two records rather than one, because a post being read has no path and a file being uploaded has no id, no
///         URL and no kind the instance has decided yet. One record spanning both would be half-empty at each end, and
///         a caller could not tell which half it was holding.
///     </para>
/// </summary>
public sealed record PostMedia
{
    /// <summary>The instance's own id for the attachment.</summary>
    public required string Id { get; init; }

    /// <summary>What kind of thing it is, which settles how — or whether — it can be shown.</summary>
    public required MediaKind Kind { get; init; }

    /// <summary>Where the file itself is, which is what an attachment opened outside the terminal points at.</summary>
    public required string Url { get; init; }

    /// <summary>
    ///     A smaller copy for showing in place, or <see langword="null" /> where the instance offered none. Preferred
    ///     over <see cref="Url" /> for anything drawn inline: a terminal renders a few hundred pixels wide at most, and
    ///     fetching the full-size original to throw most of it away is somebody's data allowance.
    /// </summary>
    public string? Preview { get; init; }

    /// <summary>
    ///     What the attachment shows, as its author described it, or <see langword="null" /> if they described it as
    ///     nothing. The one field that matters most where the attachment cannot be drawn at all, which in a terminal is
    ///     most of the time.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    ///     Whether a terminal can draw this in place, rather than only say where it is. Only a still picture can:
    ///     a video and a sound have no single frame to stand for them, and an animation drawn as the one frame its
    ///     preview happens to be is exactly the misleading inline rendering story 51 asks not to attempt — a reader
    ///     shown a frozen picture has no way to tell it was meant to move (ADR-0016).
    /// </summary>
    public bool IsDrawable => Kind is MediaKind.Image;

    /// <summary>
    ///     What this shows, in the words its author gave it — or, where they gave none, in this client's, which names
    ///     the kind and says the description is missing rather than leaving a reader to guess whether it was.
    /// </summary>
    public string Shows => Description ?? $"{MediaKindName.Of(Kind)}, undescribed";
}
