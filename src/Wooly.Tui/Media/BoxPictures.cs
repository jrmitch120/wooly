using Wooly.Tui.Rendering;

namespace Wooly.Tui.Media;

/// <summary>
///     Each picture drawn through a box at exactly its Picture box's size in pixels, taken to it once
///     (<see cref="PictureDecoder.Scaled" />) and kept while a frame still asks for it at that size (#533): what Kitty
///     through a box is handed, and what a sixel's cuts are cut from (#554).
/// </summary>
/// <remarks>
///     Terminal.Gui's image view fits whatever it is handed inside its box, rounds that up to whole cells and has the
///     terminal stretch the picture over them, so a picture whose shape is not its box's in pixels came out
///     distorted — a wide custom emoji stretched to its two cells, an avatar to a box not square in pixels. Handed a
///     picture already at its box's size, it has nothing left to fit or round, and the picture takes its box as on
///     Kitty's placeholders and sixel: by the one step all three take it by, and its <see cref="BoxFill" />.
///     <para>
///         Kept by box size as well as by picture, so that a window resized, or one picture in two sizes of box, is
///         handed over at each size rather than at the first one's; and handed back the very same pixels while the box
///         stays that size, which is what tells a box it is already showing them (<see cref="PictureView.Show" />). A
///         picture near the page is taken to its box ahead, elsewhere (<see cref="Prepare" />), so that a scroll
///         bringing it on does not wait on it — as a sixel's cuts are encoded ahead (ADR-0023).
///     </para>
///     <para>
///         One is shared by both ways that draw through a box (<see cref="Placing" />): a sixel's cuts are cut from the
///         picture held here, with nothing laid under it, and each cut laid on what is behind its box once it is cut
///         (<see cref="SixelPictures" />) — so a picture is scaled once for its box, whichever way draws it and whether
///         its post is picked or not.
///     </para>
/// </remarks>
/// <param name="elsewhere">
///     Where a picture asked for ahead of time is taken to its box: off the UI thread in the client, and wherever a
///     test says.
/// </param>
/// <param name="scale">
///     How a picture is taken to its box: <see cref="PictureDecoder.Scaled" />, or a test's count of it.
/// </param>
internal sealed class BoxPictures(
    Action<Action>? elsewhere = null,
    Func<Picture, int, int, BoxFill, Picture>? scale = null)
{
    /// <summary>
    ///     Every size asked for, to show, to prepare or to cut from, since the last
    ///     <see cref="LetGoOfWhatWasNotAsked" />: what that keeps. On the UI thread only.
    /// </summary>
    private readonly HashSet<Sized> _asked = [];

    private readonly PreparedAhead<Sized, Picture, Picture> _held = new(
        (sized, picture) => (scale ?? PictureDecoder.Scaled)(picture, sized.Width, sized.Height, sized.Fill),
        elsewhere ?? (work => Task.Run(work)),
        new HeldUntilLetGoOf());

    /// <summary>
    ///     <paramref name="picture" /> at exactly <paramref name="inset" />'s box's size in pixels on
    ///     <paramref name="cell" />, taking the box as the inset says — taken to it here only where it was neither
    ///     asked for before nor prepared. On the UI thread, counted as asked for.
    /// </summary>
    /// <param name="inset">The box, and how what is set into it takes it.</param>
    /// <param name="picture">The pixels, or the blur.</param>
    /// <param name="cell">How many pixels a cell is, which turns the box into pixels.</param>
    public Picture Of(Inset inset, Picture picture, CellSize cell) => _held.Of(Asked(inset, picture, cell), picture);

    /// <summary>
    ///     The same, from any thread and not counted as asked for: what a sixel's cut is cut from, elsewhere, once the
    ///     frame wanting the cut has asked for it (<see cref="Ask" />).
    /// </summary>
    public Picture Unasked(Inset inset, Picture picture, CellSize cell) =>
        _held.Of(SizedOf(inset, picture, cell), picture);

    /// <summary>
    ///     Starts taking <paramref name="picture" /> to <paramref name="inset" />'s box elsewhere, for a box near the
    ///     page but not on it — so that it is usually ready by the time a scroll brings it on. On the UI thread.
    /// </summary>
    public void Prepare(Inset inset, Picture picture, CellSize cell) =>
        _held.Prepare(Asked(inset, picture, cell), picture);

    /// <summary>
    ///     Counts <paramref name="picture" /> in <paramref name="inset" />'s box as asked for this frame, so that it is
    ///     kept for a frame that wants it without being handed it — one whose sixel cuts of it are all held. On the UI
    ///     thread.
    /// </summary>
    public void Ask(Inset inset, Picture picture, CellSize cell) => Asked(inset, picture, cell);

    /// <summary>
    ///     Lets go of every picture not asked for since the last time — scrolled away from the page, at a size its box
    ///     no longer has, or on a frame that did not draw through a box at all — and starts counting afresh. Once a
    ///     frame, before it asks for any. On the UI thread.
    /// </summary>
    public void LetGoOfWhatWasNotAsked()
    {
        _held.LetGoOf(sized => !_asked.Contains(sized));
        _asked.Clear();
    }

    /// <summary>
    ///     What names <paramref name="picture" /> in <paramref name="inset" />'s box, counted as asked for.
    /// </summary>
    private Sized Asked(Inset inset, Picture picture, CellSize cell)
    {
        var sized = SizedOf(inset, picture, cell);

        _asked.Add(sized);

        return sized;
    }

    private static Sized SizedOf(Inset inset, Picture picture, CellSize cell) =>
        new(inset.Drawn.Id, inset.Columns * cell.Width, inset.Rows * cell.Height, DecodedSize.Of(picture), inset.Fill);

    /// <summary>
    ///     One picture at one box size in pixels, from pixels decoded at one size (<see cref="DecodedSize" />), taking
    ///     the box as <see cref="BoxFill" /> says.
    /// </summary>
    private readonly record struct Sized(string Drawn, int Width, int Height, DecodedSize Decoded, BoxFill Fill);

    /// <summary>
    ///     Holds every picture until <see cref="LetGoOfWhatWasNotAsked" /> lets go of it: what is kept is what a frame
    ///     asked for, which only the UI thread knows.
    /// </summary>
    private sealed class HeldUntilLetGoOf : IHoldPolicy<Sized>
    {
        public void Used(Sized key)
        {
        }

        public IReadOnlyList<Sized> Held(Sized key) => [];

        public void LetGo(Sized key)
        {
        }
    }
}
