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

    /// <summary>Shows <paramref name="picture" /> as the attachment <paramref name="mediaId" /> names.</summary>
    /// <remarks>
    ///     Only ever called on a view holding this attachment already or holding nothing at all: one of these never
    ///     goes straight from one picture to another. <see cref="Release" /> says why.
    /// </remarks>
    public void Show(string mediaId, Picture picture)
    {
        if (MediaId == mediaId)
        {
            return;
        }

        MediaId = mediaId;
        Image = picture.Pixels;
    }

    /// <summary>Takes the picture off this view, and off the terminal.</summary>
    /// <remarks>
    ///     Clearing <see cref="ImageView.Image" /> is what withdraws the image from the output buffer, and withdrawing
    ///     it from the buffer is what makes the driver delete the Kitty placement holding it on screen. Hiding the view
    ///     alone leaves that to the sweep Terminal.Gui makes of views that are no longer rendering, which is a weaker
    ///     promise than saying so — and a Kitty placement nobody deletes is not erased by drawing text over it
    ///     (ADR-0016). This is the difference between a picture that goes away and one that stays over the next post.
    /// </remarks>
    public void Release()
    {
        if (MediaId is null && !Visible)
        {
            return;
        }

        MediaId = null;
        Visible = false;
        Image = null;
    }
}
