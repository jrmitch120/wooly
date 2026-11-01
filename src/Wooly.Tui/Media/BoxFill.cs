using Wooly.Tui.Rendering;

namespace Wooly.Tui.Media;

/// <summary>
///     How a picture takes the box it is drawn in, on both raster paths (<see cref="PictureDecoder.Filled" />): the
///     one thing that differs between a picture, a Stand-in's blur and a custom emoji once each is at its box.
/// </summary>
internal enum BoxFill
{
    /// <summary>
    ///     Its middle cropped to the box's shape, then resized to it, as an instance crops an avatar — every picture
    ///     but the two below (GLOSSARY.md's <b>Picture box</b>, #438).
    /// </summary>
    Cropped,

    /// <summary>
    ///     Stretched to the box: a Stand-in's blur, decoded square, which stretching gives back the picture's
    ///     proportions (ADR-0025).
    /// </summary>
    Stretched,

    /// <summary>
    ///     Fitted whole inside the box and centred, the rest of the box transparent: a custom emoji, as Mastodon's web
    ///     client draws one (ADR-0035).
    /// </summary>
    Fitted,
}

/// <summary>Which <see cref="BoxFill" /> a picture set into a box takes.</summary>
internal static class BoxFills
{
    /// <summary>
    ///     How what <paramref name="inset" /> sets into its box takes it: stretched where it is a Stand-in's blur,
    ///     fitted where it is a custom emoji, and cropped where it is anything else.
    /// </summary>
    public static BoxFill Of(Inset inset) =>
        inset.Blur is not null ? BoxFill.Stretched
        : inset.Drawn.IsCustomEmoji ? BoxFill.Fitted
        : BoxFill.Cropped;
}
