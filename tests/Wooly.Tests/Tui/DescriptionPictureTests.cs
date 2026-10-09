using System.Drawing;
using Terminal.Gui.Input;
using Wooly.Tests.Fakes;
using Wooly.Tui.Media;
using Wooly.Tui.Rendering;
using Wooly.Tui.Screens;
using Wooly.Tui.Shell;
using Wooly.Tui.Theme;
using Wooly.Tui.Views;

namespace Wooly.Tests.Tui;

/// <summary>
///     The picture in the description editor (#382, story 42): top left, beside the field and level with its label on
///     a wide panel, above the label on a narrow one — where the terminal can draw, and nowhere where it cannot.
/// </summary>
public class DescriptionPictureTests : IDisposable
{
    private const int Narrow = 78;

    private const int Wide = 98;

    private const int Height = 21;

    private const int Pad = 2;

    private readonly TemporaryDirectory _files = new();

    public void Dispose() => _files.Dispose();

    /// <summary>
    ///     On a wide panel the picture sits top left, level with the label, no wider than the half the field leaves.
    /// </summary>
    [Fact]
    public async Task OnAWidePanelThePictureSitsBesideTheFieldLevelWithItsLabel()
    {
        var (describing, cat) = await Describing("cat.png");
        var lines = Lines(describing, Wide, new FakePictures().Holding(Drawn.OnDisk(cat).Id, 400, 300), ARaster.Sixel());

        var inset = Assert.Single(lines[1].Insets);

        Assert.Equal(Drawn.OnDisk(cat), inset.Drawn);
        Assert.Equal(Pad, inset.Column);
        Assert.True(Pad + inset.Columns < describing.FieldAt(new Size(Wide, Height)).X);
        Assert.Contains("Description (alt text)", lines[1].Text, StringComparison.Ordinal);
        Assert.Equal(Drawn.OnDisk(cat), lines[1].Wants);
    }

    /// <summary>
    ///     On a narrow panel the picture sits above the label, across the panel, in a place held for it from the first
    ///     frame: the label, the field and the counter are where they are whether the pixels are here or not.
    /// </summary>
    [Fact]
    public async Task OnANarrowPanelThePictureSitsAboveTheLabelInAPlaceHeldForIt()
    {
        var (describing, cat) = await Describing("cat.png");
        var here = new FakePictures().Holding(Drawn.OnDisk(cat).Id, 400, 300);

        var before = Lines(describing, Narrow, new FakePictures(), ARaster.Sixel());
        var after = Lines(describing, Narrow, here, ARaster.Sixel());
        var inset = Assert.Single(after[1].Insets);
        var label = Array.FindIndex([.. after], line => line.Text.Contains("Description (alt text)", StringComparison.Ordinal));
        var field = describing.FieldAt(new Size(Narrow, Height), ARaster.Sixel());

        Assert.Equal(Drawn.OnDisk(cat), before[1].Wants);
        Assert.Equal(Pad, inset.Column);
        Assert.True(1 + inset.Rows < label, "a blank row at least between the picture and the label");
        Assert.Equal(label + 2, field.Y);
        Assert.Equal(before.Select(line => line.Text), after.Select(line => line.Text));
        Assert.Equal(Height, after.Count);
    }

    /// <summary>
    ///     On a terminal that cannot draw, or for an attachment with no picture to read, nothing is held above the label
    ///     and nothing is asked for: the editor is laid out as it was before pictures were drawn in it.
    /// </summary>
    [Theory]
    [InlineData("cat.png", false)]
    [InlineData("clip.mp4", true)]
    public async Task WithNoPictureToDrawNothingIsHeldAboveTheLabel(string name, bool draws)
    {
        var (describing, _) = await Describing(name);
        var raster = draws ? ARaster.Sixel() : Raster.None;

        var lines = Lines(describing, Narrow, new FakePictures(), raster);

        Assert.Contains("Description (alt text)", lines[1].Text, StringComparison.Ordinal);
        Assert.Equal(3, describing.FieldAt(new Size(Narrow, Height), raster).Y);
        Assert.All(lines, line => Assert.Null(line.Wants));
        Assert.Empty(lines.SelectMany(line => line.Insets));
    }

    /// <summary>
    ///     Wiring: in the window on a narrow sixel terminal, the picture is drawn through one of the content panel's
    ///     boxes above the label, and the field the author types into sits under the label, below it.
    /// </summary>
    [Fact]
    public async Task InTheWindowThePictureIsDrawnAboveTheFieldOnANarrowTerminal()
    {
        var cat = _files.WriteFile("cat.png");

        using var drawn = await DrawnShell.Of(
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

        var box = Assert.Single(drawn.Content.SubViews.OfType<PictureView>(), view => view.Visible);
        var label = Array.FindIndex(drawn.Rows(), row => row.Contains("Description (alt text)", StringComparison.Ordinal));
        var editor = drawn.Window.SubViews.OfType<DescriptionView>().Single().SubViews.OfType<ComposeEditor>().Single();

        Assert.Equal(Drawn.OnDisk(cat).Id, box.PictureId);
        Assert.True(box.FrameToScreen().Bottom < label);
        Assert.Equal(label + 2, editor.FrameToScreen().Y);
    }

    /// <summary>The description editor opened on <paramref name="name" />, dropped onto a fresh post.</summary>
    private async Task<(DescriptionScreen Describing, string Path)> Describing(string name)
    {
        var built = new AShell();
        var shell = await built.Opened();

        ComposeRows.Open(shell, ComposeFor.Post);
        built.Host.Drain();

        var path = _files.WriteFile(name);

        shell.Paste(path);
        _ = shell.Screen.Lines(new Drawing(Narrow, AShell.Now, Height: Height));
        shell.Press(ShellKey.Up);
        shell.Press(ShellKey.Enter);

        return (Assert.IsType<DescriptionScreen>(shell.Screen), path);
    }

    private static IReadOnlyList<Line> Lines(DescriptionScreen describing, int width, IPictures pictures, Raster raster) =>
        describing.Lines(new Drawing(width, AShell.Now, pictures, raster, Height: Height));
}
