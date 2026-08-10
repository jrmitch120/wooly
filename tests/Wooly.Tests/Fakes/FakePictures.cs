using Terminal.Gui.Drawing;
using Wooly.Tui.Media;

namespace Wooly.Tests.Fakes;

/// <summary>
///     A terminal's answer about pictures, said outright: whether it draws them at all, how big its cells are, and
///     whose pixels have arrived. Stands in for the real one so a screen can be laid out with no terminal and no
///     network, which is the whole reason <see cref="IPictures" /> is a port.
/// </summary>
internal sealed class FakePictures : IPictures
{
    private readonly Dictionary<string, Picture> _held = [];

    private FakePictures(CellSize? cell) => Cell = cell;

    /// <summary>Every picture looked up, by id, in order.</summary>
    public List<string> Asked { get; } = [];

    /// <summary>Every picture sent for, by id, in order — what proves only what is near the screen is fetched.</summary>
    public List<string> Sent { get; } = [];

    /// <inheritdoc />
    public CellSize? Cell { get; }

    /// <summary>A terminal that draws nothing: neither sixel nor the Kitty graphics protocol.</summary>
    public static FakePictures DrawingNothing() => new(cell: null);

    /// <summary>
    ///     A terminal that draws, with cells <paramref name="cell" /> pixels each — 10×20 being what both protocols
    ///     fall back to reporting.
    /// </summary>
    public static FakePictures With(CellSize? cell = null) => new(cell ?? new CellSize(10, 20));

    /// <summary>Says that the picture for the attachment <paramref name="mediaId" /> has arrived, at the given size in pixels.</summary>
    public FakePictures Holding(string mediaId, int width, int height) => Held(mediaId, width, height);

    /// <summary>Says that <paramref name="account" />'s avatar has arrived, at the given size in pixels.</summary>
    public FakePictures HoldingAvatarOf(string account, int width = 96, int height = 96) =>
        Held(Drawn.Avatar(account, "https://files.mastodon.social/avatars/original.png").Id, width, height);

    /// <inheritdoc />
    public Picture? Of(Drawn drawn)
    {
        Asked.Add(drawn.Id);

        return _held.GetValueOrDefault(drawn.Id);
    }

    /// <inheritdoc />
    public void Want(Drawn drawn) => Sent.Add(drawn.Id);

    private FakePictures Held(string id, int width, int height)
    {
        _held[id] = new Picture(new Color[width, height]);

        return this;
    }
}
