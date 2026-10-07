using System.Drawing;
using Terminal.Gui.App;
using Terminal.Gui.Drawing;
using Terminal.Gui.Drivers;
using Terminal.Gui.Views;
using Wooly.Tests.Fakes;
using Wooly.Tui.Media;
using Wooly.Tui.Rendering;
using Wooly.Tui.Theme;
using Wooly.Tui.Views;
using Attribute = Terminal.Gui.Drawing.Attribute;
using Line = Wooly.Tui.Rendering.Line;

namespace Wooly.Tests.Tui;

/// <summary>
///     A region drawn inside a panel's frame (#270, ADR-0021): the one place a view draws an edge round rows it did not
///     build. The rows and the pictures placed from them are inset by the frame, and the frame's cells are the theme's
///     answer for the panel roles. Drawn headless and read back off the terminal's cells.
/// </summary>
public class FramedRegionTests
{
    private const int Width = 12;

    private const int Height = 6;

    [Fact]
    public async Task TheRowsAreDrawnInsideTheFrame()
    {
        using var drawn = await Draw(Themes.Dark, [Line.Of("first", Role.Body), Line.Of("second", Role.Body)]);

        Assert.Equal(
            [
                "╭ Home ────╮",
                "│first     │",
                "│second    │",
                "│          │",
                "│          │",
                "╰──────────╯",
            ],
            drawn.Rows());
    }

    [Fact]
    public async Task TheRegionIsAsWideAndAsTallAsTheInsideOfItsFrame()
    {
        using var drawn = await Draw(Themes.Dark, []);

        Assert.Equal(new Size(Width - 2, Height - 2), drawn.View.Viewport.Size);
    }

    [Theory]
    [InlineData(false, Role.PanelBorder)]
    [InlineData(true, Role.PanelBorderActive)]
    public async Task TheFramesCellsAreTheThemesAnswerForThePanelRoles(bool active, Role edge)
    {
        using var drawn = await Draw(Themes.Dark, [], active);

        var border = Themes.Dark.For(edge);
        var title = Themes.Dark.For(Role.PanelTitle);

        Assert.Equal(border, drawn.Cell(0, 0));
        Assert.Equal(title, drawn.Cell(0, 3));
        Assert.Equal(border, drawn.Cell(0, Width - 1));
        Assert.Equal(border, drawn.Cell(2, 0));
        Assert.Equal(border, drawn.Cell(2, Width - 1));
        Assert.Equal(border, drawn.Cell(Height - 1, 4));
    }

    [Fact]
    public async Task WithoutColourTheBoxCharactersStillDivideTheRegion()
    {
        using var drawn = await Draw(Themes.Plain, [Line.Of("first", Role.Body)]);

        Assert.Equal("╭ Home ────╮", drawn.Rows()[0]);
        Assert.Equal("│first     │", drawn.Rows()[1]);
        Assert.Equal("╰──────────╯", drawn.Rows()[^1]);
    }

    /// <summary>
    ///     The picture's box is placed from the inside's rows: scrolled half off the top, what is left of it starts on
    ///     the inside's first row, and not one of its cells is on the frame.
    /// </summary>
    [Fact]
    public async Task APictureHalfAboveTheTopDrawsNoCellOnTheFrame()
    {
        // Twice as tall as it is wide, in pixels, which at ten by twenty to a cell fills its four-by-four box.
        var drawn = new Drawn("m1", "https://files.example/m1.png");
        var pictures = new FakePictures().Holding("m1", 40, 80);

        Line[] lines =
        [
            Line.Of("▒▒▒▒", Role.Media) with { Insets = [new Inset(drawn, Column: 0, Columns: 4, Rows: 4)] },
            Line.Of("▒▒▒▒", Role.Media),
            Line.Of("▒▒▒▒", Role.Media),
            Line.Of("▒▒▒▒", Role.Media),
            .. Enumerable.Repeat(Line.Of("after", Role.Body), 8),
        ];

        using var shown = await Draw(Themes.Dark, lines, pictures: pictures, top: 2);

        var cells = shown.PictureCells();

        Assert.NotEmpty(cells);
        Assert.Equal(1, cells.Min(cell => cell.Y));
        Assert.DoesNotContain(cells, cell => cell.Y == 0 || cell.X == 0 || cell.X == Width - 1);
    }

    /// <summary>And one half past the foot leaves the bottom edge alone.</summary>
    [Fact]
    public async Task APictureHalfBelowTheFootDrawsNoCellOnTheFrame()
    {
        var drawn = new Drawn("m1", "https://files.example/m1.png");
        var pictures = new FakePictures().Holding("m1", 40, 80);

        Line[] lines =
        [
            Line.Of("one", Role.Body),
            Line.Of("two", Role.Body),
            Line.Of("▒▒▒▒", Role.Media) with { Insets = [new Inset(drawn, Column: 0, Columns: 4, Rows: 4)] },
            Line.Of("▒▒▒▒", Role.Media),
            Line.Of("▒▒▒▒", Role.Media),
            Line.Of("▒▒▒▒", Role.Media),
        ];

        using var shown = await Draw(Themes.Dark, lines, pictures: pictures);

        var cells = shown.PictureCells();

        Assert.NotEmpty(cells);
        Assert.Equal(Height - 2, cells.Max(cell => cell.Y));
    }

    /// <summary>
    ///     <paramref name="lines" /> drawn once by a framed region filling a <see cref="Width" />×<see cref="Height" />
    ///     terminal, on a terminal able to draw pictures where <paramref name="pictures" /> is given.
    /// </summary>
    private static async Task<Shown> Draw(
        ITheme theme,
        IReadOnlyList<Line> lines,
        bool active = false,
        IPictures? pictures = null,
        int top = 0)
    {
        await Task.Yield();

        var application = Application.Create();
        application.Init("ansi");
        application.Driver!.SetScreenSize(Width, Height);

        if (pictures is not null)
        {
            application.Driver.SetKittyGraphicsSupport(
                new KittyGraphicsSupportResult { IsSupported = true, Resolution = new Size(10, 20) });
        }

        var window = new Runnable { BorderStyle = LineStyle.None };
        var view = new PaintedView(
            theme,
            (_, _) => lines,
            pictures,
            (width, height) => Panel.Framed("Home", [], width, height, active),
            raster: () => Raster.Of(application.Driver, placeholders: false, () => null))
        {
            Width = Width,
            Height = Height,
            Scrolls = top > 0,
        };

        view.Resume(top, following: false);
        window.Add(view);

        application.Begin(window);
        application.LayoutAndDraw(true);

        return new Shown(application, window, view);
    }

    private sealed record Shown(IApplication Application, Runnable Window, PaintedView View) : IDisposable
    {
        public string[] Rows()
        {
            var cells = Application.Driver!.Contents!;

            return
            [
                .. Enumerable.Range(0, cells.GetLength(0)).Select(row => string.Concat(
                    Enumerable.Range(0, cells.GetLength(1)).Select(column => cells[row, column].Grapheme))),
            ];
        }

        public Attribute Cell(int row, int column) => Application.Driver!.Contents![row, column].Attribute!.Value;

        /// <summary>Every cell a picture was drawn in: where it was placed, cut to where the terminal was let draw it.</summary>
        public List<Point> PictureCells()
        {
            var cells = new List<Point>();

            foreach (var image in Application.Driver!.GetOutputBuffer().GetRasterImages())
            {
                var clip = image.Clip?.GetRectangles() ?? [image.DestinationCells];

                foreach (var visible in clip.Select(rectangle => Rectangle.Intersect(rectangle, image.DestinationCells)))
                {
                    for (var y = visible.Top; y < visible.Bottom; y++)
                    {
                        for (var x = visible.Left; x < visible.Right; x++)
                        {
                            cells.Add(new Point(x, y));
                        }
                    }
                }
            }

            return cells;
        }

        public void Dispose()
        {
            Window.Dispose();
            Application.Dispose();
        }
    }
}
