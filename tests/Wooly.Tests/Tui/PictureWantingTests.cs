using Wooly.Core.Posts;
using Wooly.Tests.Fakes;
using Wooly.Tui.Media;
using Wooly.Tui.Rendering;
using Wooly.Tui.Screens;
using Wooly.Tui.Theme;
using Line = Wooly.Tui.Rendering.Line;

namespace Wooly.Tests.Tui;

/// <summary>
///     What a frame says it wants of <see cref="IPictures" />, said by <see cref="Placing" /> from the rows and the
///     scroll top the view hands it: the pictures on screen and near it, nearest first, with those on screen marked —
///     which is what lets the cache keep what a reader is looking at (ADR-0016, ADR-0025, #350, #363).
/// </summary>
public class PictureWantingTests
{
    private const int Width = 40;
    private const int Height = 10;

    private static readonly DateTimeOffset Now = new(2026, 7, 29, 12, 30, 0, TimeSpan.Zero);

    /// <summary>
    ///     Scrolled to row 20 of 60, ten rows of page: what is on the page comes first and is marked, then the rest
    ///     by how far it is from the page in either direction. A picture whose box is on the page is on screen even
    ///     when the row that wants it has been scrolled off the top, and a picture wanted twice is said once.
    /// </summary>
    [Fact]
    public void AFrameWantsWhatIsNearestFirstWithWhatIsOnScreenMarked()
    {
        var pictures = new FakePictures();
        var rows = Enumerable.Range(0, 60).Select(at => Line.Of($"row {at}", Role.Body)).ToArray();

        Wanting(rows, 15, "behind");
        Wanting(rows, 18, "box");
        rows[19] = rows[19] with { Insets = [new Inset(Picture("box"), Column: 0, Columns: 10, Rows: 4)] };
        Wanting(rows, 25, "here");
        Wanting(rows, 33, "ahead");
        Wanting(rows, 36, "here");
        Wanting(rows, 38, "far");

        Placed(pictures).Frame(rows, 20, Width, Height, ARaster.Sixel());

        Assert.Equal(
            [("box", true), ("here", true), ("ahead", false), ("behind", false), ("far", false)],
            pictures.Frames[^1].Select(wanted => (wanted.Drawn.Id, wanted.OnScreen)));
    }

    /// <summary>
    ///     A page that has not moved reaches two screens above it and two below, since there is no telling which way the
    ///     reader will go: scrolled to row 40 of 100, ten rows of page, that is rows 20 to 69 and nothing past them.
    /// </summary>
    [Fact]
    public void AStillPageWantsTwoScreensEitherSide()
    {
        var pictures = new FakePictures();

        Placed(pictures).Frame(Reach(), 40, Width, Height, ARaster.Sixel());

        Assert.Equal((20, 69), Edges(pictures.Frames[^1]));
    }

    /// <summary>
    ///     A page moving down reaches three screens below it and one above, so that what the reader is heading for is
    ///     usually here before they arrive: from row 40 down to 45, that is rows 35 to 84.
    /// </summary>
    [Fact]
    public void APageScrollingDownWantsThreeScreensBelowAndOneAbove()
    {
        var pictures = new FakePictures();
        var placing = Placed(pictures);

        placing.Frame(Reach(), 40, Width, Height, ARaster.Sixel());
        placing.Frame(Reach(), 45, Width, Height, ARaster.Sixel());

        Assert.Equal((35, 84), Edges(pictures.Frames[^1]));
    }

    /// <summary>
    ///     And moving up, the reverse: from row 45 up to 40, three screens above and one below — rows 10 to 59.
    /// </summary>
    [Fact]
    public void APageScrollingUpWantsThreeScreensAboveAndOneBelow()
    {
        var pictures = new FakePictures();
        var placing = Placed(pictures);

        placing.Frame(Reach(), 45, Width, Height, ARaster.Sixel());
        placing.Frame(Reach(), 40, Width, Height, ARaster.Sixel());

        Assert.Equal((10, 59), Edges(pictures.Frames[^1]));
    }

    /// <summary>
    ///     A frame drawn without the page moving — a picture landing redraws the screen — keeps reaching the way the
    ///     page last went. Falling back to two either side there would take the third screen ahead out of what is
    ///     wanted, and the fetches queued for it would be abandoned by the very picture whose arrival redrew it.
    /// </summary>
    [Fact]
    public void APageThatStopsKeepsReachingTheWayItWasGoing()
    {
        var pictures = new FakePictures();
        var placing = Placed(pictures);

        placing.Frame(Reach(), 40, Width, Height, ARaster.Sixel());
        placing.Frame(Reach(), 45, Width, Height, ARaster.Sixel());
        placing.Frame(Reach(), 45, Width, Height, ARaster.Sixel());

        Assert.Equal((35, 84), Edges(pictures.Frames[^1]));
    }

    /// <summary>
    ///     Rows resumed — another lot, or the same lot come back to — have not been moved through yet, so the first
    ///     frame of them reaches two screens either side again rather than the way the last rows were going.
    /// </summary>
    [Fact]
    public void ResumedRowsReachTwoScreensEitherSideAgain()
    {
        var pictures = new FakePictures();
        var placing = Placed(pictures);

        placing.Frame(Reach(), 40, Width, Height, ARaster.Sixel());
        placing.Frame(Reach(), 45, Width, Height, ARaster.Sixel());
        placing.Resume();
        placing.Frame(Reach(), 40, Width, Height, ARaster.Sixel());

        Assert.Equal((20, 69), Edges(pictures.Frames[^1]));
    }

    /// <summary>
    ///     A terminal that draws nothing still says what its frame wants — nothing, since its rows want nothing — so
    ///     that the cache is told every frame rather than left holding what the last terminal wanted (#362's note).
    /// </summary>
    [Fact]
    public void AFrameOnATerminalThatDrawsNothingStillSaysWhatItWants()
    {
        var pictures = new FakePictures();

        Placed(pictures).Frame(Reach(), 40, Width, Height, Raster.None);

        Assert.Equal((20, 69), Edges(pictures.Frames[^1]));
    }

    /// <summary>A frame with no room to draw in says nothing at all: there is no page to be near.</summary>
    [Fact]
    public void AFrameWithNoRoomWantsNothing()
    {
        var pictures = new FakePictures();

        Placed(pictures).Frame(Reach(), 40, Width, 0, ARaster.Sixel());

        Assert.Empty(pictures.Frames);
    }

    /// <summary>
    ///     The cache is handed the room each frame gives — its Raster and the page's columns — alongside what it wants,
    ///     so it decodes to it without ever asking the window for either (#359).
    /// </summary>
    [Fact]
    public void AFrameHandsTheCacheItsRoom()
    {
        var pictures = new FakePictures();
        var raster = ARaster.Kitty();

        Placed(pictures).Frame(Reach(), 40, 58, Height, raster);

        Assert.Equal((raster, 58), pictures.Rooms[^1]);
    }

    /// <summary>
    ///     A warned post's pictures are not wanted until the reader asks past the warning: its hidden attachments
    ///     carry no <c>Wants</c>, and only a picture some row wants is said at all (ADR-0016).
    /// </summary>
    [Fact]
    public void AWarnedPostsPicturesAreNotWantedUntilItIsAskedPast()
    {
        var pictures = new FakePictures();
        var placing = Placed(pictures);
        var post = APost.With(sensitive: true, media: [APost.APicture("m1")]);
        var drawing = new Drawing(Width, Now, pictures, ARaster.Sixel());

        placing.Frame(PostLines.Feed(post, drawing, default), 0, Width, Height, ARaster.Sixel());

        Assert.Empty(pictures.Frames[^1]);

        placing.Frame(PostLines.Feed(post, drawing, new Reading(Revealed: true)), 0, Width, Height, ARaster.Sixel());

        Assert.Equal(["m1"], pictures.Frames[^1].Select(wanted => wanted.Drawn.Id));
    }

    /// <summary>
    ///     A box drawing a Stand-in's blur wants the picture it stands in for and never the blur, so blurs cannot crowd
    ///     real pictures out of the cache's budget (ADR-0025, #349).
    /// </summary>
    [Fact]
    public void AStandInWantsItsPictureAndNeverItsBlur()
    {
        var pictures = new FakePictures();
        var post = APost.With(
            media: [APost.APicture("m1", shape: new PictureShape(800, 200), blurhash: StandInBlurTests.AHash)]);

        var drawing = new Drawing(Width, Now, pictures, ARaster.Sixel(), Blurs: new Blurs());
        var rows = PostLines.Feed(post, drawing, default);

        Assert.Contains(rows.SelectMany(row => row.Insets), inset => inset.Blur is not null);

        Placed(pictures).Frame(rows, 0, Width, Height, ARaster.Sixel());

        Assert.Equal(["m1"], pictures.Frames[^1].Select(wanted => wanted.Drawn.Id));
    }

    /// <summary>
    ///     Rows each wanting a picture named for the row, at the edges of every reach a page at rows 35 to 45 could
    ///     have — so that which of them a frame says is exactly where its reach begins and ends.
    /// </summary>
    private static Line[] Reach()
    {
        var rows = Enumerable.Range(0, 100).Select(at => Line.Of($"row {at}", Role.Body)).ToArray();

        foreach (var at in new[] { 4, 5, 9, 10, 19, 20, 24, 25, 34, 35, 59, 60, 69, 70, 84, 85 })
        {
            Wanting(rows, at, $"{at}");
        }

        return rows;
    }

    /// <summary>The first and last rows a frame reaches to, by the rows its pictures are named for.</summary>
    private static (int First, int Last) Edges(IReadOnlyList<WantedPicture> frame)
    {
        var at = frame.Select(wanted => int.Parse(wanted.Drawn.Id)).ToList();

        return (at.Min(), at.Max());
    }

    private static void Wanting(Line[] rows, int at, string id) => rows[at] = rows[at] with { Wants = Picture(id) };

    private static Drawn Picture(string id) => new(id, $"https://files.mastodon.social/{id}.png");

    /// <summary>
    ///     A Placing over fake boxes, its sixels encoded on the spot, wanting of <paramref name="pictures" />.
    /// </summary>
    private static Placing Placed(IPictures pictures) =>
        new(pictures, FakePictureBox.Pool([]), new SixelPictures((_, _) => "sixel", work => work()));
}
