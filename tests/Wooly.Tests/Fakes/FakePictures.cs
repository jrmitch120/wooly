using Terminal.Gui.Drawing;
using Wooly.Core.Posts;
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
    private readonly List<string> _letGo = [];

    private FakePictures(CellSize? cell) => Cell = cell;

    /// <summary>Every picture looked up, by id, in order.</summary>
    public List<string> Asked { get; } = [];

    /// <summary>
    ///     Every picture sent for, by id, in order, frame after frame — what proves only what is near the screen is
    ///     fetched.
    /// </summary>
    public List<string> Sent { get; } = [];

    /// <summary>Every frame's wants as the view said them: nearest first, with those on screen marked.</summary>
    public List<IReadOnlyList<WantedPicture>> Frames { get; } = [];

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

    /// <summary>
    ///     Says that the picture the instance chose for <paramref name="link" /> has arrived, at the given size in
    ///     pixels — or holds nothing at all where it chose none, which is a preview with no picture coming.
    /// </summary>
    public FakePictures HoldingLinkPreview(LinkPreview link, int width, int height) =>
        Drawn.LinkPreview(link) is { } drawn ? Held(drawn.Id, width, height) : this;

    /// <summary>Says that <paramref name="account" />'s avatar has arrived, at the given size in pixels.</summary>
    public FakePictures HoldingAvatarOf(string account, int width = 96, int height = 96) =>
        Held(Drawn.Avatar(account, "https://files.mastodon.social/avatars/original.png").Id, width, height);

    /// <summary>
    ///     Says that the picture for the attachment <paramref name="mediaId" /> has arrived as a busy photograph —
    ///     every pixel different from its neighbours — which is what makes encoding it cost what a real one does.
    /// </summary>
    public FakePictures HoldingPhotograph(string mediaId, int width, int height)
    {
        var pixels = new Color[width, height];

        for (var x = 0; x < width; x++)
        {
            for (var y = 0; y < height; y++)
            {
                pixels[x, y] = new Color((x * 7 + y * 3) % 256, (x * y) % 256, (x * 13 + y * 11) % 256);
            }
        }

        _held[mediaId] = new Picture(pixels);

        return this;
    }

    /// <inheritdoc />
    public Picture? Of(Drawn drawn)
    {
        Asked.Add(drawn.Id);

        return _held.GetValueOrDefault(drawn.Id);
    }

    /// <inheritdoc />
    public void Want(IReadOnlyList<WantedPicture> frame)
    {
        Frames.Add(frame);
        Sent.AddRange(frame.Select(wanted => wanted.Drawn.Id));
    }

    /// <inheritdoc />
    public IReadOnlyList<string> Drain()
    {
        string[] letGo = [.. _letGo];

        _letGo.Clear();

        return letGo;
    }

    /// <summary>
    ///     Says that the cache has let go of the picture <paramref name="id" /> names, to be handed back by the next
    ///     <see cref="Drain" /> — what a view should pass on to a Kitty terminal holding a copy.
    /// </summary>
    public FakePictures LettingGo(string id)
    {
        _letGo.Add(id);

        return this;
    }

    private FakePictures Held(string id, int width, int height)
    {
        _held[id] = new Picture(new Color[width, height]);

        return this;
    }
}
