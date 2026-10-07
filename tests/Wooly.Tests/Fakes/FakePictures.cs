using Terminal.Gui.Drawing;
using Wooly.Core.Posts;
using Wooly.Tui.Media;

namespace Wooly.Tests.Fakes;

/// <summary>
///     Whose pixels have arrived, said outright. Stands in for the real cache so a screen can be laid out with no
///     network, which is the whole reason <see cref="IPictures" /> is a port. Whether the terminal draws them at all,
///     and how big its cells are, is the <see cref="Raster" /> on the drawing instead (#357).
/// </summary>
internal sealed class FakePictures : IPictures
{
    private readonly Dictionary<string, Picture> _held = [];

    /// <summary>Every picture looked up, by id, in order.</summary>
    public List<string> Asked { get; } = [];

    /// <summary>
    ///     Every picture sent for, by id, in order, frame after frame — what proves only what is near the screen is
    ///     fetched.
    /// </summary>
    public List<string> Sent { get; } = [];

    /// <summary>Every frame's wants as the view said them: nearest first, with those on screen marked.</summary>
    public List<IReadOnlyList<WantedPicture>> Frames { get; } = [];

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

    private FakePictures Held(string id, int width, int height)
    {
        _held[id] = new Picture(new Color[width, height]);

        return this;
    }
}
