using Wooly.Tui.Rendering;

namespace Wooly.Tui.Media;

/// <summary>
///     The pictures Kitty through a box hands its boxes, each taken to its box's size in pixels once
///     (<see cref="PictureDecoder.Scaled" />) and kept while a frame still asks for it at that size (#533).
/// </summary>
/// <remarks>
///     Terminal.Gui's image view fits whatever it is handed inside its box, rounds that up to whole cells and has the
///     terminal stretch the picture over them, so a picture whose shape is not its box's in pixels came out distorted —
///     a wide custom emoji stretched to its two cells, an avatar to a box not square in pixels. Handed a picture already
///     at its box's size, it has nothing left to fit or round, and the picture takes its box as on Kitty's placeholders
///     and sixel: by the one step all three take it by, and its <see cref="BoxFill" />.
///     <para>
///         Kept by box size as well as by picture, so that a window resized, or one picture in two sizes of box, is
///         handed over at each size rather than at the first one's; and handed back the very same pixels while the box
///         stays that size, which is what tells a box it is already showing them (<see cref="PictureView.Show" />). A
///         picture near the page is taken to its box ahead, elsewhere (<see cref="Prepare" />), so that a scroll bringing
///         it on does not wait on it — as a sixel's cuts are encoded ahead (ADR-0023).
///     </para>
/// </remarks>
/// <param name="elsewhere">
///     Where a picture asked for ahead of time is taken to its box: off the UI thread in the client, and wherever a test
///     says.
/// </param>
internal sealed class KittyBoxPictures(Action<Action>? elsewhere = null)
{
    private readonly Action<Action> _elsewhere = elsewhere ?? (work => Task.Run(work));
    private readonly Lock _gate = new();
    private readonly Dictionary<Sized, Picture> _held = [];
    private readonly Dictionary<Sized, TaskCompletionSource<Picture>> _preparing = [];

    /// <summary>
    ///     Every size asked for, to show or to prepare, since the last <see cref="LetGoOfWhatWasNotAsked" />: what that
    ///     keeps. On the UI thread only.
    /// </summary>
    private readonly HashSet<Sized> _asked = [];

    /// <summary>
    ///     <paramref name="picture" /> at exactly <paramref name="inset" />'s box's size in pixels on
    ///     <paramref name="cell" />, taking the box as the inset says — taken to it here only where it was neither asked
    ///     for before nor prepared.
    /// </summary>
    /// <param name="inset">The box, and how what is set into it takes it.</param>
    /// <param name="picture">The pixels, or the blur.</param>
    /// <param name="cell">How many pixels a cell is, which turns the box into pixels.</param>
    public Picture Of(Inset inset, Picture picture, CellSize cell)
    {
        var sized = Asked(inset, picture, cell);
        TaskCompletionSource<Picture>? preparing;

        lock (_gate)
        {
            if (_held.TryGetValue(sized, out var known))
            {
                return known;
            }

            _preparing.TryGetValue(sized, out preparing);
        }

        if (preparing is not null)
        {
            try
            {
                return preparing.Task.GetAwaiter().GetResult();
            }
            catch (Exception failure) when (failure is not OutOfMemoryException)
            {
                // Taken to its box here instead, as it would have been had nobody prepared it.
            }
        }

        var scaled = Scaled(sized, picture);

        lock (_gate)
        {
            _held.TryAdd(sized, scaled);

            return _held[sized];
        }
    }

    /// <summary>
    ///     Starts taking <paramref name="picture" /> to <paramref name="inset" />'s box elsewhere, for a box near the
    ///     page but not on it — so that it is usually ready by the time a scroll brings it on.
    /// </summary>
    public void Prepare(Inset inset, Picture picture, CellSize cell)
    {
        var sized = Asked(inset, picture, cell);
        var preparing = new TaskCompletionSource<Picture>(TaskCreationOptions.RunContinuationsAsynchronously);

        lock (_gate)
        {
            if (_held.ContainsKey(sized) || !_preparing.TryAdd(sized, preparing))
            {
                return;
            }
        }

        _elsewhere(() =>
        {
            // Settled whatever happens, an out-of-memory included: the frame that asks for this picture waits on it,
            // and one left unsettled would leave the UI thread waiting for ever.
            try
            {
                var scaled = Scaled(sized, picture);

                lock (_gate)
                {
                    // Let go of while it was being taken to its box, in which case it is nobody's to hold — nor to
                    // take off the list, where it has been asked for again since and is being taken there afresh.
                    if (Settled(sized, preparing))
                    {
                        _held.TryAdd(sized, scaled);
                    }
                }

                preparing.SetResult(scaled);
            }
            catch (Exception failure)
            {
                lock (_gate)
                {
                    Settled(sized, preparing);
                }

                preparing.SetException(failure);

                if (failure is OutOfMemoryException)
                {
                    throw;
                }
            }
        });
    }

    /// <summary>
    ///     Lets go of every picture not asked for since the last time — scrolled away from the page, at a size its box
    ///     no longer has, or on a frame that did not draw through Kitty's boxes at all — and starts counting afresh.
    ///     Once a frame, before it asks for any.
    /// </summary>
    public void LetGoOfWhatWasNotAsked()
    {
        lock (_gate)
        {
            foreach (var gone in _held.Keys.Where(sized => !_asked.Contains(sized)).ToList())
            {
                _held.Remove(gone);
            }

            // One still being taken to its box is not held once it is, and is taken again if it is asked for again.
            foreach (var gone in _preparing.Keys.Where(sized => !_asked.Contains(sized)).ToList())
            {
                _preparing.Remove(gone);
            }
        }

        _asked.Clear();
    }

    /// <summary>
    ///     Takes <paramref name="preparing" /> off the list where it is still the one there for
    ///     <paramref name="sized" />, and says whether it was. Under the lock.
    /// </summary>
    private bool Settled(Sized sized, TaskCompletionSource<Picture> preparing) =>
        _preparing.TryGetValue(sized, out var listed) && listed == preparing && _preparing.Remove(sized);

    /// <summary>
    ///     What names <paramref name="picture" /> in <paramref name="inset" />'s box, counted as asked for.
    /// </summary>
    private Sized Asked(Inset inset, Picture picture, CellSize cell)
    {
        var sized = new Sized(
            inset.Drawn.Id,
            inset.Columns * cell.Width,
            inset.Rows * cell.Height,
            DecodedSize.Of(picture),
            inset.Fill);

        _asked.Add(sized);

        return sized;
    }

    /// <summary>The picture taken to its box. Outside the lock: this is the slow part.</summary>
    private static Picture Scaled(Sized sized, Picture picture) =>
        PictureDecoder.Scaled(picture, sized.Width, sized.Height, sized.Fill);

    /// <summary>
    ///     One picture at one box size in pixels, from pixels decoded at one size (<see cref="DecodedSize" />), taking
    ///     the box as <see cref="BoxFill" /> says.
    /// </summary>
    private readonly record struct Sized(string Drawn, int Width, int Height, DecodedSize Decoded, BoxFill Fill);
}
