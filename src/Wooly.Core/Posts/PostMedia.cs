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
    ///     Whether there are pixels a terminal can put in a box for this: a still picture, and a video or an animation
    ///     the instance offered a <see cref="Preview" /> of.
    /// </summary>
    /// <remarks>
    ///     ADR-0016 held this to a still picture, because a frozen frame with nothing to say it was meant to move is a
    ///     misleading rendering. ADR-0017 gave a video and an animation the thing that says it — the permanent,
    ///     walkable label beside the box, which is what <c>⏎</c> opens the whole of the attachment with — so the frame
    ///     stands beside the word rather than in place of the thing.
    ///     <para>
    ///         <c>Audio</c> and <c>Unknown</c> stay out even where an instance sends a preview: cover art is not a
    ///         frame of motion, so it tells a reader nothing the label and description do not already say, and
    ///         <c>Unknown</c> cannot promise a box means anything at all (ADR-0017).
    ///     </para>
    ///     <para>
    ///         A still picture is the one kind whose own file stands in for a preview the instance did not send: that
    ///         file is already a picture. A video's is motion, so a video with no preview has nothing to draw, and
    ///         sending for it anyway would fetch a whole video only to fail to decode it.
    ///     </para>
    /// </remarks>
    public bool IsDrawable => Kind switch
    {
        MediaKind.Image => true,
        MediaKind.Video or MediaKind.Animation => Preview is not null,
        _ => false,
    };

    /// <summary>
    ///     Whether the thing itself is reached by opening its address outside the terminal, which is what makes an
    ///     attachment one of the <c>Reference</c>s <c>←</c>/<c>→</c> walk and <c>⏎</c> opens (ADR-0017).
    /// </summary>
    /// <remarks>
    ///     Everything but a still picture, and separate from <see cref="IsDrawable" /> rather than its opposite: a
    ///     video is both drawn and opened, and its preview being drawn is precisely why it needs a label saying what
    ///     opening it would reach. A picture is the whole of itself once drawn, and where it cannot be drawn it is
    ///     linked exactly as the CLI links it (ADR-0016), so it never joins the walk.
    /// </remarks>
    public bool Opens => Kind is not MediaKind.Image;

    /// <summary>
    ///     What this shows, in the words its author gave it — or, where they gave none, in this client's, which names
    ///     the kind and says the description is missing rather than leaving a reader to guess whether it was.
    /// </summary>
    public string Shows => Description ?? $"{MediaKindName.Of(Kind)}, undescribed";
}
