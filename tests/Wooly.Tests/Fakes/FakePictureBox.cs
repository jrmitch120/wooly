using System.Drawing;
using Wooly.Tui.Media;

namespace Wooly.Tests.Fakes;

/// <summary>
///     A box a drawn picture is drawn through that draws nothing: it holds where it was framed and whether it is visible,
///     and writes down what it was shown and when it was let go, into a log shared with the rest of its pool — so that
///     what each box shows is an assertion, and so is what was let go of before anything was shown (#360, #361).
/// </summary>
internal sealed class FakePictureBox(int number, List<FakePictureBox.Call> log) : IPictureBox
{
    /// <summary>
    ///     What makes a pool of these, numbered from nought in the order they are made, every one writing into
    ///     <paramref name="log" /> and, where it is given, added to <paramref name="made" />.
    /// </summary>
    public static Func<IPictureBox> Pool(List<Call> log, List<FakePictureBox>? made = null)
    {
        var count = 0;

        return () =>
        {
            var box = new FakePictureBox(count++, log);

            made?.Add(box);

            return box;
        };
    }

    /// <inheritdoc />
    public bool CanDraw { get; set; } = true;

    /// <inheritdoc />
    public string? PictureId { get; private set; }

    /// <inheritdoc />
    public Rectangle Frame { get; set; }

    /// <inheritdoc />
    public bool Visible { get; set; }

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
        Visible = false;
    }

    /// <summary>What a box was asked to do.</summary>
    public enum Kind
    {
        ShownWhole,
        ShownSixel,
        Released,
    }

    /// <summary>
    ///     One call made of box <paramref name="Box" />, with the picture it held as it was made — for a release, the one
    ///     it let go of.
    /// </summary>
    public sealed record Call(int Box, Kind What, string? Picture);
}
