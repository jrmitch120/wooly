using Wooly.Tui.Rendering;

namespace Wooly.Tui.Media;

/// <summary>
///     Which pictures a Kitty terminal holds, under which ids: each sent once per box size, then drawn as placeholder
///     cells naming it, and forgotten when the client lets go of it (ADR-0022).
/// </summary>
/// <remarks>
///     Asked from the frame, on the UI thread, which is the only thread the terminal is written to on. The one slow
///     part — encoding a PNG — is handed to <c>elsewhere</c>, so a frame never waits on it: the box keeps the rows it
///     reserved until the PNG is ready, a redraw is asked for, and the next frame sends it.
/// </remarks>
/// <param name="terminal">The terminal's image store.</param>
/// <param name="drawing">
///     Whether pictures are drawn this way at all, which is whether the terminal is known by name to draw Kitty's
///     placeholders (<see cref="KnownTerminal" />).
/// </param>
/// <param name="encoded">
///     What to do when a PNG is ready: redraw, so the box waiting on it fills in. Called on whatever thread encoded it.
/// </param>
/// <param name="elsewhere">
///     Where encoding is done: off the UI thread in the client, and on the spot in a test.
/// </param>
public sealed class Placeholders(
    ITerminalImages terminal,
    bool drawing,
    Action encoded,
    Action<Action> elsewhere) : IDisposable
{
    private readonly Lock _gate = new();
    private readonly Dictionary<Sized, Image> _images = [];
    private readonly List<int> _forgetting = [];

    // Unique to this run, and well clear of whatever an earlier one counted up to: a placeholder is drawn with the
    // first placement the terminal holds for its id, so an id an earlier run left behind is a picture drawn in the
    // wrong box (ADR-0022). Kept under 0xF00000 so a long run still fits in the 24 bits of colour an id is drawn in.
    private int _next = Random.Shared.Next(0x10000, 0xF00000);
    private bool _sentAny;
    private bool _disposed;

    /// <summary>Whether pictures are drawn as placeholders on this terminal.</summary>
    public bool Drawing => drawing;

    /// <summary>
    ///     The terminal's id for <paramref name="picture" /> in <paramref name="inset" />'s box, sending it first
    ///     where it is encoded and has not been sent — or <see langword="null" /> while it is still being encoded, or
    ///     where it cannot be drawn this way at all.
    /// </summary>
    /// <param name="inset">The box, whose size is what the picture is sent at.</param>
    /// <param name="picture">The pixels.</param>
    /// <param name="cell">How many pixels a cell is, which turns the box into pixels.</param>
    public int? Ready(Inset inset, Picture picture, CellSize cell)
    {
        if (Begun(inset, picture, cell) is not { } image)
        {
            return null;
        }

        if (image.Sent)
        {
            return image.Id;
        }

        byte[] png;

        lock (_gate)
        {
            // Dropped since it was looked up, in which case it is no longer anybody's to send.
            if (image.Png is null || image.Dropped)
            {
                return null;
            }

            png = image.Png;
            image.Png = null;
            image.Sent = true;
        }

        // Before the first picture of the run, so that nothing an earlier run left in the terminal is drawn.
        if (!_sentAny)
        {
            _sentAny = true;
            terminal.ForgetAll();
        }

        terminal.Transmit(image.Id, png, inset.Columns, inset.Rows);

        return image.Id;
    }

    /// <summary>
    ///     Starts encoding <paramref name="picture" /> for <paramref name="inset" />'s box without sending it, for a box
    ///     near the page but not on it — so that a picture is usually ready by the time it is scrolled to, rather than
    ///     encoded the frame it arrives and drawn a moment after the text around it.
    /// </summary>
    public void Prepare(Inset inset, Picture picture, CellSize cell) => Begun(inset, picture, cell);

    /// <summary>
    ///     Says that the picture <paramref name="drawnId" /> names has been let go of, so the terminal can let go of
    ///     every size of it too: what the frame drains from the picture cache (ADR-0022), and a blur no longer near the
    ///     page. On the UI thread; the terminal is told at the next <see cref="Flush" />.
    /// </summary>
    public void Drop(string drawnId)
    {
        lock (_gate)
        {
            foreach (var sized in _images.Keys.Where(sized => sized.Drawn == drawnId).ToList())
            {
                var image = _images[sized];

                _images.Remove(sized);
                image.Dropped = true;

                if (image.Sent)
                {
                    _forgetting.Add(image.Id);
                }
            }
        }
    }

    /// <summary>Tells the terminal about everything dropped since the last frame. On the UI thread.</summary>
    public void Flush()
    {
        int[] forgetting;

        lock (_gate)
        {
            forgetting = [.. _forgetting];
            _forgetting.Clear();
        }

        foreach (var id in forgetting)
        {
            terminal.Forget(id);
        }
    }

    /// <summary>Takes every picture this run sent off the terminal, on the way out.</summary>
    public void Dispose()
    {
        lock (_gate)
        {
            _disposed = true;
        }

        if (_sentAny)
        {
            terminal.ForgetAll();
        }
    }

    /// <summary>
    ///     The image for <paramref name="picture" /> in <paramref name="inset" />'s box, its encode started if this is
    ///     the first time it has been asked for — or <see langword="null" /> where it cannot be drawn this way at all.
    /// </summary>
    private Image? Begun(Inset inset, Picture picture, CellSize cell)
    {
        if (inset.Columns < 1 || inset.Rows < 1 || cell.Width < 1 || cell.Height < 1
            || inset.Columns > KittyPlaceholder.MostCells || inset.Rows > KittyPlaceholder.MostCells)
        {
            return null;
        }

        var sized = new Sized(inset.Drawn.Id, inset.Columns, inset.Rows, cell, DecodedSize.Of(picture));
        Image image;

        lock (_gate)
        {
            if (_images.TryGetValue(sized, out var known))
            {
                return known;
            }

            image = _images[sized] = new Image(_next++);
        }

        // Outside the lock, because in a test it is done on the spot and takes the lock itself.
        elsewhere(() => Encode(image, picture, sized));

        return image;
    }

    private void Encode(Image image, Picture picture, Sized sized)
    {
        byte[]? png;

        try
        {
            png = PictureEncoder.Png(picture, sized.Columns * sized.Cell.Width, sized.Rows * sized.Cell.Height);
        }
        catch (Exception failure) when (failure is not OutOfMemoryException)
        {
            // A picture that will not encode keeps the rows it reserved, as one that never arrived does.
            png = null;
        }

        lock (_gate)
        {
            // Nor is a redraw asked of a shell that has closed: there is nothing left to draw it on.
            if (png is null || image.Dropped || _disposed)
            {
                return;
            }

            image.Png = png;
        }

        encoded();
    }

    /// <summary>
    ///     One picture at one box size, on one size of cell, from pixels decoded at one size
    ///     (<see cref="DecodedSize" />) — which is what one image id means.
    /// </summary>
    private sealed record Sized(string Drawn, int Columns, int Rows, CellSize Cell, DecodedSize Decoded);

    /// <summary>Where one image has got to: being encoded, encoded and waiting to be sent, or sent.</summary>
    private sealed class Image(int id)
    {
        public int Id { get; } = id;

        public byte[]? Png { get; set; }

        public bool Sent { get; set; }

        public bool Dropped { get; set; }
    }
}
