using System.Drawing;
using Wooly.Tests.Fakes;
using Wooly.Tui.Media;
using Wooly.Tui.Rendering;
using Wooly.Tui.Theme;
using Kind = Wooly.Tests.Fakes.FakePictureBox.Kind;
using Line = Wooly.Tui.Rendering.Line;

namespace Wooly.Tests.Tui;

/// <summary>
///     Drawn pictures put through boxes by <see cref="Placing" />, a frame at a time (#361): handed the rows, where the
///     scroll has got to, the page and the Raster, and read back off the fake boxes it was given — what each was shown,
///     where it sat, and when it was let go. The pixels are a manual smoke test (ADR-0016).
/// </summary>
public class PlacingTests
{
    private const int Width = 40;
    private const int Height = 10;

    /// <summary>
    ///     On a sixel terminal a box on the page is shown a cut of its picture, framed over the rows reserved for it,
    ///     and made visible — in that order, so that it is never drawn empty or in the wrong place.
    /// </summary>
    [Fact]
    public void ASixelBoxIsShownItsCutFramedOverItsRowsAndMadeVisible()
    {
        var frame = new AFrame(new FakePictures().Holding("m1", 40, 80));
        var rows = Rows(Box("m1", at: 2, column: 3, columns: 4, rows: 4));

        frame.Place(rows, top: 0, ARaster.Sixel());

        Assert.Equal(
            [
                new FakePictureBox.Call(0, Kind.ShownSixel, "m1"),
                new FakePictureBox.Call(0, Kind.Framed, "m1", Frame: new Rectangle(3, 2, 4, 4)),
                new FakePictureBox.Call(0, Kind.Visible, "m1", Visible: true),
            ],
            frame.Log.Where(call => call.Picture is not null));
    }

    /// <summary>
    ///     A box half off the top of the page is framed to the rows still on it, so the driver is handed exactly the
    ///     part on the page — rather than the whole box to cut, which it does by encoding the cut again on every frame
    ///     (#292).
    /// </summary>
    [Fact]
    public void ASixelBoxHalfOffTheTopIsFramedToTheRowsStillOnThePage()
    {
        var frame = new AFrame(new FakePictures().Holding("m1", 40, 80));

        frame.Place(Rows(Box("m1", at: 10, rows: 4)), top: 12, ARaster.Sixel());

        Assert.Equal(new Rectangle(0, 0, 4, 2), frame.FramedLast("m1"));
    }

    /// <summary>
    ///     The same at the foot: a box coming up onto the page is drawn as the rows already on it, a row more on each
    ///     step, rather than waiting until it is whole.
    /// </summary>
    [Fact]
    public void ASixelBoxComingUpOntoThePageIsFramedToTheRowsAlreadyOnIt()
    {
        var frame = new AFrame(new FakePictures().Holding("m1", 40, 80));
        var rows = Rows(Box("m1", at: 12, rows: 4));

        frame.Place(rows, top: 4, ARaster.Sixel());

        Assert.Equal(new Rectangle(0, 8, 4, 2), frame.FramedLast("m1"));

        frame.Place(rows, top: 5, ARaster.Sixel());

        Assert.Equal(new Rectangle(0, 7, 4, 3), frame.FramedLast("m1"));
    }

    /// <summary>And a box running past the right of the page is cut at it, never drawn on the edge.</summary>
    [Fact]
    public void ASixelBoxPastTheRightOfThePageIsCutAtIt()
    {
        var frame = new AFrame(new FakePictures().Holding("m1", 40, 80));

        frame.Place(Rows(Box("m1", at: 0, column: Width - 3, columns: 8, rows: 4)), top: 0, ARaster.Sixel());

        Assert.Equal(new Rectangle(Width - 3, 0, 3, 4), frame.FramedLast("m1"));
    }

    /// <summary>
    ///     The frame a picture scrolls off the page lets go of its box before any box is shown anything, so that a
    ///     picture is never put on screen over one the terminal has not been told to drop (#360).
    /// </summary>
    [Theory]
    [MemberData(nameof(BoxWays))]
    public void AFrameLetsGoOfWhatItIsNotKeepingBeforeItShowsAnything(PictureWay way)
    {
        var frame = new AFrame(new FakePictures().Holding("m1", 40, 80).Holding("m2", 40, 80));
        var rows = Rows(Box("m1", at: 2), Box("m2", at: 12));

        frame.Place(rows, top: 0, Of(way));
        frame.Log.Clear();
        frame.Place(rows, top: 8, Of(way));

        var released = frame.Log.FindIndex(call => call is { What: Kind.Released, Picture: "m1" });
        var shown = frame.Log.FindIndex(call => call is { What: Kind.ShownSixel or Kind.ShownWhole, Picture: "m2" });

        Assert.True(released >= 0, "m1's box was never let go of");
        Assert.True(shown >= 0, "m2 was never shown");
        Assert.True(released < shown, "m2 was shown before m1's box was let go of");
    }

    /// <summary>
    ///     A box given the very picture it already holds keeps it: never let go of and shown again, which on screen is
    ///     a picture blinking on every frame.
    /// </summary>
    [Theory]
    [MemberData(nameof(BoxWays))]
    public void ABoxGivenThePictureItHoldsKeepsIt(PictureWay way)
    {
        var frame = new AFrame(new FakePictures().Holding("m1", 40, 80));
        var rows = Rows(Box("m1", at: 2));

        frame.Place(rows, top: 0, Of(way));
        frame.Log.Clear();
        frame.Place(rows, top: 1, Of(way));

        Assert.DoesNotContain(frame.Log, call => call is { What: Kind.Released, Picture: "m1" });
        Assert.Equal(new Rectangle(0, 1, 4, 4), frame.FramedLast("m1"));
    }

    /// <summary>
    ///     Through Kitty a box half off the top keeps the whole box as its frame, and is shown the whole picture: the
    ///     image view sends it once and places a crop of it as it moves, which cutting the box down for it — as sixel
    ///     is — would undo (ADR-0016, ADR-0022).
    /// </summary>
    [Fact]
    public void AKittyBoxIsShownTheWholePictureAndKeepsItsWholeFrameHalfOffThePage()
    {
        var frame = new AFrame(new FakePictures().Holding("m1", 40, 80));

        frame.Place(Rows(Box("m1", at: 10, rows: 4)), top: 12, ARaster.Kitty());

        Assert.Contains(frame.Log, call => call is { What: Kind.ShownWhole, Picture: "m1" });
        Assert.DoesNotContain(frame.Log, call => call.What is Kind.ShownSixel);
        Assert.Equal(new Rectangle(0, -2, 4, 4), frame.FramedLast("m1"));
    }

    /// <summary>
    ///     Where pictures are drawn as placeholders, or not drawn at all, no box is shown anything — and one still
    ///     holding a picture from before is let go of, so that nothing drawn through it is left on screen under the
    ///     placeholders or the link (ADR-0022).
    /// </summary>
    [Theory]
    [MemberData(nameof(NoBoxWays))]
    public void NoBoxIsShownAnythingWherePicturesAreNotDrawnThroughOne(PictureWay way)
    {
        var frame = new AFrame(new FakePictures().Holding("m1", 40, 80));
        var rows = Rows(Box("m1", at: 2));

        frame.Place(rows, top: 0, ARaster.Sixel());
        frame.Log.Clear();
        frame.Place(rows, top: 0, Of(way));

        Assert.Contains(frame.Log, call => call is { What: Kind.Released, Picture: "m1" });
        Assert.DoesNotContain(frame.Log, call => call.What is Kind.ShownSixel or Kind.ShownWhole);
    }

    /// <summary>A page wanting no picture at all lets go of every box holding one.</summary>
    [Theory]
    [MemberData(nameof(BoxWays))]
    public void APageWantingNothingLetsGoOfEveryBox(PictureWay way)
    {
        var frame = new AFrame(new FakePictures().Holding("m1", 40, 80).Holding("m2", 40, 80));

        frame.Place(Rows(Box("m1", at: 0), Box("m2", at: 5)), top: 0, Of(way));
        frame.Log.Clear();
        frame.Place(Rows(), top: 0, Of(way));

        Assert.Equal(
            ["m1", "m2"],
            frame.Log.Where(call => call.What is Kind.Released && call.Picture is not null).Select(call => call.Picture));
        Assert.DoesNotContain(frame.Log, call => call.What is Kind.ShownSixel or Kind.ShownWhole);
    }

    /// <summary>
    ///     A page with no room — the window shrunk to nothing — lets go of every box, so that nothing is left drawn
    ///     from the last size there was over whatever replaces it.
    /// </summary>
    [Fact]
    public void APageWithNoRoomLetsGoOfEveryBox()
    {
        var frame = new AFrame(new FakePictures().Holding("m1", 40, 80));
        var rows = Rows(Box("m1", at: 2));

        frame.Place(rows, top: 0, ARaster.Sixel());
        frame.Log.Clear();
        frame.Place(rows, top: 0, ARaster.Sixel(), height: 0);

        Assert.Contains(frame.Log, call => call is { What: Kind.Released, Picture: "m1" });
        Assert.DoesNotContain(frame.Log, call => call.What is Kind.ShownSixel or Kind.ShownWhole);
    }

    /// <summary>
    ///     The same picture wanted twice on the page — an account's avatar over a run of their posts — gets a box for
    ///     each place, and once one of them is scrolled away exactly one box goes on drawing it and the other is let
    ///     go. Deciding box by box, both said the avatar was still wanted somewhere, and the one neither moved nor
    ///     released was an avatar stuck across the middle of a post (#77).
    /// </summary>
    [Fact]
    public void APictureWantedTwiceHasTwoBoxesAndOnceAgainLetsGoOfOne()
    {
        var frame = new AFrame(new FakePictures().Holding("avatar", 40, 80));
        var rows = Rows(Box("avatar", at: 0), Box("avatar", at: 5));

        frame.Place(rows, top: 0, ARaster.Kitty());

        Assert.Equal([0, 1], frame.ShownIn("avatar"));

        frame.Log.Clear();
        frame.Place(rows, top: 5, ARaster.Kitty());

        var released = Assert.Single(frame.Log, call => call is { What: Kind.Released, Picture: "avatar" });
        Assert.Equal([1 - released.Box], frame.ShownIn("avatar"));
    }

    /// <summary>A box already holding a picture keeps it, whatever order the page now wants them in.</summary>
    [Fact]
    public void APictureStaysInTheBoxAlreadyHoldingIt()
    {
        var frame = new AFrame(new FakePictures().Holding("m1", 40, 80).Holding("m2", 40, 80));

        frame.Place(Rows(Box("m1", at: 0), Box("m2", at: 5)), top: 0, ARaster.Kitty());
        frame.Log.Clear();
        frame.Place(Rows(Box("m2", at: 0), Box("m1", at: 5)), top: 0, ARaster.Kitty());

        Assert.Equal([0], frame.ShownIn("m1"));
        Assert.Equal([1], frame.ShownIn("m2"));
        Assert.DoesNotContain(frame.Log, call => call is { What: Kind.Released, Picture: not null });
    }

    /// <summary>
    ///     A box holding nothing is preferred over one whose picture is being let go of this frame, so that no box goes
    ///     from one picture straight to another within a frame.
    /// </summary>
    [Fact]
    public void AnEmptyBoxIsPreferredOverOneLetGoOfThisFrame()
    {
        var frame = new AFrame(new FakePictures().Holding("m1", 40, 80).Holding("m2", 40, 80));

        frame.Place(Rows(Box("m1", at: 0)), top: 0, ARaster.Kitty());
        frame.Log.Clear();
        frame.Place(Rows(Box("m2", at: 0)), top: 0, ARaster.Kitty());

        Assert.Contains(frame.Log, call => call is { Box: 0, What: Kind.Released, Picture: "m1" });
        Assert.Equal([1], frame.ShownIn("m2"));
    }

    /// <summary>
    ///     With every box full, one being let go of anyway is used again rather than a picture being skipped — a page
    ///     fuller than the pool draws what it can.
    /// </summary>
    [Fact]
    public void ABoxLetGoOfIsUsedAgainWhereNoneIsEmpty()
    {
        var before = Named("p", Placing.MostBoxes);
        var after = Named("q", Placing.MostBoxes);
        var frame = new AFrame(Holding(new FakePictures(), [.. before, .. after]));

        frame.Place(Abreast(before), top: 0, ARaster.Kitty());
        frame.Log.Clear();
        frame.Place(Abreast(after), top: 0, ARaster.Kitty());

        Assert.Equal(Placing.MostBoxes, frame.Log.Count(call => call.What is Kind.ShownWhole));
    }

    /// <summary>
    ///     More pictures than boxes: every box draws one, the one left over is not drawn, and no two share a box.
    /// </summary>
    [Fact]
    public void APictureThereIsNoBoxLeftForIsNotDrawn()
    {
        var many = Named("p", Placing.MostBoxes + 1);
        var frame = new AFrame(Holding(new FakePictures(), many));

        frame.Place(Abreast(many), top: 0, ARaster.Kitty());

        var shown = frame.Log.Where(call => call.What is Kind.ShownWhole).ToList();

        Assert.Equal(Placing.MostBoxes, shown.Select(call => call.Box).Distinct().Count());
        Assert.Equal(Placing.MostBoxes, shown.Select(call => call.Picture).Distinct().Count());
    }

    /// <summary>
    ///     A <b>Stand-in</b>'s blur is drawn through a box of its own, and when the picture lands that box is let go of
    ///     before the picture is shown — so the picture is never put over a blur the terminal has not been told to drop
    ///     (#349).
    /// </summary>
    [Theory]
    [MemberData(nameof(BoxWays))]
    public void ABlursBoxIsLetGoOfBeforeThePictureReplacingItIsShown(PictureWay way)
    {
        var pictures = new FakePictures();
        var frame = new AFrame(pictures);
        var blur = Drawn.Blur(Picture("m1"));
        var rows = Rows(Box("m1", at: 2));

        rows[2] = rows[2] with
        {
            Insets = [new Inset(blur, 0, 4, 4) { Blur = new Picture(new Terminal.Gui.Drawing.Color[4, 4]) }],
        };

        frame.Place(rows, top: 0, Of(way));

        Assert.Equal([0], frame.ShownIn(blur.Id));

        pictures.Holding("m1", 40, 80);
        frame.Log.Clear();
        frame.Place(Rows(Box("m1", at: 2)), top: 0, Of(way));

        var released = frame.Log.FindIndex(call => call is { What: Kind.Released } && call.Picture == blur.Id);
        var shown = frame.Log.FindIndex(call => call is { What: Kind.ShownSixel or Kind.ShownWhole, Picture: "m1" });

        Assert.True(released >= 0, "the blur's box was never let go of");
        Assert.True(released < shown, "the picture was shown before the blur's box was let go of");
    }

    /// <summary>
    ///     A sixel box straddling the foot of a page moving down has the cuts of the next four rows of the scroll
    ///     encoded ahead, off the frame, and the one a row back for a reader who turns round — so a step costs the
    ///     sending and not the encoding (#292, ADR-0023).
    /// </summary>
    [Fact]
    public void TheCutsTheNextRowsOfAScrollWantAreEncodedAhead()
    {
        var ahead = new List<Action>();
        var encoded = new List<int>();
        var frame = new AFrame(
            new FakePictures().Holding("m1", 40, 160),
            work => ahead.Add(work),
            (pixels, _) =>
            {
                encoded.Add(pixels.GetLength(1) / 20);

                return "sixel";
            });
        var rows = Rows(Box("m1", at: 8, rows: 8));

        // Standing still, two rows of the box on the page: encoded now, and a row either way encoded ahead.
        frame.Place(rows, top: 0, ARaster.Sixel());

        Assert.Equal([2], encoded);

        Run(ahead);

        Assert.Equal([1, 2, 3], encoded.Order());

        // Down a row: the three rows now on the page were ready, and four rows on the way down are encoded ahead.
        encoded.Clear();
        frame.Place(rows, top: 1, ARaster.Sixel());

        Assert.Empty(encoded);

        Run(ahead);

        Assert.Equal([4, 5, 6, 7], encoded.Order());

        static void Run(List<Action> work)
        {
            var now = work.ToList();

            work.Clear();
            now.ForEach(action => action());
        }
    }

    public static TheoryData<PictureWay> BoxWays() => [PictureWay.Sixel, PictureWay.Kitty];

    public static TheoryData<PictureWay> NoBoxWays() => [PictureWay.Placeholders, PictureWay.None];

    private static Raster Of(PictureWay way) => way switch
    {
        PictureWay.Sixel => ARaster.Sixel(),
        PictureWay.Kitty => ARaster.Kitty(),
        PictureWay.Placeholders => ARaster.Placeholders(),
        _ => Raster.None,
    };

    /// <summary>A screen's worth of rows, plenty more below it, and boxes reserved where each says.</summary>
    private static Line[] Rows(params (int At, Inset Inset)[] boxes)
    {
        var rows = Enumerable.Range(0, 60).Select(at => Line.Of($"row {at}", Role.Body)).ToArray();

        foreach (var (at, inset) in boxes)
        {
            for (var row = 0; row < inset.Rows; row++)
            {
                rows[at + row] = Line.Of(new string('▒', inset.Columns), Role.Media);
            }

            rows[at] = rows[at] with { Insets = [.. rows[at].Insets, inset] };
        }

        return rows;
    }

    /// <summary>One row of one-cell boxes side by side, one for each of <paramref name="ids" />.</summary>
    private static Line[] Abreast(IReadOnlyList<string> ids)
    {
        var rows = Rows();

        rows[0] = rows[0] with { Insets = [.. ids.Select((id, at) => new Inset(Picture(id), at, 1, 1))] };

        return rows;
    }

    private static List<string> Named(string prefix, int count) =>
        [.. Enumerable.Range(0, count).Select(at => $"{prefix}{at}")];

    private static FakePictures Holding(FakePictures pictures, IEnumerable<string> ids) =>
        ids.Aggregate(pictures, (held, id) => held.Holding(id, 10, 20));

    private static (int At, Inset Inset) Box(string id, int at, int column = 0, int columns = 4, int rows = 4) =>
        (at, new Inset(Picture(id), column, columns, rows));

    private static Drawn Picture(string id) => new(id, $"https://files.mastodon.social/{id}.png");

    /// <summary>
    ///     A Placing over fake boxes writing into one log, its sixels encoded by <paramref name="encode" /> and those
    ///     encoded ahead of a scroll encoded <paramref name="ahead" /> — both on the spot unless a test says otherwise.
    /// </summary>
    private sealed class AFrame
    {
        public AFrame(
            IPictures pictures,
            Action<Action>? ahead = null,
            Func<Terminal.Gui.Drawing.Color[,], int, string>? encode = null)
        {
            Placing = new Placing(
                pictures,
                FakePictureBox.Pool(Log),
                new SixelPictures(encode ?? ((_, _) => "sixel"), ahead ?? (work => work())));
        }

        public List<FakePictureBox.Call> Log { get; } = [];

        public Placing Placing { get; }

        /// <summary>Which boxes were shown <paramref name="id" />, in the order they were.</summary>
        public List<int> ShownIn(string id) =>
        [
            .. Log.Where(call => call.What is Kind.ShownSixel or Kind.ShownWhole && call.Picture == id)
               .Select(call => call.Box),
        ];

        /// <summary>Where a box showing <paramref name="id" /> was last framed.</summary>
        public Rectangle? FramedLast(string id) =>
            Log.LastOrDefault(call => call.What is Kind.Framed && call.Picture == id)?.Frame;

        public void Place(IReadOnlyList<Line> rows, int top, Raster raster, int height = Height) =>
            Placing.Frame(rows, top, Width, height, raster);
    }
}
