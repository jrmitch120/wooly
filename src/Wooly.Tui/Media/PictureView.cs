using Terminal.Gui.Views;

namespace Wooly.Tui.Media;

/// <summary>
///     One picture drawn in place, over the rows a post reserved for it. Terminal.Gui's own <see cref="ImageView" />
///     does the drawing and picks the way of doing it — the Kitty graphics protocol, then sixel, then a coloured cell
///     per pixel, which needs nothing of the terminal at all. Which order those are tried in is settled once, at
///     startup, by <see cref="RasterProtocol" /> (ADR-0016).
/// </summary>
/// <remarks>
///     A subclass for one reason: to remember which attachment it is showing. Handing the same pixels back to
///     <see cref="ImageView.Image" /> on every frame would re-encode and re-transmit the picture on every keypress,
///     which on a slow connection to a terminal is visible as a flicker.
/// </remarks>
internal sealed class PictureView : ImageView
{
    public PictureView()
    {
        // A picture is something a reader looks at rather than something they tab to, and the shell's keys all belong
        // to the screen underneath it.
        CanFocus = false;
    }

    /// <summary>The instance's id for the attachment being shown, or <see langword="null" /> while it is showing none.</summary>
    public string? MediaId { get; private set; }

    /// <summary>Whether there is anything here to draw.</summary>
    public bool HasPicture => MediaId is not null;

    /// <summary>Shows <paramref name="picture" /> as the attachment <paramref name="mediaId" /> names.</summary>
    /// <remarks>
    ///     A <see langword="null" /> picture is one that has not arrived, or one that never will; either way there is
    ///     nothing to draw, and the row the post left saying what is attached is what the reader has.
    /// </remarks>
    public void Show(string mediaId, Picture? picture)
    {
        if (picture is null)
        {
            MediaId = null;

            return;
        }

        if (MediaId == mediaId)
        {
            return;
        }

        MediaId = mediaId;
        Image = picture.Pixels;
    }
}
