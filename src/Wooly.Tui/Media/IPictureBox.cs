using System.Drawing;

namespace Wooly.Tui.Media;

/// <summary>
///     One box a <b>drawn</b> picture is drawn through, over the rows a post reserved for it — on a terminal drawing
///     sixel, or Kitty without its placeholders (ADR-0016, ADR-0022). What the view does with a box, and no more.
/// </summary>
/// <remarks>
///     A seam with two adapters (#360): <see cref="PictureView" />, Terminal.Gui's image view, in the program — which
///     it must stay, since the driver keeps a raster from one frame to the next only for an image view that claims it —
///     and a fake in tests that writes down what each box was shown, where and when it was let go.
/// </remarks>
internal interface IPictureBox
{
    /// <summary>Whether this can be drawn at all, which on a terminal with neither protocol it cannot.</summary>
    bool CanDraw { get; }

    /// <summary>Which picture is being shown, as <see cref="Drawn.Id" />, or <see langword="null" /> while none is.</summary>
    string? PictureId { get; }

    /// <summary>Where the box sits, in the cells of the view holding it.</summary>
    Rectangle Frame { get; set; }

    /// <summary>Whether the box is drawn.</summary>
    bool Visible { get; set; }

    /// <summary>
    ///     Shows the whole of <paramref name="picture" />, as the one <paramref name="pictureId" /> names, to be drawn
    ///     through Kitty. Only ever called on a box holding this picture already or holding nothing at all.
    /// </summary>
    void Show(string pictureId, Picture picture);

    /// <summary>Shows <paramref name="sixel" />, a part of the picture <paramref name="pictureId" /> names.</summary>
    void Show(string pictureId, Sixel sixel);

    /// <summary>Takes the picture off this box, and off the terminal.</summary>
    void Release();
}
