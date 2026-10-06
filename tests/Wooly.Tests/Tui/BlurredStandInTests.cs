using Wooly.Core.Posts;
using Wooly.Tests.Fakes;
using Wooly.Tui.Media;
using Wooly.Tui.Rendering;
using Wooly.Tui.Screens;
using Wooly.Tui.Theme;

namespace Wooly.Tests.Tui;

/// <summary>
///     A <b>Stand-in</b> blurred from the instance's blurhash where it sent one (#349, ADR-0025): the same box, the
///     same rows, with the blur drawn over the shade through the same raster path a picture is — so that the picture
///     arriving reads as sharpening rather than popping in.
/// </summary>
/// <remarks>
///     Held against <see cref="PostLines" /> through <see cref="FakePictures" />, as <c>ReservedBoxTests</c> holds the
///     box itself: which rows there are, and what is set into them to be drawn. Drawing the blur through each raster
///     path is <c>BlurredStandInDrawingTests</c>'.
/// </remarks>
public class BlurredStandInTests
{
    /// <summary>The blurhash project's own example: four components across and three down.</summary>
    public const string Blurred = "LEHV6nWB2yk8pyo0adR*.7kCMdnj";

    private static readonly DateTimeOffset Now = new(2026, 7, 29, 12, 30, 0, TimeSpan.Zero);

    /// <summary>800×200 at 61 columns of 10×20-pixel cells: eight rows, as in <c>ReservedBoxTests</c>.</summary>
    private static readonly PictureShape Wide = new(800, 200);

    private static List<Line> StandIn(IEnumerable<Line> lines) =>
        [.. lines.Where(line => line.Has(Role.StandIn))];

    /// <summary>
    ///     With a blurhash and no pixels yet, the box's own top row carries the blur, the size of the whole box, and
    ///     the box keeps its shade under it — so a terminal still encoding the blur shows the fill, not nothing.
    /// </summary>
    [Fact]
    public void Feed_SetsTheBlurIntoTheWholeBoxWhereTheInstanceSentABlurhash()
    {
        var post = APost.With(media: [APost.APicture(shape: Wide, blurhash: Blurred)]);

        var lines = PostLines.Feed(post, new Drawing(61, Now, FakePictures.With()), default);

        var shaded = StandIn(lines);
        Assert.Equal(8, shaded.Count);

        var blur = Assert.Single(lines.SelectMany(line => line.Insets));
        Assert.Same(shaded[0], Assert.Single(lines, line => line.Insets.Count > 0));
        Assert.Equal((0, 61, 8), (blur.Column, blur.Columns, blur.Rows));
        Assert.NotNull(blur.Blurred);
        Assert.NotEqual("m1", blur.Drawn.Id);
    }

    /// <summary>
    ///     And the blur is replaced by the picture in place: the same rows, the box's top row carrying the picture now
    ///     and no blur anywhere.
    /// </summary>
    [Fact]
    public void Feed_ReplacesTheBlurWithThePictureInPlace()
    {
        var post = APost.With(media: [APost.APicture(shape: Wide, blurhash: Blurred)]);

        var waiting = PostLines.Feed(post, new Drawing(61, Now, FakePictures.With()), default);
        var held = PostLines.Feed(post, new Drawing(61, Now, FakePictures.With().Holding("m1", 800, 200)), default);

        Assert.Equal(waiting.Count, held.Count);
        Assert.Equal(
            waiting.ToList().FindIndex(line => line.Insets.Count > 0),
            held.ToList().FindIndex(line => line.Insets.Count > 0));

        var picture = Assert.Single(held.SelectMany(line => line.Insets));
        Assert.Equal("m1", picture.Drawn.Id);
        Assert.Null(picture.Blurred);
    }

    /// <summary>
    ///     No blurhash, or one that does not decode, is the shaded fill and nothing set into it — an instance's mistake
    ///     costs a blur, not a post.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("not a blurhash")]
    [InlineData("LEHV6nWB2yk8pyo0adR*.7kCMdn")]
    public void Feed_LeavesTheShadedFillWhereThereIsNoBlurToDraw(string? blurhash)
    {
        var post = APost.With(media: [APost.APicture(shape: Wide, blurhash: blurhash)]);

        var lines = PostLines.Feed(post, new Drawing(61, Now, FakePictures.With()), default);

        Assert.Equal(8, StandIn(lines).Count);
        Assert.Empty(lines.SelectMany(line => line.Insets));
    }

    /// <summary>
    ///     A warned post draws no blur, as it reserves no box, until it is asked past — a blur is a picture of the
    ///     picture, and its colours are part of what the warning hides. Asked past, the blur is there at once.
    /// </summary>
    [Fact]
    public void Feed_DrawsNoBlurForAWarnedPostUntilItIsAskedPast()
    {
        var post = APost.With(
            sensitive: true,
            media: [APost.APicture(shape: Wide, blurhash: Blurred)],
            linkPreview: APost.ALinkPreview(shape: Wide, blurhash: Blurred));

        var hidden = PostLines.Feed(post, new Drawing(61, Now, FakePictures.With()), default);

        Assert.Empty(hidden.SelectMany(line => line.Insets));

        var revealed = PostLines.Feed(post, new Drawing(61, Now, FakePictures.With()), new Reading(Revealed: true));

        Assert.Equal(2, revealed.SelectMany(line => line.Insets).Count(inset => inset.Blurred is not null));
    }

    /// <summary>A link preview's picture is blurred the same way, from its card's blurhash.</summary>
    [Fact]
    public void Feed_SetsTheBlurIntoALinkPreviewsBox()
    {
        var link = APost.ALinkPreview(shape: Wide, blurhash: Blurred);
        var post = APost.With(linkPreview: link);

        var waiting = PostLines.Feed(post, new Drawing(61, Now, FakePictures.With()), default);
        var held = PostLines.Feed(
            post,
            new Drawing(61, Now, FakePictures.With().HoldingLinkPreview(link, 800, 200)),
            default);

        var blur = Assert.Single(waiting.SelectMany(line => line.Insets));
        Assert.Equal((0, 61, 8), (blur.Column, blur.Columns, blur.Rows));
        Assert.NotNull(blur.Blurred);

        Assert.Equal(waiting.Count, held.Count);
        Assert.Null(Assert.Single(held.SelectMany(line => line.Insets)).Blurred);
    }

    /// <summary>
    ///     A blur is never asked of the picture cache, so it can never crowd a real picture out of it: the rows only
    ///     ever look up the picture itself.
    /// </summary>
    [Fact]
    public void Feed_NeverAsksThePictureCacheForTheBlur()
    {
        var pictures = FakePictures.With();
        var post = APost.With(media: [APost.APicture(shape: Wide, blurhash: Blurred)]);

        _ = PostLines.Feed(post, new Drawing(61, Now, pictures), default);

        Assert.All(pictures.Asked, id => Assert.Equal("m1", id));
    }
}
