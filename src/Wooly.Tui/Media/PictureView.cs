using System.Drawing;
using Terminal.Gui.App;
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
internal sealed class PictureView : ImageView, IPictureBox
{
    /// <summary>
    ///     What the cells under a picture are painted in: blank, with no background at all, which is how the driver
    ///     knows they are the picture's and leaves them unwritten rather than drawing text over it. Not a role — the
    ///     cells are the picture's, not the page's (ADR-0014).
    /// </summary>
    private static readonly Attribute UnderThePicture = new(Color.None, Color.None);

    private Sixel? _sixel;

    /// <summary>
    ///     The driver's cells as this last handed it its sixel — the array itself, which Terminal.Gui makes anew when it
    ///     clears the buffer (<see cref="OnScreen" />).
    /// </summary>
    private Cell[,]? _sentOver;

    /// <summary>Whether a frame written since the sixel was last sent wrote over any of its cells (<see cref="Written" />).</summary>
    private bool _overdrawn;

    /// <summary>The application whose written frames this asks after, from the first sixel it hands over.</summary>
    private IApplication? _watching;

    /// <summary>
    ///     Makes a box and adds it to <paramref name="holder" />, hidden: the program's way of making the boxes a view
    ///     draws pictures through (<see cref="IPictureBox" />, #360).
    /// </summary>
    public static IPictureBox AddedTo(View holder)
    {
        var box = new PictureView { Visible = false };

        holder.Add(box);

        return box;
    }

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

    protected override void Dispose(bool disposing)
    {
        if (disposing && _watching is not null)
        {
            _watching.LayoutAndDrawComplete -= Written;
            _watching = null;
        }

        base.Dispose(disposing);
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
        _sentOver = null;
        Visible = false;
        Image = null;
    }

    /// <summary>
    ///     Hands the driver the part of the picture this box is showing, already encoded — the cells under it painted
    ///     as the picture's, so the driver leaves them to it.
    /// </summary>
    /// <remarks>
    ///     The box is never larger than the part of the picture on the page (<see cref="Placing" /> frames it so), so the
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

        var screen = ViewportToScreen();
        var cells = new Rectangle(screen.X, screen.Y, Viewport.Width, Viewport.Height);
        var buffer = driver.GetOutputBuffer();

        // The image view's own id, which is the one the driver keeps it under from frame to frame.
        var id = $"ImageView_{GetHashCode()}";

        var resend = !OnScreen(buffer, id, sixel, cells);

        SetAttribute(UnderThePicture);

        for (var row = 0; row < Viewport.Height; row++)
        {
            AddStr(0, row, new string(' ', Viewport.Width));
        }

        buffer.AddRasterImage(new RasterImageCommand
        {
            Id = id,
            Pixels = sixel.Pixels,
            EncodedSixel = sixel.Encoded,
            DestinationCells = cells,
            IsDirty = resend,
        });

        _sentOver = buffer.Contents;
        _overdrawn = false;

        if (_watching is null && App is { } app)
        {
            _watching = app;
            app.LayoutAndDrawComplete += Written;
        }

        context?.AddDrawnRectangle(cells);

        return true;
    }

    /// <summary>
    ///     Whether the terminal still shows <paramref name="sixel" /> in <paramref name="cells" />, just as this frame
    ///     would send it — which is when sending it again is only bytes the terminal must parse and draw. A frame that
    ///     redraws the rows around a picture and leaves it be, as a letter typed into a description does for the counter
    ///     under it, is every frame on a compose screen, and on Windows Terminal a sixel a letter lagged the typing
    ///     (review of #372).
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         Terminal.Gui keeps a raster image from one frame to the next under its id, and writes it only while it
    ///         is dirty. One that is not leaves its cells unwritten all the same — the cells painted as the picture's are
    ///         never written over it — so what was sent stays on screen. That is what makes not sending it sound, and
    ///         only where all of these hold:
    ///     </para>
    ///     <list type="bullet">
    ///         <item>
    ///             The buffer is the one last sent over. Terminal.Gui clears it, as a new array, on a resize and on a
    ///             whole redraw; after that nothing in it says what the terminal shows, so the picture goes again.
    ///         </item>
    ///         <item>
    ///             The buffer holds this view's image from before, already written, and it is this sixel in these
    ///             cells, let draw in the same parts of them. A resize drops every image, and a box hidden or framed
    ///             to another cut hands a different one.
    ///         </item>
    ///         <item>
    ///             No frame written since wrote over any cell it is let draw in (<see cref="Written" />).
    ///         </item>
    ///     </list>
    /// </remarks>
    private bool OnScreen(IOutputBuffer buffer, string id, Sixel sixel, Rectangle cells) =>
        !_overdrawn
        && buffer.Contents is { } contents
        && ReferenceEquals(contents, _sentOver)
        && Held(buffer, id) is { IsDirty: false, AlwaysRender: false } held
        && ReferenceEquals(held.EncodedSixel, sixel.Encoded)
        && held.DestinationCells == cells
        && LetDraw(buffer.Clip, cells).SequenceEqual(LetDraw(held.Clip, cells));

    /// <summary>The image the driver holds under <paramref name="id" />, or <see langword="null" /> where it holds none.</summary>
    private static RasterImageCommand? Held(IOutputBuffer buffer, string id) =>
        buffer.GetRasterImages().FirstOrDefault(image => image.Id == id);

    /// <summary>
    ///     Once a frame is written: whether anything was written over this picture's cells, in this frame or any since it
    ///     was last sent — a list hung over it, say. Each cell it is let draw in should still hold its blank, which the
    ///     driver never writes; one holding anything else went to the terminal over the picture, and left a hole the
    ///     next frame that draws it fills by sending it again (<see cref="OnScreen" />).
    /// </summary>
    /// <remarks>
    ///     Asked once the frame is out rather than as the picture is drawn, since by then the rows around it have been
    ///     painted again for the frame, over whatever was written there before.
    /// </remarks>
    private void Written(object? sender, EventArgs e)
    {
        if (_overdrawn
            || _sixel is null
            || App?.Driver?.GetOutputBuffer() is not { Contents: { } contents } buffer
            || Held(buffer, $"ImageView_{GetHashCode()}") is not { } held)
        {
            return;
        }

        foreach (var part in LetDraw(held.Clip, held.DestinationCells))
        {
            for (var row = part.Top; row < part.Bottom; row++)
            {
                for (var column = part.Left; column < part.Right; column++)
                {
                    if (!ThePictures(contents[row, column]))
                    {
                        _overdrawn = true;

                        return;
                    }
                }
            }
        }
    }

    /// <summary>
    ///     The parts of <paramref name="cells" /> inside <paramref name="clip" /> and on the screen — where the driver
    ///     lets a picture placed there draw.
    /// </summary>
    private Rectangle[] LetDraw(Region? clip, Rectangle cells)
    {
        var on = Rectangle.Intersect(cells, new Rectangle(Point.Empty, App?.Driver?.Screen.Size ?? cells.Size));

        if (clip is null)
        {
            return on.IsEmpty ? [] : [on];
        }

        var region = clip.Clone();

        region.Intersect(on);

        return [.. region.GetRectangles().Where(part => !part.IsEmpty)];
    }

    /// <summary>
    ///     Whether <paramref name="cell" /> is one the driver takes for a picture's and leaves unwritten: blank, with no
    ///     background at all — as Terminal.Gui decides it, and as <see cref="UnderThePicture" /> paints it.
    /// </summary>
    private static bool ThePictures(Cell cell) =>
        (string.IsNullOrEmpty(cell.Grapheme) || cell.Grapheme == " ")
        && (cell.Attribute is not { } attribute || attribute.Background.A == 0);
}
