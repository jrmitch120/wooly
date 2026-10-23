using Wooly.Core.Posts;
using Wooly.Tests.Fakes;
using Wooly.Tui.Media;
using Wooly.Tui.Rendering;
using Wooly.Tui.Theme;

namespace Wooly.Tests.Tui;

/// <summary>
///     A <b>Picture box</b>: the cells a <b>drawn</b> picture is painted in, and what every row of them holds before
///     and after its pixels arrive (#438). Fixed boxes — an avatar's, a Media row's, the public profile form's — are
///     filled; shaped boxes — a post's picture's, a link preview's — fit the picture inside, centred.
/// </summary>
public class PictureBoxTests
{
    private static readonly Drawn Face = new("m1", "https://files.example/m1.png");

    /// <summary>
    ///     Every way a picture is not drawn: nowhere for pixels to come from, no cell to draw at, or neither.
    /// </summary>
    public static TheoryData<bool, bool> NotDrawn => new()
    {
        { false, true },
        { true, false },
        { false, false },
    };

    private static IPictures? Pictures(bool given) => given ? new FakePictures() : null;

    private static Raster Raster(bool cell) => cell ? ARaster.Kitty() : Wooly.Tui.Media.Raster.None;

    [Fact]
    public void Fixed_HoldsTheShadeOnEveryRowUntilThePixelsAreHere()
    {
        var box = PictureBox.Fixed(Face, columns: 4, rows: 2, new FakePictures(), ARaster.Kitty());

        Assert.Equal(Face, box.Wanted);
        Assert.Equal(new Span("░░░░", Role.StandIn), box.Held);
        Assert.Empty(box.On(row: 0, column: 3));
        Assert.Empty(box.On(row: 1, column: 3));
    }

    /// <summary>
    ///     Once the pixels are here the box is blank, and the picture is set into its first row over the whole box —
    ///     filled, whatever its own shape, so the box is the inset.
    /// </summary>
    [Fact]
    public void Fixed_SetsThePictureIntoTheWholeBoxOnceItIsHere()
    {
        var pictures = new FakePictures().Holding("m1", 300, 900);

        var box = PictureBox.Fixed(Face, columns: 3, rows: 1, pictures, ARaster.Kitty());

        Assert.Equal(Face, box.Wanted);
        Assert.Equal("   ", box.Held.Text);
        Assert.NotEqual(Role.StandIn, box.Held.Role);
        Assert.Equal([new Inset(Face, Column: 7, Columns: 3, Rows: 1)], box.On(row: 0, column: 7));
    }

    [Fact]
    public void Fixed_SetsNothingIntoAnyRowButTheFirst()
    {
        var pictures = new FakePictures().Holding("m1", 96, 96);

        var box = PictureBox.Fixed(Face, columns: 8, rows: 4, pictures, ARaster.Kitty());

        Assert.All([1, 2, 3], row => Assert.Empty(box.On(row, column: 0)));
    }

    [Theory]
    [MemberData(nameof(NotDrawn))]
    public void Fixed_HoldsBlanksAndWantsNothingWhereNoPictureIsDrawn(bool pictures, bool cell)
    {
        var box = PictureBox.Fixed(Face, columns: 4, rows: 2, Pictures(pictures), Raster(cell));

        Assert.Null(box.Wanted);
        Assert.Equal("    ", box.Held.Text);
        Assert.NotEqual(Role.StandIn, box.Held.Role);
        Assert.Empty(box.On(row: 0, column: 0));
    }

    /// <summary>
    ///     A box with no picture to draw in it — a file chosen that cannot be decoded — is blank columns.
    /// </summary>
    [Fact]
    public void Fixed_HoldsBlanksWhereThereIsNoPicture()
    {
        var box = PictureBox.Fixed(wanted: null, columns: 3, rows: 1, new FakePictures(), ARaster.Kitty());

        Assert.Null(box.Wanted);
        Assert.Equal("   ", box.Held.Text);
        Assert.Empty(box.On(row: 0, column: 0));
    }

    /// <summary>
    ///     800×200 at 61 columns of 10×20-pixel cells is eight rows, every one shaded until the pixels are here, with
    ///     nothing set into any of them where no blurhash was sent.
    /// </summary>
    [Fact]
    public void Shaped_IsTheReportedShapeShadedUntilThePixelsAreHere()
    {
        var box = Shaped(new FakePictures(), new PictureShape(800, 200));

        Assert.NotNull(box);
        Assert.Equal((61, 8), (box.Columns, box.Rows));
        Assert.Equal(new Span(new string('░', 61), Role.StandIn), box.Held);
        Assert.All(Enumerable.Range(0, 8), row => Assert.Empty(box.On(row, column: 0)));
    }

    /// <summary>The blur is set into the box's top row over the whole box, and the shade stays under it.</summary>
    [Fact]
    public void Shaped_KeepsTheBlurOnItsTopRow()
    {
        var box = Shaped(new FakePictures(), new PictureShape(800, 200), StandInBlurTests.AHash);

        Assert.NotNull(box);
        Assert.Equal(Role.StandIn, box.Held.Role);

        var blur = Assert.Single(box.On(row: 0, column: 2));

        Assert.Equal((2, 61, 8), (blur.Column, blur.Columns, blur.Rows));
        Assert.Equal(Drawn.Blur(Face), blur.Drawn);
        Assert.NotNull(blur.Blur);
        Assert.All(Enumerable.Range(1, 7), row => Assert.Empty(box.On(row, column: 2)));
    }

    /// <summary>
    ///     A square picture in a box the instance said was four times as wide: fitted inside, as tall as the box, with
    ///     the slack shared either side of it.
    /// </summary>
    [Fact]
    public void Shaped_FitsPixelsOfAnotherShapeInsideItCentredAcross()
    {
        var box = Shaped(new FakePictures().Holding("m1", 200, 200), new PictureShape(800, 200));

        Assert.NotNull(box);
        Assert.Equal(new string(' ', 61), box.Held.Text);
        Assert.NotEqual(Role.StandIn, box.Held.Role);
        Assert.Equal([new Inset(Face, Column: 25, Columns: 16, Rows: 8)], box.On(row: 0, column: 3));
    }

    /// <summary>
    ///     A wide picture in a box the instance said was square: twenty columns by ten rows, with the picture's three
    ///     rows set into the fourth, so the slack is shared above and below.
    /// </summary>
    [Fact]
    public void Shaped_FitsPixelsOfAnotherShapeInsideItCentredDown()
    {
        var box = PictureBox.Shaped(
            Face,
            new PictureShape(100, 100),
            blurhash: null,
            width: 20,
            mostRows: Inset.FeedRows,
            new FakePictures().Holding("m1", 400, 100),
            ARaster.Kitty(),
            blurs: null);

        Assert.NotNull(box);
        Assert.Equal((20, 10), (box.Columns, box.Rows));
        Assert.Equal([new Inset(Face, Column: 0, Columns: 20, Rows: 3)], box.On(row: 3, column: 0));
        Assert.All(new[] { 0, 1, 2, 4, 5 }, row => Assert.Empty(box.On(row, column: 0)));
    }

    /// <summary>
    ///     Where the instance reported no shape, the box is sixteen by nine: full width, narrowed here to the feed's
    ///     cap.
    /// </summary>
    [Fact]
    public void Shaped_IsSixteenByNineWhereNoShapeWasReported()
    {
        var box = Shaped(new FakePictures(), shape: null);

        Assert.NotNull(box);
        Assert.Equal((57, 16), (box.Columns, box.Rows));
    }

    [Theory]
    [MemberData(nameof(NotDrawn))]
    public void Shaped_IsNoBoxWhereNoPictureIsDrawn(bool pictures, bool cell)
    {
        var box = PictureBox.Shaped(
            Face,
            new PictureShape(800, 200),
            StandInBlurTests.AHash,
            width: 61,
            mostRows: Inset.FeedRows,
            Pictures(pictures),
            Raster(cell),
            new Blurs());

        Assert.Null(box);
    }

    private static PictureBox? Shaped(IPictures pictures, PictureShape? shape, string? blurhash = null) =>
        PictureBox.Shaped(
            Face,
            shape,
            blurhash,
            width: 61,
            mostRows: Inset.FeedRows,
            pictures,
            ARaster.Kitty(),
            new Blurs());
}
