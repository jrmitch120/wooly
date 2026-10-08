using Wooly.Core.Posts;
using Wooly.Tui.Media;
using Wooly.Tui.Theme;

namespace Wooly.Tui.Rendering;

/// <summary>
///     The small picture on a pending attachment's row under the Media header (#382): what its column holds, the
///     picture to read off the disk for it, and the box to draw it in once the pixels are here.
/// </summary>
/// <remarks>
///     The column is held whatever the terminal, blank where nothing will be drawn in it, so that a row reads the same
///     on every terminal and nothing on it moves when a picture arrives — the rule the rows were laid out by (#375).
///     Where a picture is coming, the <see cref="StandIn" />'s shade holds the column until it is here, as it does an
///     avatar's (ADR-0025). Only a picture this client can decode is sent for: a video, a sound, or a photograph in a
///     format the decoder does not read would otherwise hold the shade for a picture that never comes.
///     <para>
///         Three columns by one row, the size the prototype tried in place (#374): one cell of a picture is a blot,
///         and three are enough to tell roughly what it is. Seeing it properly is the description editor's (story 42).
///     </para>
/// </remarks>
/// <param name="Held">What the column holds: the shade, or blanks.</param>
/// <param name="Wanted">The picture to read, or <see langword="null" /> where none is drawn.</param>
/// <param name="Box">Where to draw it, or <see langword="null" /> while the pixels are not here.</param>
public readonly record struct AttachmentPicture(Span Held, Drawn? Wanted, Inset? Box)
{
    /// <summary>How wide the column is.</summary>
    public const int Columns = 3;

    /// <summary>
    ///     The kinds of file whose pixels this client decodes (<see cref="PictureDecoder" />), by extension: the
    ///     pictures Mastodon takes but for HEIC, HEIF and AVIF.
    /// </summary>
    private static readonly HashSet<string> Decoded = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".gif", ".webp",
    };

    /// <summary>Whether the file at <paramref name="path" /> is a picture this client decodes, by its extension.</summary>
    public static bool Decodes(string path) => Decoded.Contains(Path.GetExtension(path));

    /// <summary>
    ///     What the picture column at <paramref name="column" /> holds for the file at <paramref name="path" />.
    /// </summary>
    /// <param name="path">Where the file being attached is.</param>
    /// <param name="column">How far in from the left of the row the column starts.</param>
    /// <param name="pictures">
    ///     Whose pixels have arrived, or <see langword="null" /> for a screen laid out with no terminal in the room.
    /// </param>
    /// <param name="raster">How this terminal paints pixels, or <see langword="null" /> where nobody said.</param>
    public static AttachmentPicture Of(string path, int column, IPictures? pictures, Raster? raster) =>
        Of(Decodes(path) ? Drawn.Attaching(path) : null, column, pictures, raster);

    /// <summary>
    ///     What the picture column at <paramref name="column" /> holds for <paramref name="kept" />, an attachment the
    ///     post being edited already carries (#381, review of #372): the instance's preview of it, sent for as the feed's
    ///     pictures are, where it has one this client can draw.
    /// </summary>
    public static AttachmentPicture OfKept(PostMedia kept, int column, IPictures? pictures, Raster? raster) =>
        Of(kept.IsDrawable ? Drawn.Kept(kept) : null, column, pictures, raster);

    /// <summary>What the picture column holds for <paramref name="wanted" />, or blanks where nothing is.</summary>
    private static AttachmentPicture Of(Drawn? wanted, int column, IPictures? pictures, Raster? raster)
    {
        var blank = new Span(new string(' ', Columns), Role.Body);

        if (wanted is null || !Drawing.Draws(pictures, raster, out _))
        {
            return new AttachmentPicture(blank, null, null);
        }

        return pictures.Of(wanted) is null
            ? new AttachmentPicture(StandIn.Row(Columns), wanted, null)
            : new AttachmentPicture(blank, wanted, new Inset(wanted, column, Columns, Rows: 1));
    }
}
