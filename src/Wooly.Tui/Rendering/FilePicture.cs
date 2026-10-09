using Wooly.Core.Posts;
using Wooly.Tui.Media;

namespace Wooly.Tui.Rendering;

/// <summary>
///     A file on this machine drawn large, top left of a place a screen holds for it (#382): the file browser's
///     preview of the picture under its cursor, and the description editor's of the picture being described.
/// </summary>
/// <remarks>
///     The place is the screen's to hold, from the first frame and whatever is in it, so that nothing beside it moves
///     when a picture arrives or the cursor moves off one: this only says what goes in it. The box is the picture's
///     own shape, as wide as the place allows and no taller than it, set against the place's top left as the prototype
///     placed it (#374) — known only once the pixels are here, which is why nothing is set into the place before then.
///     Nothing at all where the terminal cannot draw, or the file is not a picture this client decodes.
/// </remarks>
/// <param name="Wanted">The picture to read off the disk, or <see langword="null" /> where none is drawn.</param>
/// <param name="Box">Where to draw it, or <see langword="null" /> while the pixels are not here.</param>
public readonly record struct FilePicture(Drawn? Wanted, Inset? Box)
{
    /// <summary>
    ///     What goes in a place <paramref name="columns" /> by <paramref name="rows" /> for the file at
    ///     <paramref name="path" />.
    /// </summary>
    /// <param name="path">Where the file is.</param>
    /// <param name="column">How far in from the left of the row the place starts.</param>
    /// <param name="columns">How wide the place is.</param>
    /// <param name="rows">How tall the place is.</param>
    /// <param name="drawing">What pictures are here, and how this terminal paints them.</param>
    public static FilePicture Of(string path, int column, int columns, int rows, Drawing drawing)
    {
        if (!Drawing.Draws(drawing.Pictures, drawing.Raster, out var cell) || !AttachmentPicture.Decodes(path))
        {
            return default;
        }

        var wanted = Drawn.OnDisk(path);
        var box = drawing.Pictures.Of(wanted) is { } picture
            ? Inset.For(wanted, new PictureShape(picture.Width, picture.Height), cell, columns, rows)
            : null;

        return new FilePicture(wanted, box is null ? null : box with { Column = column });
    }
}
