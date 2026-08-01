using Terminal.Gui.Views;

namespace Wooly.Tui.Media;

/// <summary>
///     One picture drawn in place, over the rows a post reserved for it. Terminal.Gui's own <see cref="ImageView" />
///     does the drawing, through the Kitty graphics protocol or sixel; which of those it prefers is settled by
///     <see cref="RasterProtocol" /> (ADR-0016).
/// </summary>
/// <remarks>
///     A subclass for two reasons. It remembers which attachment it is showing, because handing the same pixels back
///     to <see cref="ImageView.Image" /> on every frame would re-encode and re-transmit the picture on every keypress,
///     which on a slow connection to a terminal is visible as a flicker. And it will not draw at all where neither
///     protocol is there: <see cref="ImageView" /> would otherwise fall back to a coloured cell per pixel, and that
///     rung is gone from this client's ladder on the evidence of what it produced — a photograph rendered as a few
///     dozen blocks resembles nothing, and the reader is better served by the description and the address the rows
///     carry instead. A post is laid out knowing that, so the rows for a box only exist where there is a box to draw;
///     this is the belt to that braces.
/// </remarks>
internal sealed class PictureView : ImageView
{
    public PictureView()
    {
        // A picture is something a reader looks at rather than something they tab to, and the shell's keys all belong
        // to the screen underneath it.
        CanFocus = false;
    }

    /// <summary>Whether this can be drawn at all, which on a terminal with neither protocol it cannot.</summary>
    public bool CanDraw => IsUsingRasterGraphics;

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
