using Terminal.Gui.Drawing;
using Wooly.Core;
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
    private readonly List<string> _letGo = [];
    private readonly Dictionary<string, Unshowable> _refused = [];

    /// <summary>Whose avatars have arrived, wherever the instance serves them from.</summary>
    private readonly Dictionary<string, Picture> _avatars = [];

    /// <summary>Every picture looked up, by id, in order.</summary>
    public List<string> Asked { get; } = [];

    /// <summary>
    ///     Every picture sent for, by id, in order, frame after frame — what proves only what is near the screen is
    ///     fetched.
    /// </summary>
    public List<string> Sent { get; } = [];

    /// <summary>Every frame's wants as the view said them: nearest first, with those on screen marked.</summary>
    public List<IReadOnlyList<WantedPicture>> Frames { get; } = [];

    /// <summary>The room each frame gave alongside its wants: its Raster and the content region's columns.</summary>
    public List<(Raster Raster, int Columns)> Rooms { get; } = [];

    /// <summary>Says that the picture for the attachment <paramref name="mediaId" /> has arrived, at the given size in pixels.</summary>
    public FakePictures Holding(string mediaId, int width, int height) => Held(mediaId, width, height);

    /// <summary>
    ///     Says that the original of the attachment <paramref name="mediaId" /> has arrived, as the Picture screen sends
    ///     for it, at the given size in pixels.
    /// </summary>
    public FakePictures HoldingOriginal(string mediaId, int width, int height) =>
        Held(Drawn.Original(APost.APicture(mediaId), 0, 0).Id, width, height);

    /// <summary>
    ///     Says that the picture the instance chose for <paramref name="link" /> has arrived, at the given size in
    ///     pixels — or holds nothing at all where it chose none, which is a preview with no picture coming.
    /// </summary>
    public FakePictures HoldingLinkPreview(LinkPreview link, int width, int height) =>
        Drawn.LinkPreview(link) is { } drawn ? Held(drawn.Id, width, height) : this;

    /// <summary>
    ///     Says that <paramref name="account" />'s avatar has arrived, at the given size in pixels — whichever address it
    ///     was sent for at, so that a test about the layout need not spell the fixture's.
    /// </summary>
    public FakePictures HoldingAvatarOf(string account, int width = 96, int height = 96)
    {
        _avatars[account] = new Picture(new Color[width, height]);

        return this;
    }

    /// <summary>Says that <paramref name="account" />'s avatar has arrived as <paramref name="picture" />.</summary>
    public FakePictures HoldingAvatarOf(string account, Picture picture)
    {
        _avatars[account] = picture;

        return this;
    }

    /// <summary>
    ///     Says that <paramref name="emoji" />'s still picture has arrived, at the given size in pixels — one picture
    ///     for every post and name that uses it.
    /// </summary>
    public FakePictures HoldingCustomEmoji(CustomEmoji emoji, int width = 36, int height = 36) =>
        Held(Drawn.CustomEmoji(emoji).Id, width, height);

    /// <summary>Says that <paramref name="emoji" />'s still picture has arrived as <paramref name="picture" />.</summary>
    public FakePictures HoldingCustomEmoji(CustomEmoji emoji, Picture picture)
    {
        _held[Drawn.CustomEmoji(emoji).Id] = picture;

        return this;
    }

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

        return _held.GetValueOrDefault(drawn.Id)
            ?? _avatars.FirstOrDefault(avatar => Drawn.Avatar(avatar.Key, drawn.Address) == drawn).Value;
    }

    /// <inheritdoc />
    public Unshowable? WhyNot(Drawn drawn) => _refused.TryGetValue(drawn.Id, out var why) ? why : null;

    /// <summary>
    ///     Says that the picture <paramref name="id" /> names could not be had, for <paramref name="why" /> — what the
    ///     Picture screen says on the status row of an original refused.
    /// </summary>
    public FakePictures Refusing(string id, Unshowable why)
    {
        _refused[id] = why;

        return this;
    }

    /// <inheritdoc />
    public void Want(IReadOnlyList<WantedPicture> frame, Raster raster, int columns)
    {
        Frames.Add(frame);
        Rooms.Add((raster, columns));
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
