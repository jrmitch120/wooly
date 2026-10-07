namespace Wooly.Tui.Media;

/// <summary>
///     How many pixels across and down a <see cref="Picture" /> was decoded at. Part of what names a picture prepared
///     for the terminal — a Kitty image id (<see cref="Placeholders" />) or a sixel cut (<c>SixelPictures</c>) —
///     because a picture decoded again for a wider window is sharper pixels in the same box, and is prepared and sent
///     again for that (ADR-0025).
/// </summary>
/// <param name="Width">Pixels across.</param>
/// <param name="Height">Pixels down.</param>
public readonly record struct DecodedSize(int Width, int Height)
{
    /// <summary>The size <paramref name="picture" /> was decoded at.</summary>
    public static DecodedSize Of(Picture picture) => new(picture.Width, picture.Height);
}
