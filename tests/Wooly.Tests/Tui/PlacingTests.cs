using System.Drawing;
using Wooly.Tests.Fakes;
using Wooly.Tui.Media;
using Wooly.Tui.Rendering;
using Wooly.Tui.Theme;
using Kind = Wooly.Tests.Fakes.FakePictureBox.Kind;
using Line = Wooly.Tui.Rendering.Line;
using static Wooly.Tests.Fakes.APage;

namespace Wooly.Tests.Tui;

/// <summary>
///     Drawn pictures put through boxes by <see cref="Placing" />, a frame at a time (#361): handed the rows, where the
///     scroll has got to, the page and the Raster, and read back off the fake boxes it was given — what each was shown,
///     where it sat, and when it was let go. The pixels are a manual smoke test (ADR-0016).
/// </summary>
public class PlacingTests
{
    /// <summary>
    ///     On a sixel terminal a box on the page is shown a cut of its picture, framed over the rows reserved for it,
    ///     and made visible.
    /// </summary>
    [Fact]
    public void ASixelBoxIsShownItsCutFramedOverItsRowsAndMadeVisible()
    {
        var frame = new AFrame(new FakePictures().Holding("m1", 40, 80));
        var rows = Rows(Box("m1", at: 2, column: 3, columns: 4, rows: 4));

        frame.Place(rows, top: 0, ARaster.Sixel());

        var box = frame.Showing("m1");

        Assert.Contains(frame.Log, call => call is { What: Kind.ShownSixel, Picture: "m1" });
        Assert.Equal(new Rectangle(3, 2, 4, 4), box.Frame);
        Assert.True(box.Visible);
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

        Assert.Equal(new Rectangle(0, 0, 4, 2), frame.Showing("m1").Frame);
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

        Assert.Equal(new Rectangle(0, 8, 4, 2), frame.Showing("m1").Frame);

        frame.Place(rows, top: 5, ARaster.Sixel());

        Assert.Equal(new Rectangle(0, 7, 4, 3), frame.Showing("m1").Frame);
    }

    /// <summary>And a box running past the right of the page is cut at it, never drawn on the edge.</summary>
    [Fact]
    public void ASixelBoxPastTheRightOfThePageIsCutAtIt()
    {
        var frame = new AFrame(new FakePictures().Holding("m1", 40, 80));

        frame.Place(Rows(Box("m1", at: 0, column: Width - 3, columns: 8, rows: 4)), top: 0, ARaster.Sixel());

        Assert.Equal(new Rectangle(Width - 3, 0, 3, 4), frame.Showing("m1").Frame);
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
        Assert.Equal(new Rectangle(0, 1, 4, 4), frame.Showing("m1").Frame);
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
        Assert.Equal(new Rectangle(0, -2, 4, 4), frame.Showing("m1").Frame);
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

    /// <summary>
    ///     Rows resumed have not been moved through yet, so their first frame encodes a row either way and nothing
    ///     further ahead — rather than reaching on the way the last rows were going, which is the same answer
    ///     <see cref="PictureWantingTests.ResumedRowsReachTwoScreensEitherSideAgain" /> gives what is wanted.
    /// </summary>
    [Fact]
    public void ResumedRowsEncodeOnlyARowEitherWayOnTheirFirstFrame()
    {
        var encoded = new List<int>();
        var frame = new AFrame(
            new FakePictures().Holding("m1", 40, 160),
            encode: (pixels, _) =>
            {
                encoded.Add(pixels.GetLength(1) / 20);

                return "sixel";
            });
        var rows = Rows(Box("m1", at: 8, rows: 8));

        frame.Place(rows, top: 0, ARaster.Sixel());
        frame.Place(rows, top: 1, ARaster.Sixel());
        frame.Placing.Resume();
        encoded.Clear();

        // Six rows of the box on the page, and the seven and five a row either way: all encoded on the way here.
        frame.Place(rows, top: 4, ARaster.Sixel());

        Assert.Empty(encoded);
    }

    /// <summary>
    ///     On a Kitty terminal drawing placeholders, a picture on the page is sent once and handed back as a placement:
    ///     its box, the row of the page it starts on, and the id the terminal holds it under (ADR-0022).
    /// </summary>
    [Fact]
    public void APictureOnThePageIsSentAndHandedBackAsAPlacement()
    {
        var frame = new AFrame(new FakePictures().Holding("m1", 40, 80));
        var rows = Rows(Box("m1", at: 2, column: 3));

        var placed = frame.Place(rows, top: 0, ARaster.Placeholders());

        var sent = Assert.Single(frame.Kitty.Transmitted);
        var (inset, top, id) = Assert.Single(placed);

        Assert.Equal("m1", inset.Drawn.Id);
        Assert.Equal(3, inset.Column);
        Assert.Equal(2, top);
        Assert.Equal(sent.Id, id);
        Assert.Equal((4, 4), (sent.Columns, sent.Rows));
    }

    /// <summary>
    ///     The point of placeholders: a scroll moves a placement a row with the text, under the same id, and sends the
    ///     terminal no image data to do it — including a box half off the top, whose lower rows are still placed so the
    ///     terminal crops the picture rather than it blinking out (ADR-0022).
    /// </summary>
    [Fact]
    public void AScrollMovesThePlacementWithTheRowsAndTransmitsNothing()
    {
        var frame = new AFrame(new FakePictures().Holding("m1", 40, 80));
        var rows = Rows(Box("m1", at: 2));

        var before = Assert.Single(frame.Place(rows, top: 0, ARaster.Placeholders()));
        var after = Assert.Single(frame.Place(rows, top: 1, ARaster.Placeholders()));
        var halfOff = Assert.Single(frame.Place(rows, top: 4, ARaster.Placeholders()));

        Assert.Equal(before with { Top = 1 }, after);
        Assert.Equal(before with { Top = -2 }, halfOff);
        Assert.Single(frame.Kitty.Transmitted);
    }

    /// <summary>
    ///     A picture within a screen below the page is encoded before it is scrolled to, and only sent once it is on
    ///     the page — so that it comes into view with the text around it rather than a frame or two behind (#292).
    /// </summary>
    [Fact]
    public void APictureJustBelowThePageIsEncodedAheadAndSentOnlyOnceItIsOnThePage()
    {
        var encoding = new List<Action>();
        var frame = new AFrame(new FakePictures().Holding("m1", 40, 80), encoding: encoding.Add);
        var rows = Rows(Box("m1", at: Height + 5));

        Assert.Empty(frame.Place(rows, top: 0, ARaster.Placeholders()));

        // Encoded already, though nothing is sent while the box is off the page.
        Assert.Single(encoding)();
        Assert.Empty(frame.Place(rows, top: 0, ARaster.Placeholders()));
        Assert.Empty(frame.Kitty.Transmitted);

        // Sent on the very frame its first row comes onto the page, with nothing more to encode.
        Assert.Equal(Height - 1, Assert.Single(frame.Place(rows, top: 6, ARaster.Placeholders())).Top);
        Assert.Single(frame.Kitty.Transmitted);
        Assert.Single(encoding);
    }

    /// <summary>
    ///     A picture more than a screen from the page is neither encoded nor sent: it is the cache's to fetch, and
    ///     nobody's yet to encode.
    /// </summary>
    [Fact]
    public void APictureMoreThanAScreenFromThePageIsNeitherEncodedNorSent()
    {
        var encoding = new List<Action>();
        var frame = new AFrame(new FakePictures().Holding("m1", 40, 80), encoding: encoding.Add);

        Assert.Empty(frame.Place(Rows(Box("m1", at: Height * 2)), top: 0, ARaster.Placeholders()));
        Assert.Empty(encoding);
        Assert.Empty(frame.Kitty.Transmitted);
    }

    /// <summary>
    ///     A picture the cache lets go of is forgotten by the terminal on the next frame, every size of it, so that the
    ///     terminal does not hold it for the rest of the run (ADR-0022) — told from the frame, on the UI thread, rather
    ///     than from wherever the cache let go of it.
    /// </summary>
    [Fact]
    public void APictureTheCacheLetGoOfIsForgottenOnTheNextFrame()
    {
        var pictures = new FakePictures().Holding("m1", 40, 80);
        var frame = new AFrame(pictures);
        var rows = Rows(Box("m1", at: 2), Box("m1", at: 8, columns: 6));

        frame.Place(rows, top: 0, ARaster.Placeholders());

        var sent = frame.Kitty.Transmitted.Select(sent => sent.Id).ToList();

        Assert.Equal(2, sent.Count);
        Assert.Empty(frame.Kitty.Forgotten);

        pictures.LettingGo("m1");
        frame.Place(Rows(), top: 0, ARaster.Placeholders());

        Assert.Equal(sent.Order(), frame.Kitty.Forgotten.Order());
    }

    /// <summary>
    ///     Where no placeholders are drawn, what the cache let go of is still drained each frame — there is nothing on
    ///     the terminal to forget, so it is simply discarded rather than left to pile up.
    /// </summary>
    [Theory]
    [MemberData(nameof(BoxWays))]
    public void WhatTheCacheLetGoOfIsDrainedWhereNoPlaceholdersAreDrawn(PictureWay way)
    {
        var pictures = new FakePictures().Holding("m1", 40, 80);
        var frame = new AFrame(pictures);

        frame.Place(Rows(Box("m1", at: 2)), top: 0, Of(way));
        pictures.LettingGo("m1");
        frame.Place(Rows(Box("m1", at: 2)), top: 0, Of(way));

        Assert.Empty(pictures.Drain());
        Assert.Empty(frame.Kitty.Forgotten);
    }

    /// <summary>
    ///     On a Kitty terminal drawing placeholders, a <b>Stand-in</b>'s blur is sent and placed; when the picture
    ///     lands it is sent in the blur's place, and the terminal is told to forget the blur in that same frame (#349).
    /// </summary>
    [Fact]
    public void ABlurIsSentAndThenForgottenInTheFrameThePictureReplacingItIsSent()
    {
        var pictures = new FakePictures();
        var frame = new AFrame(pictures);

        var placed = frame.Place(Blurred("m1", at: 2), top: 0, ARaster.Placeholders());

        var blur = Assert.Single(frame.Kitty.Transmitted);
        Assert.Equal(blur.Id, Assert.Single(placed).Id);

        pictures.Holding("m1", 40, 80);
        placed = frame.Place(Rows(Box("m1", at: 2)), top: 0, ARaster.Placeholders());

        Assert.Equal(2, frame.Kitty.Transmitted.Count);
        Assert.Equal([blur.Id], frame.Kitty.Forgotten);
        Assert.Equal(frame.Kitty.Transmitted[1].Id, Assert.Single(placed).Id);
    }

    /// <summary>
    ///     A blur is held while it is within a screen of the page, and forgotten once it is scrolled further than that:
    ///     a blur nobody deletes is a Kitty image the terminal holds for the rest of the run (#349).
    /// </summary>
    [Fact]
    public void ABlurScrolledMoreThanAScreenFromThePageIsForgotten()
    {
        var frame = new AFrame(new FakePictures());
        var rows = Blurred("m1", at: 2);

        frame.Place(rows, top: 0, ARaster.Placeholders());

        var blur = Assert.Single(frame.Kitty.Transmitted);

        // Its foot two rows above the page, and then a whole screen above it.
        frame.Place(rows, top: 8, ARaster.Placeholders());

        Assert.Empty(frame.Kitty.Forgotten);

        frame.Place(rows, top: 6 + Height, ARaster.Placeholders());

        Assert.Equal([blur.Id], frame.Kitty.Forgotten);
    }

    /// <summary>
    ///     A frame with no room — the window shrunk to nothing — lets go of every box, forgets every blur the terminal
    ///     was holding and places nothing: the page has nothing near it any more, so a blur kept would be held for as
    ///     long as the window stays that small, and for the rest of the run if it never grows back.
    /// </summary>
    [Fact]
    public void AFrameWithNoRoomForgetsEveryBlurAndPlacesNothing()
    {
        var frame = new AFrame(new FakePictures());
        var rows = Blurred("m1", at: 2);

        frame.Place(rows, top: 0, ARaster.Placeholders());

        var blur = Assert.Single(frame.Kitty.Transmitted);

        Assert.Empty(frame.Place(rows, top: 0, ARaster.Placeholders(), height: 0));
        Assert.Equal([blur.Id], frame.Kitty.Forgotten);
        Assert.Single(frame.Kitty.Transmitted);
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

    /// <summary>
    ///     Rows with a box at <paramref name="at" /> drawing the blur that stands in for <paramref name="id" />.
    /// </summary>
    private static Line[] Blurred(string id, int at)
    {
        var rows = Rows(Box(id, at: at));

        rows[at] = rows[at] with
        {
            Insets =
            [
                new Inset(Drawn.Blur(Picture(id)), 0, 4, 4)
                {
                    Blur = new Picture(new Terminal.Gui.Drawing.Color[4, 4]),
                },
            ],
        };

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

    /// <summary>
    ///     A Placing over fake boxes writing into one log, its sixels encoded by <paramref name="encode" /> and those
    ///     encoded ahead of a scroll encoded <paramref name="ahead" />, and a Kitty terminal's placeholders encoded
    ///     <paramref name="encoding" /> — all on the spot unless a test says otherwise.
    /// </summary>
    private sealed class AFrame
    {
        public AFrame(
            IPictures pictures,
            Action<Action>? ahead = null,
            Func<Terminal.Gui.Drawing.Color[,], int, string>? encode = null,
            Action<Action>? encoding = null)
        {
            Placing = new Placing(
                pictures,
                FakePictureBox.Pool(Log, Boxes),
                new SixelPictures(encode ?? ((_, _) => "sixel"), ahead ?? (work => work())),
                new Placeholders(Kitty, encoded: () => { }, elsewhere: encoding ?? (work => work())));
        }

        /// <summary>What every box was shown and let go of, in the order it happened.</summary>
        public List<FakePictureBox.Call> Log { get; } = [];

        /// <summary>Every box, in the order Placing made them.</summary>
        public List<FakePictureBox> Boxes { get; } = [];

        /// <summary>What a Kitty terminal drawing placeholders was sent and told to forget.</summary>
        public FakeTerminalImages Kitty { get; } = new();

        public Placing Placing { get; }

        /// <summary>Which boxes were shown <paramref name="id" />, in the order they were.</summary>
        public List<int> ShownIn(string id) =>
        [
            .. Log.Where(call => call.What is Kind.ShownSixel or Kind.ShownWhole && call.Picture == id)
               .Select(call => call.Box),
        ];

        /// <summary>The one box holding <paramref name="id" /> now.</summary>
        public FakePictureBox Showing(string id) => Assert.Single(Boxes, box => box.PictureId == id);

        public IReadOnlyList<Placement> Place(
            IReadOnlyList<Line> rows,
            int top,
            Raster raster,
            int height = Height) =>
            Placing.Frame(rows, top, Width, height, raster);
    }
}
