using Wooly.Core.Posts;
using Wooly.Tests.Fakes;
using Wooly.Tui.Media;
using Wooly.Tui.Rendering;
using Wooly.Tui.Screens;
using Wooly.Tui.Theme;

namespace Wooly.Tests.Tui;

/// <summary>
///     A <b>Stand-in</b>'s blur drawn through each raster path, read back off the whole shell drawn headless (#349). The
///     pixels are a manual smoke test (ADR-0016); what holds the blur, what the terminal is sent, and what it is told to
///     forget are here — a blur sent to the terminal is let go of when the picture replaces it or it is scrolled away,
///     the same as a picture is (ADR-0022).
/// </summary>
public class StandInBlurDrawingTests
{
    private const string Placeholder = "\U0010EEEE";

    /// <summary>
    ///     On a Kitty terminal drawing placeholders, the blur is sent and drawn in the box's cells; when the picture
    ///     lands it is sent in the blur's place, and the terminal is told to forget the blur.
    /// </summary>
    [Fact]
    public async Task KittyDrawsTheBlurThenForgetsItWhenThePictureReplacesIt()
    {
        var terminal = new FakeTerminalImages();
        var pictures = new FakePictures();

        using var drawn = await DrawnShell.Of(
            80,
            24,
            Themes.Plain,
            AShellWithAPictureAndItsBlurhash(),
            pictures: pictures,
            kittyImages: terminal,
            drawsPlaceholders: true);

        var blur = Assert.Single(terminal.Transmitted);
        Assert.NotEmpty(PlaceholderCells(drawn));

        pictures.Holding("m1", 800, 200);
        drawn.Redraw();

        Assert.Equal(2, terminal.Transmitted.Count);
        Assert.Contains(blur.Id, terminal.Forgotten);
        Assert.DoesNotContain(terminal.Transmitted[1].Id, terminal.Forgotten);
    }

    /// <summary>
    ///     And a blur scrolled far enough from the page is forgotten by the terminal, as it would be by the client:
    ///     a blur nobody deletes is a Kitty image the terminal holds for the rest of the run.
    /// </summary>
    [Fact]
    public async Task KittyForgetsABlurScrolledFarFromThePage()
    {
        var terminal = new FakeTerminalImages();

        using var drawn = await DrawnShell.Of(
            80,
            24,
            Themes.Plain,
            AShellWithAPictureAndItsBlurhash(followedBy: 30),
            pictures: new FakePictures(),
            kittyImages: terminal,
            drawsPlaceholders: true);

        var blur = Assert.Single(terminal.Transmitted);

        for (var notch = 0; notch < 200 && !terminal.Forgotten.Contains(blur.Id); notch++)
        {
            drawn.Wheel(RailLines.Width + 4, 3);
        }

        Assert.Contains(blur.Id, terminal.Forgotten);
    }

    /// <summary>
    ///     And a window made too small to draw the page in at all forgets the blurs it was holding, as it releases its
    ///     boxes: the page has nothing near it any more, so a blur kept would be held for as long as the window stays
    ///     that small, and for the rest of the run if it never grows back.
    /// </summary>
    [Fact]
    public async Task KittyForgetsABlurWhenThereIsNoRoomLeftToDrawThePage()
    {
        var terminal = new FakeTerminalImages();

        using var drawn = await DrawnShell.Of(
            80,
            24,
            Themes.Plain,
            AShellWithAPictureAndItsBlurhash(),
            pictures: new FakePictures(),
            kittyImages: terminal,
            drawsPlaceholders: true);

        var blur = Assert.Single(terminal.Transmitted);

        drawn.Application.Driver!.SetScreenSize(80, 1);
        drawn.Redraw();

        Assert.Contains(blur.Id, terminal.Forgotten);
    }

    /// <summary>
    ///     Through a box — sixel, or Kitty on a terminal not known to draw its placeholders — the blur is held by a
    ///     box of its own, and that box lets go of it when the picture lands: the picture is never put over a blur
    ///     the terminal has not been told to drop.
    /// </summary>
    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task ABoxDrawsTheBlurThenLetsGoOfItWhenThePictureReplacesIt(bool sixel, bool kitty)
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
        Assert.DoesNotContain(
            drawn.Content.SubViews.OfType<PictureView>(),
            view => view.PictureId?.StartsWith("blur:", StringComparison.Ordinal) == true);
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

    private static AShell AShellWithAPictureAndItsBlurhash(int followedBy = 4) => new()
    {
        Timelines = FakeTimelineReader.Holding(
        [
            APost.With(
                id: "100",
                media: [APost.APicture("m1", shape: new PictureShape(800, 200), blurhash: StandInBlurTests.AHash)]),
            .. Enumerable.Range(1, followedBy).Select(at => APost.With(id: $"{100 + at}")),
        ]),
    };

    private static List<(int Row, int Column)> PlaceholderCells(DrawnShell drawn)
    {
        var contents = drawn.Application.Driver!.Contents!;
        var cells = new List<(int, int)>();

        for (var row = 0; row < contents.GetLength(0); row++)
        {
            for (var column = 0; column < contents.GetLength(1); column++)
            {
                if (contents[row, column].Grapheme.StartsWith(Placeholder, StringComparison.Ordinal))
                {
                    cells.Add((row, column));
                }
            }
        }

        return cells;
    }
}
