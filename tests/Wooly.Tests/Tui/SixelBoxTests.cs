using Wooly.Tests.Fakes;
using Wooly.Tui.Media;
using Wooly.Tui.Screens;
using Wooly.Tui.Theme;
using Wooly.Tui.Views;

namespace Wooly.Tests.Tui;

/// <summary>
///     Pictures on a sixel terminal, drawn through a box each and scrolled (#292). The pixels are a manual smoke test
///     (ADR-0016); what is here is the shape of what the driver is handed, and when.
/// </summary>
public class SixelBoxTests
{
    private const int OverContent = RailLines.Width + 4;

    /// <summary>
    ///     A box half off the top of the page is framed to the rows still on it, so the driver is handed exactly the
    ///     part on the page — rather than the whole box to cut, which it does by encoding the cut again on every frame.
    /// </summary>
    [Fact]
    public async Task ABoxHalfOffTheTopIsFramedToTheRowsStillOnThePage()
    {
        using var drawn = await Drawn();

        var box = Assert.Single(drawn.Content.SubViews.OfType<PictureView>(), view => view.Visible);
        var whole = box.Frame;

        for (var notch = 0; notch < 100 && drawn.Content.Top < whole.Y + 2; notch++)
        {
            drawn.Wheel(OverContent, 3);
        }

        Assert.Equal(whole.Y + 2, drawn.Content.Top);
        Assert.Equal(0, box.Frame.Y);
        Assert.Equal(whole.Height - 2, box.Frame.Height);
        Assert.Equal(whole.Width, box.Frame.Width);
    }

    /// <summary>
    ///     The same at the foot of the page: a box coming up onto it is drawn as the rows already on it, a row more on
    ///     each notch, rather than waiting until it is whole.
    /// </summary>
    [Fact]
    public async Task ABoxComingUpOntoThePageIsDrawnAsTheRowsAlreadyOnIt()
    {
        using var drawn = await DrawnShell.Of(
            80,
            24,
            Themes.Plain,
            new AShell
            {
                Timelines = FakeTimelineReader.Holding(
                    APost.With(id: "110"),
                    APost.With(id: "220"),
                    APost.With(id: "330", media: [APost.APicture("m1")]),
                    APost.With(id: "440")),
            },
            pictures: FakePictures.With().Holding("m1", 800, 400),
            drawsPictures: true);

        var box = Assert.Single(drawn.Content.SubViews.OfType<PictureView>(), view => view.Visible);
        var rows = box.Frame.Height;

        Assert.Equal(drawn.Content.Viewport.Height, box.Frame.Bottom);

        drawn.Wheel(OverContent, 3);
        drawn.Wheel(OverContent, 3);

        Assert.True(box.Visible);
        Assert.Equal(drawn.Content.Viewport.Height, box.Frame.Bottom);
        Assert.Equal(rows + 2, box.Frame.Height);
    }

    /// <summary>
    ///     Notches that arrive while a frame is being drawn are added up and drawn once, by the next frame — a notch
    ///     moves the page and draws nothing, so a fast flick never queues behind slow frames.
    /// </summary>
    /// <remarks>
    ///     This holds the half of it that is this client's: a notch draws nothing of its own. The other half — that
    ///     Terminal.Gui takes every input waiting before it draws a frame — is the library's main loop
    ///     (<c>ApplicationMainLoop.IterationImpl</c> drains the input queue, then draws), and is not something a
    ///     headless test drives through.
    /// </remarks>
    [Fact]
    public async Task NotchesBetweenTwoFramesAreDrawnTogetherByTheNext()
    {
        using var drawn = await Drawn();

        var before = drawn.Rows();

        for (var notch = 0; notch < 5; notch++)
        {
            drawn.WheelUndrawn(OverContent, 3);
        }

        Assert.Equal(before, drawn.Rows());

        drawn.Redraw();

        Assert.Equal(5, drawn.Content.Top);
        Assert.NotEqual(before, drawn.Rows());
    }

    /// <summary>
    ///     While the page is moving its pictures are handed to the driver rough — half the resolution, a quarter of the
    ///     colours, about a third of the bytes — and once it has been still for a moment, sharp again, without a key
    ///     to ask for it (#342).
    /// </summary>
    [Fact]
    public async Task APictureIsRoughWhileThePageMovesAndSharpOnceItIsStill()
    {
        var built = new AShell
        {
            Timelines = FakeTimelineReader.Holding(
                APost.With(id: "110", media: [APost.APicture("m1")]),
                APost.With(id: "220"),
                APost.With(id: "330"),
                APost.With(id: "440")),
        };
        using var drawn = await Drawn(built, new SixelPictures((_, colours) => colours == SixelPictures.RoughColours ? "rough" : "sharp", work => work()));

        var box = Assert.Single(drawn.Content.SubViews.OfType<PictureView>(), view => view.Visible);

        Assert.Equal("sharp", box.Shown?.Encoded);

        drawn.Wheel(OverContent, 3);
        Assert.Equal("rough", box.Shown?.Encoded);

        built.Clock.Advance(PaintedView.Quiet / 2);
        drawn.Redraw();
        Assert.Equal("rough", box.Shown?.Encoded);

        built.Clock.Advance(PaintedView.Quiet);
        drawn.Redraw();
        Assert.Equal("sharp", box.Shown?.Encoded);
    }

    /// <summary>
    ///     Nothing is encoded sharp while the page is moving — neither the cuts drawn nor those encoded ahead of the
    ///     scroll, since the next step of it is drawn rough too.
    /// </summary>
    [Fact]
    public async Task NothingIsEncodedSharpWhileThePageMoves()
    {
        var built = new AShell
        {
            Timelines = FakeTimelineReader.Holding(
                APost.With(id: "110", media: [APost.APicture("m1")]),
                APost.With(id: "220"),
                APost.With(id: "330"),
                APost.With(id: "440")),
        };
        var encoded = new List<int>();
        using var drawn = await Drawn(built, new SixelPictures((_, colours) =>
        {
            encoded.Add(colours);

            return "";
        }, work => work()));

        var box = Assert.Single(drawn.Content.SubViews.OfType<PictureView>(), view => view.Visible);
        var top = box.Frame.Y;

        // The page as it opened, still, which is drawn sharp.
        encoded.Clear();

        for (var notch = 0; notch < 100 && drawn.Content.Top < top + 2; notch++)
        {
            drawn.Wheel(OverContent, 3);
        }

        Assert.NotEmpty(encoded);
        Assert.All(encoded, colours => Assert.Equal(SixelPictures.RoughColours, colours));
    }

    private static Task<DrawnShell> Drawn(AShell built, SixelPictures sixels) => DrawnShell.Of(
        80,
        24,
        Themes.Plain,
        built,
        pictures: FakePictures.With().Holding("m1", 800, 400),
        drawsPictures: true,
        sixels: sixels);

    private static Task<DrawnShell> Drawn() => DrawnShell.Of(
        80,
        24,
        Themes.Plain,
        new AShell
        {
            Timelines = FakeTimelineReader.Holding(
                APost.With(id: "110", media: [APost.APicture("m1")]),
                APost.With(id: "220"),
                APost.With(id: "330"),
                APost.With(id: "440")),
        },
        pictures: FakePictures.With().Holding("m1", 800, 400),
        drawsPictures: true);
}
