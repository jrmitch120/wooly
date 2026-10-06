namespace Wooly.Core.Posts;

/// <summary>
///     How wide and how tall a picture is, in pixels, as the instance reported it rather than as its pixels turn out to
///     be. What a <b>drawn</b> picture's box is settled from before the picture arrives, so that its arriving moves
///     nothing (ADR-0025).
/// </summary>
/// <remarks>
///     Only ever made from a width and a height that are both positive: a shape with a side of nothing has no
///     proportions to size a box from, and is no shape at all. Where the instance said none in full, the field that
///     would carry one is <see langword="null" />, and the default box is the TUI's to choose.
/// </remarks>
public readonly record struct PictureShape(int Width, int Height)
{
    /// <summary>
    ///     The shape a width and a height make, or <see langword="null" /> where either is missing or not positive.
    /// </summary>
    public static PictureShape? Of(int? width, int? height) =>
        width is { } wide and > 0 && height is { } tall and > 0 ? new PictureShape(wide, tall) : null;
}
