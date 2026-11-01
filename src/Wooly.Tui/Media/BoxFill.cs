namespace Wooly.Tui.Media;

/// <summary>
///     How a picture takes the box it is drawn in, on every raster way (<see cref="PictureDecoder.Filled" />): the
///     one thing that differs between a picture, a Stand-in's blur and a custom emoji once each is at its box.
/// </summary>
internal enum BoxFill
{
    /// <summary>
    ///     Its middle cropped to the box's shape, then resized to it, as an instance crops an avatar — every picture
    ///     but the ones below (GLOSSARY.md's <b>Picture box</b>, #438).
    /// </summary>
    Cropped,

    /// <summary>
    ///     Stretched to the box: a Stand-in's blur, decoded square, which stretching gives back the picture's
    ///     proportions (ADR-0025).
    /// </summary>
    Stretched,

    /// <summary>
    ///     Fitted whole inside the box and centred, the rest of the box transparent: a custom emoji, as Mastodon's web
    ///     client draws one (ADR-0035), and an avatar, whose box is square only to within a column (#532).
    /// </summary>
    Fitted,
}
