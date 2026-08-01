using Terminal.Gui.Drawing;
using Wooly.Core.Posts;
using Wooly.Tui.Media;

namespace Wooly.Tests.Fakes;

/// <summary>
///     A terminal's answer about pictures, said outright: whether it draws them at all, how big its cells are, and
///     which attachments' pixels have arrived. Stands in for the real one so a screen can be laid out with no terminal
///     and no network, which is the whole reason <see cref="IPictures" /> is a port.
/// </summary>
internal sealed class FakePictures : IPictures
{
    private readonly Dictionary<string, Picture> _held = [];

    private FakePictures(CellSize? cell) => Cell = cell;

    /// <summary>Every attachment this was asked about, in order — what proves a screen asked rather than assumed.</summary>
    public List<string> Asked { get; } = [];

    /// <inheritdoc />
    public CellSize? Cell { get; }

    /// <summary>A terminal that draws nothing: neither sixel nor the Kitty graphics protocol.</summary>
    public static FakePictures DrawingNothing() => new(cell: null);

    /// <summary>
    ///     A terminal that draws, with cells <paramref name="cell" /> pixels each — 10×20 being what both protocols
    ///     fall back to reporting.
    /// </summary>
    public static FakePictures With(CellSize? cell = null) => new(cell ?? new CellSize(10, 20));

    /// <summary>Says that the picture for <paramref name="mediaId" /> has arrived, at the given size in pixels.</summary>
    public FakePictures Holding(string mediaId, int width, int height)
    {
        _held[mediaId] = new Picture(new Color[width, height]);

        return this;
    }

    /// <inheritdoc />
    public Picture? Of(PostMedia media)
    {
        Asked.Add(media.Id);

        return _held.GetValueOrDefault(media.Id);
    }
}
