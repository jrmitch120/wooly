using Wooly.Core.Posts;
using Wooly.Tests.Fakes;
using Wooly.Tui.Screens;
using Wooly.Tui.Theme;

namespace Wooly.Tests.Tui;

/// <summary>
///     Every shaped attachment box gives up a column either side, walked or not, which is the room a walked picture's
///     brackets are drawn in so that walking to it moves nothing (#519, ADR-0034). Read back off the whole shell drawn
///     headless: the columns a box's <b>Stand-in</b> shades and its pixels land in, against the content region's.
/// </summary>
public class AttachmentBoxColumnsTests
{
    private const char Shade = '░';

    private const string Placeholder = "\U0010EEEE";

    /// <summary>
    ///     The first column a screen draws in on an 80-column terminal: just past the rail's 20 columns, the panel's
    ///     left edge and its margin.
    /// </summary>
    private const int FirstDrawn = RailLines.Width + 2;

    /// <summary>The last column a screen draws in there: the one before the margin inside the panel's right edge.</summary>
    private const int LastDrawn = 77;

    /// <summary>Wider than any content region, so that the region's width is what bounds the box.</summary>
    private static readonly PictureShape Wide = new(4000, 400);

    [Fact]
    public async Task AStillPicturesBoxIsTwoColumnsNarrowerThanTheContentRegionWithAnEmptyColumnEitherSide()
    {
        using var drawn = await Drawn(APost.With(id: "110", media: [APost.APicture("m1", shape: Wide)]));

        var rows = ShadedRows(drawn);

        Assert.NotEmpty(rows);
        Assert.All(rows, row =>
        {
            Assert.Equal((FirstDrawn + 1, LastDrawn - 1), Across(drawn, row));
            Assert.Equal(' ', At(drawn, row, FirstDrawn));
            Assert.Equal(' ', At(drawn, row, LastDrawn));
        });
    }

    /// <summary>
    ///     A video's preview gives up the same columns, so that it lines up with a picture's on the same post.
    /// </summary>
    [Theory]
    [InlineData(MediaKind.Video)]
    [InlineData(MediaKind.Animation)]
    public async Task AVideosPreviewBoxIsNarrowedTheSameWayAndLinesUpWithAPicturesBesideIt(MediaKind kind)
    {
        using var drawn = await Drawn(APost.With(
            id: "110",
            media: [APost.APicture("m1", shape: Wide), APost.Attached(kind, "m2", shape: Wide)]));

        var rows = ShadedRows(drawn);
        var picture = rows.TakeWhile((row, at) => at == 0 || rows[at - 1] == row - 1).ToList();
        var preview = rows.Skip(picture.Count).ToList();

        Assert.NotEmpty(picture);
        Assert.NotEmpty(preview);
        Assert.All(preview, row => Assert.Equal((FirstDrawn + 1, LastDrawn - 1), Across(drawn, row)));
        Assert.All(picture, row => Assert.Equal(Across(drawn, preview[0]), Across(drawn, row)));
    }

    /// <summary>
    ///     Nothing about a picture moves between its Stand-in and its pixels landing: Kitty's placeholder cells take
    ///     the columns the shade took, on the rows it took.
    /// </summary>
    [Fact]
    public async Task APicturesPixelsLandInTheColumnsItsStandInShaded()
    {
        var pictures = new FakePictures();

        using var drawn = await Drawn(
            APost.With(id: "110", media: [APost.APicture("m1", shape: Wide)]),
            pictures,
            placeholders: true);

        var shaded = ShadedRows(drawn).ToDictionary(row => row, row => Across(drawn, row));

        pictures.Holding("m1", Wide.Width, Wide.Height);
        drawn.Redraw();

        var landed = PlaceholderCells(drawn)
            .GroupBy(cell => cell.Row)
            .ToDictionary(row => row.Key, row => (row.Min(cell => cell.Column), row.Max(cell => cell.Column)));

        Assert.NotEmpty(shaded);
        Assert.Equal(shaded, landed);
    }

    /// <summary>A byline's avatar is a fixed box, and is drawn at the region's first column, four wide, as before.</summary>
    [Fact]
    public async Task AnAvatarsBoxIsDrawnAsBefore()
    {
        using var drawn = await Drawn(APost.With(
            id: "110",
            avatarUrl: "https://files.mastodon.social/avatars/original.png"));

        var rows = ShadedRows(drawn);

        Assert.Equal(2, rows.Count);
        Assert.All(rows, row => Assert.Equal((FirstDrawn, FirstDrawn + 3), Across(drawn, row)));
    }

    /// <summary>A link preview's picture is not an attachment, and keeps the region's whole width.</summary>
    [Fact]
    public async Task ALinkPreviewsPictureKeepsTheWholeWidth()
    {
        using var drawn = await Drawn(APost.With(id: "110", linkPreview: APost.ALinkPreview(shape: Wide)));

        var rows = ShadedRows(drawn);

        Assert.NotEmpty(rows);
        Assert.All(rows, row => Assert.Equal((FirstDrawn, LastDrawn), Across(drawn, row)));
    }

    /// <summary>The shell on an 80-column terminal drawing pictures, reading <paramref name="post" /> and two more.</summary>
    private static Task<DrawnShell> Drawn(Post post, FakePictures? pictures = null, bool placeholders = false) =>
        DrawnShell.Of(
            80,
            40,
            Themes.Plain,
            new AShell { Timelines = FakeTimelineReader.Holding(post, APost.With(id: "220"), APost.With(id: "330")) },
            pictures: pictures ?? new FakePictures(),
            drawsPictures: !placeholders,
            kittyImages: placeholders ? new FakeTerminalImages() : null,
            drawsPlaceholders: placeholders);

    /// <summary>Every row of the screen with the Stand-in's shade on it, top to bottom.</summary>
    private static List<int> ShadedRows(DrawnShell drawn) =>
    [
        .. Enumerable.Range(0, Height(drawn))
            .Where(row => Enumerable.Range(0, Width(drawn)).Any(column => At(drawn, row, column) == Shade)),
    ];

    /// <summary>The first and last column shaded on <paramref name="row" />.</summary>
    private static (int First, int Last) Across(DrawnShell drawn, int row)
    {
        var shaded = Enumerable.Range(0, Width(drawn)).Where(column => At(drawn, row, column) == Shade).ToList();

        return (shaded.Min(), shaded.Max());
    }

    /// <summary>The one character drawn at <paramref name="column" /> of <paramref name="row" />.</summary>
    private static char At(DrawnShell drawn, int row, int column) =>
        drawn.Application.Driver!.Contents![row, column].Grapheme is [var only] ? only : '\0';

    /// <summary>Every placeholder cell on screen, with where it is.</summary>
    private static List<(int Row, int Column)> PlaceholderCells(DrawnShell drawn) =>
    [
        .. Enumerable.Range(0, Height(drawn))
            .SelectMany(row => Enumerable.Range(0, Width(drawn)).Select(column => (row, column)))
            .Where(cell => drawn.Application.Driver!.Contents![cell.row, cell.column].Grapheme
                .StartsWith(Placeholder, StringComparison.Ordinal)),
    ];

    private static int Height(DrawnShell drawn) => drawn.Application.Driver!.Contents!.GetLength(0);

    private static int Width(DrawnShell drawn) => drawn.Application.Driver!.Contents!.GetLength(1);
}
