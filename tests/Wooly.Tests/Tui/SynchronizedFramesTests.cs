using Wooly.Tests.Fakes;
using Wooly.Tui.Screens;
using Wooly.Tui.Theme;
using Wooly.Tui.Views;

namespace Wooly.Tests.Tui;

/// <summary>
///     Each frame with pictures in it wrapped in synchronized output, DEC mode 2026, so that a terminal shows it whole —
///     not an avatar a moment before the rule above it (#342).
/// </summary>
public class SynchronizedFramesTests
{
    private const int OverContent = RailLines.Width + 4;

    [Fact]
    public void ABlockIsOpenedOnceAndClosedOnce()
    {
        var written = new List<string>();
        var frames = new SynchronizedFrames(written.Add);

        frames.Close();
        frames.Open();
        frames.Open();
        frames.Close();
        frames.Close();

        Assert.Equal([SynchronizedFrames.Begin, SynchronizedFrames.End], written);
    }

    /// <summary>
    ///     A notch is drawn inside one block: opened as the content region starts drawing, before anything of the frame
    ///     can have been written, and closed once the application has drawn it.
    /// </summary>
    [Fact]
    public async Task AFrameOfTheContentIsDrawnInsideOneBlock()
    {
        var written = new List<string>();

        using var drawn = await DrawnShell.Of(
            80,
            24,
            Themes.Plain,
            new AShell
            {
                Timelines = FakeTimelineReader.Holding(
                    APost.With(id: "110", media: [APost.APicture("m1")]),
                    APost.With(id: "220"),
                    APost.With(id: "330")),
            },
            pictures: new FakePictures().Holding("m1", 800, 400),
            drawsPictures: true,
            frames: new SynchronizedFrames(written.Add));

        written.Clear();
        drawn.Wheel(OverContent, 3);

        Assert.Equal([SynchronizedFrames.Begin, SynchronizedFrames.End], written);
    }
}
