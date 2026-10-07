using Wooly.Core.Posts;
using Wooly.Tests.Fakes;
using Wooly.Tui.Media;
using Wooly.Tui.Rendering;
using Wooly.Tui.Screens;
using Wooly.Tui.Theme;

namespace Wooly.Tests.Tui;

/// <summary>
///     A <b>Stand-in</b>'s blur drawn through a box, read back off the whole shell drawn headless (#349). Wiring only:
///     a blur sent to a Kitty terminal drawing placeholders, and forgotten when its picture replaces it or it is
///     scrolled away, is <see cref="Placing" />'s (<see cref="PlacingTests" />, #362). The pixels are a manual smoke
///     test (ADR-0016).
/// </summary>
public class StandInBlurDrawingTests
{
    /// <summary>
    ///     Through a box — sixel, or Kitty on a terminal not known to draw its placeholders — the blur is drawn until
    ///     the picture lands, and the picture on the redraw its landing asks for. Wiring only: which box lets go of
    ///     what, and in what order, is <see cref="Placing" />'s (<see cref="PlacingTests" />).
    /// </summary>
    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task ABoxDrawsTheBlurAndThenThePictureOnceItLands(bool sixel, bool kitty)
    {
        var pictures = new FakePictures();

        using var drawn = await DrawnShell.Of(
            80,
            24,
            Themes.Plain,
            AShellWithAPictureAndItsBlurhash(),
            pictures: pictures,
            drawsPictures: sixel,
            answersKitty: kitty);

        var showing = Assert.Single(drawn.Content.SubViews.OfType<PictureView>(), view => view.Visible);
        Assert.Equal(Drawn.Blur(new Drawn("m1", "")).Id, showing.PictureId);

        pictures.Holding("m1", 800, 200);
        drawn.Redraw();

        Assert.Equal("m1", Assert.Single(drawn.Content.SubViews.OfType<PictureView>(), view => view.Visible).PictureId);
    }

    /// <summary>
    ///     The frame never wants a blur of the picture cache — only the picture itself — so blurs cannot crowd real
    ///     pictures out of its budget (ADR-0025).
    /// </summary>
    [Fact]
    public async Task TheFrameNeverWantsABlurOfThePictureCache()
    {
        var pictures = new FakePictures();

        using var drawn = await DrawnShell.Of(
            80,
            24,
            Themes.Plain,
            AShellWithAPictureAndItsBlurhash(),
            pictures: pictures,
            drawsPictures: true);

        Assert.NotEmpty(pictures.Sent);
        Assert.All(pictures.Sent, id => Assert.Equal("m1", id));
    }

    private static AShell AShellWithAPictureAndItsBlurhash() => new()
    {
        Timelines = FakeTimelineReader.Holding(
        [
            APost.With(
                id: "100",
                media: [APost.APicture("m1", shape: new PictureShape(800, 200), blurhash: StandInBlurTests.AHash)]),
            .. Enumerable.Range(1, 4).Select(at => APost.With(id: $"{100 + at}")),
        ]),
    };
}
