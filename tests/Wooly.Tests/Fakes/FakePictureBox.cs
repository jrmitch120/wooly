using System.Drawing;
using Wooly.Tui.Media;

namespace Wooly.Tests.Fakes;

/// <summary>
///     A box a drawn picture is drawn through that draws nothing and writes down every call made of it, into a log
///     shared with the rest of its pool — so that what each box was shown, where it sat and when it was let go are
///     assertions, in the order they happened, without a terminal (#360, #361).
/// </summary>
internal sealed class FakePictureBox(int number, List<FakePictureBox.Call> log) : IPictureBox
{
    private Rectangle _frame;
    private bool _visible;

    /// <summary>
    ///     What makes a pool of these, numbered from nought in the order they are made, every one writing into
    ///     <paramref name="log" />.
    /// </summary>
    public static Func<IPictureBox> Pool(List<Call> log)
    {
        var made = 0;

        return () => new FakePictureBox(made++, log);
    }

    /// <inheritdoc />
    public bool CanDraw { get; set; } = true;

    /// <inheritdoc />
    public string? PictureId { get; private set; }

    /// <inheritdoc />
    public Rectangle Frame
    {
        get => _frame;
        set
        {
            _frame = value;
            log.Add(new Call(number, Kind.Framed, PictureId, Frame: value));
        }
    }

    /// <inheritdoc />
    public bool Visible
    {
        get => _visible;
        set
        {
            _visible = value;
            log.Add(new Call(number, Kind.Visible, PictureId, Visible: value));
        }
    }

    /// <inheritdoc />
    public void Show(string pictureId, Picture picture)
    {
        PictureId = pictureId;
        log.Add(new Call(number, Kind.ShownWhole, pictureId));
    }

    /// <inheritdoc />
    public void Show(string pictureId, Sixel sixel)
    {
        PictureId = pictureId;
        log.Add(new Call(number, Kind.ShownSixel, pictureId));
    }

    /// <inheritdoc />
    public void Release()
    {
        log.Add(new Call(number, Kind.Released, PictureId));
        PictureId = null;
        _visible = false;
    }

    /// <summary>What a box was asked to do.</summary>
    public enum Kind
    {
        ShownWhole,
        ShownSixel,
        Framed,
        Visible,
        Released,
    }

    /// <summary>
    ///     One call made of box <paramref name="Box" />, with the picture it held as it was made — for a release, the one
    ///     it let go of.
    /// </summary>
    public sealed record Call(int Box, Kind What, string? Picture, Rectangle? Frame = null, bool? Visible = null);
}
