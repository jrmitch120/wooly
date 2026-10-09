using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Wooly.Tests.Fakes;
using Wooly.Tui.Media;
using Wooly.Tui.Rendering;
using Wooly.Tui.Screens;
using Wooly.Tui.Theme;

namespace Wooly.Tests.Tui;

/// <summary>
///     A picture drawn through a box is sent to the terminal again only where what the terminal shows of it could have
///     changed — never for a frame that redraws the rows around it and leaves it as it was. Typing a description redraws
///     its counter, and with it the panel the picture sits on, a frame a letter; a sixel sent each time is up to ten rows
///     of picture, which Windows Terminal draws slowly enough to lag the typing (review of #372 on Windows).
/// </summary>
public class PictureResendTests : IDisposable
{
    private readonly TemporaryDirectory _files = new();

    public void Dispose() => _files.Dispose();

    /// <summary>
    ///     Typing a description redraws its counter a letter at a time, and sends the picture above it once — when the
    ///     editor opens, not with every letter.
    /// </summary>
    [Fact]
    public async Task TypingADescriptionSendsThePictureOnce()
    {
        using var drawn = await Describing();

        Assert.Equal(1, drawn.SixelsSent());

        foreach (var letter in "A cat")
        {
            drawn.PressAsTheLoopDraws(new Key(letter));

            Assert.Equal(0, drawn.SixelsSent());
        }

        Assert.Contains(drawn.Rows(), row => row.Contains("5 / 1500", StringComparison.Ordinal));
    }

    /// <summary>Typing the post sends none of the pictures on the rows under Media again either.</summary>
    [Fact]
    public async Task TypingThePostSendsTheRowsPicturesOnce()
    {
        var cat = _files.WriteFile("cat.png");

        using var drawn = await DrawnShell.Of(
            100,
            30,
            Themes.Plain,
            pictures: new FakePictures().HoldingPhotograph(Drawn.Attaching(cat).Id, 40, 30),
            drawsPictures: true);

        drawn.Shell.Compose();
        drawn.Shell.Paste(cat);
        drawn.Settle();
        drawn.DrawAsTheLoopDraws();

        Assert.Equal(1, drawn.SixelsSent());

        foreach (var letter in "hello")
        {
            drawn.PressAsTheLoopDraws(new Key(letter));

            Assert.Equal(0, drawn.SixelsSent());
        }

        Assert.Equal("hello", Assert.IsType<ComposeScreen>(drawn.Shell.Screen).Text);
    }

    /// <summary>
    ///     Where anything was written over the picture's cells on the terminal, the next frame that draws it sends it
    ///     again, so that no hole is left where it was written over.
    /// </summary>
    [Fact]
    public async Task APictureWrittenOverIsSentAgain()
    {
        using var drawn = await Describing();

        var box = Assert.Single(drawn.Content.SubViews.OfType<PictureView>(), view => view.Visible);
        var at = box.FrameToScreen();
        var driver = drawn.Application.Driver!;

        drawn.PressAsTheLoopDraws(new Key('a'));

        Assert.Equal(0, drawn.SixelsSent());

        // Text drawn over a cell of the picture once everything under it is drawn, and written in that frame — as a
        // list hung over it is.
        void Over(object? sender, DrawEventArgs e)
        {
            driver.Move(at.X + 1, at.Y + 1);
            driver.AddStr("x");
        }

        drawn.Window.DrawComplete += Over;
        drawn.PressAsTheLoopDraws(new Key('b'));
        drawn.Window.DrawComplete -= Over;

        drawn.PressAsTheLoopDraws(new Key('c'));

        Assert.Equal(1, drawn.SixelsSent());

        drawn.PressAsTheLoopDraws(new Key('d'));

        Assert.Equal(0, drawn.SixelsSent());
    }

    /// <summary>
    ///     A whole screen drawn again over a cleared buffer, as a resize or a change of the window's layout draws it,
    ///     sends the picture again: what was on the terminal is no longer known from the buffer.
    /// </summary>
    [Fact]
    public async Task AWholeRedrawSendsThePictureAgain()
    {
        using var drawn = await Describing();

        drawn.PressAsTheLoopDraws(new Key('a'));

        Assert.Equal(0, drawn.SixelsSent());

        drawn.Redraw();

        Assert.Equal(1, drawn.SixelsSent());
    }

    /// <summary>The description editor opened on a picture dropped onto a fresh post, in the window, drawn.</summary>
    private async Task<DrawnShell> Describing()
    {
        var cat = _files.WriteFile("cat.png");

        var drawn = await DrawnShell.Of(
            80,
            24,
            Themes.Plain,
            pictures: new FakePictures().HoldingPhotograph(Drawn.OnDisk(cat).Id, 60, 40),
            drawsPictures: true);

        drawn.Shell.Compose();
        drawn.Shell.Paste(cat);
        drawn.Settle();
        drawn.Press(Key.CursorUp);
        drawn.Press(Key.Enter);

        Assert.IsType<DescriptionScreen>(drawn.Shell.Screen);
        Assert.Single(drawn.Content.SubViews.OfType<PictureView>(), view => view.Visible);

        // Opening the editor lays its field anew, which has Terminal.Gui clear the screen on the loop's next pass; that
        // pass comes before any key, and sends the picture again.
        drawn.DrawAsTheLoopDraws();

        return drawn;
    }
}
