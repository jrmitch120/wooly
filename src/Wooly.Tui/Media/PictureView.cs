using System.Drawing;
using Terminal.Gui.Drawing;
using Terminal.Gui.Drivers;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

// Terminal.Gui's own Attribute and Color, which are what a cell is painted in. System has both too.
using Attribute = Terminal.Gui.Drawing.Attribute;
using Color = Terminal.Gui.Drawing.Color;

namespace Wooly.Tui.Media;

/// <summary>
///     One picture drawn in place through a box, over the rows a post reserved for it — on a terminal drawing sixel, or
///     Kitty without its placeholders (ADR-0016, ADR-0022).
/// </summary>
/// <remarks>
///     An <see cref="ImageView" /> above all because the driver keeps a raster image from one frame to the next
///     only where an image view that is showing something says it is its own, and that question is not one a view
///     outside the library can answer. On a sixel terminal what it draws is its own: <see cref="ImageView" /> scales
///     and encodes the whole picture again every time its box moves, which on a scroll is every frame, so this is
///     handed a part of a picture already scaled to its box and encoded (<see cref="SixelPictures" />), and only hands
///     it on (#292, ADR-0023). Through Kitty it is the image view's to draw, which sends a picture once and places it
///     again as it moves.
///     <para>
///         It will not draw at all where neither protocol is there: <see cref="ImageView" /> would otherwise fall back
///         to a coloured cell per pixel, and that rung is gone from this client's ladder on the evidence of what it
///         produced. A post is laid out knowing that, so the rows for a box only exist where there is a box to draw;
///         this is the belt to that braces.
///     </para>
/// </remarks>
internal sealed class PictureView : ImageView
{
    /// <summary>
    ///     What the cells under a picture are painted in: blank, with no background at all, which is how the driver
    ///     knows they are the picture's and leaves them unwritten rather than drawing text over it. Not a role — the
    ///     cells are the picture's, not the page's (ADR-0014).
    /// </summary>
    private static readonly Attribute UnderThePicture = new(Color.None, Color.None);

    private Sixel? _sixel;

    public PictureView()
    {
        // A picture is something a reader looks at rather than something they tab to, and the shell's keys all belong
        // to the screen underneath it.
        CanFocus = false;

        // Nor is it something a reader points at. Terminal.Gui's image view zooms on the wheel and pans on a drag, so
        // a wheel over a picture grew it inside its box and never reached the page (#287): the pointer, like the
        // keys, is the screen's underneath, and a picture is only ever a part of the rows it sits on.
        MouseBindings.Clear();
    }

    /// <inheritdoc />
    /// <remarks>
    ///     Declined whatever it is, so that it bubbles to the content panel and the window as if no picture were there.
    /// </remarks>
    protected override bool OnMouseEvent(Mouse mouse) => false;

    /// <summary>Whether this can be drawn at all, which on a terminal with neither protocol it cannot.</summary>
    public bool CanDraw => IsUsingRasterGraphics;

    /// <summary>Which picture is being shown, as <see cref="Drawn.Id" />, or <see langword="null" /> while none is.</summary>
    public string? PictureId { get; private set; }

    /// <summary>
    ///     Shows the whole of <paramref name="picture" />, as the one <paramref name="pictureId" /> names, for the image
    ///     view to draw through Kitty.
    /// </summary>
    /// <remarks>
    ///     Only ever called on a view holding this picture already or holding nothing at all: one of these never goes
    ///     straight from one picture to another. <see cref="Release" /> says why.
    /// </remarks>
    public void Show(string pictureId, Picture picture)
    {
        if (PictureId == pictureId && _sixel is null)
        {
            return;
        }

        PictureId = pictureId;
        _sixel = null;
        Image = picture.Pixels;
    }

    /// <summary>Shows <paramref name="sixel" />, a part of the picture <paramref name="pictureId" /> names.</summary>
    /// <remarks>The same promise as the other <see cref="Show(string, Picture)" />.</remarks>
    public void Show(string pictureId, Sixel sixel)
    {
        if (ReferenceEquals(_sixel, sixel))
        {
            return;
        }

        PictureId = pictureId;
        _sixel = sixel;

        // Never drawn by the image view itself, but it is what the driver asks to know this view is showing something.
        Image = sixel.Pixels;

        SetNeedsDraw();
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
        if (PictureId is null && !Visible)
        {
            return;
        }

        PictureId = null;
        _sixel = null;
        Visible = false;
        Image = null;
    }

    /// <summary>
    ///     Hands the driver the part of the picture this box is showing, already encoded — the cells under it painted
    ///     as the picture's, so the driver leaves them to it.
    /// </summary>
    /// <remarks>
    ///     The box is never larger than the part of the picture on the page (<c>PaintedView</c> frames it so), so the
    ///     driver is never left to cut it at the edge — which it does by encoding the cut again, on every frame. A
    ///     whole picture shown for Kitty is the image view's own to draw.
    /// </remarks>
    protected override bool OnDrawingContent(DrawContext? context)
    {
        if (_sixel is not { } sixel)
        {
            return base.OnDrawingContent(context);
        }

        if (!CanDraw || App?.Driver is not { } driver || Viewport.Width <= 0 || Viewport.Height <= 0)
        {
            return true;
        }

        SetAttribute(UnderThePicture);

        for (var row = 0; row < Viewport.Height; row++)
        {
            AddStr(0, row, new string(' ', Viewport.Width));
        }

        var screen = ViewportToScreen();
        var cells = new Rectangle(screen.X, screen.Y, Viewport.Width, Viewport.Height);

        driver.GetOutputBuffer().AddRasterImage(new RasterImageCommand
        {
            // The image view's own id, which is the one the driver keeps it under from frame to frame.
            Id = $"ImageView_{GetHashCode()}",
            Pixels = sixel.Pixels,
            EncodedSixel = sixel.Encoded,
            DestinationCells = cells,
            IsDirty = true,
        });

        context?.AddDrawnRectangle(cells);

        return true;
    }
}
