using Wooly.Core.Posts;
using Wooly.Tests.Fakes;
using Wooly.Tui.Media;
using Wooly.Tui.Rendering;
using Wooly.Tui.Screens;
using Wooly.Tui.Theme;

namespace Wooly.Tests.Tui;

/// <summary>
///     A <b>drawn</b> picture's box, settled from the shape the instance reported before its pixels arrive, and the
///     <b>Stand-in</b> it shows until they do (#347, ADR-0025). The rows a post takes are the same before its pictures
///     are here, once they are, and if they never come — which is the whole of what stops the timeline jumping.
/// </summary>
/// <remarks>
///     Held against <see cref="PostLines" /> through <see cref="FakePictures" />, the half a test can hold: which rows
///     there are, which of them are shaded, and where a picture is put. Drawing the pixels themselves is the raster
///     paths' own, and their tests (<c>KittyPictureTests</c>, <c>PlaceholdersTests</c>) are unchanged by this.
/// </remarks>
public class ReservedBoxTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 29, 12, 30, 0, TimeSpan.Zero);

    /// <summary>
    ///     800×200 at 61 columns of 10×20-pixel cells: 61 × 10 × 200 ÷ (800 × 20) is 7.6, so eight rows. Small enough
    ///     to sit under the feed's cap, so the rows are the shape's own rather than the cap's.
    /// </summary>
    private static readonly PictureShape Wide = new(800, 200);

    /// <summary>The rows of the Stand-in: shaded and nothing else.</summary>
    private static List<Line> StandIn(IEnumerable<Line> lines) =>
        [.. lines.Where(line => line.Has(Role.StandIn))];

    /// <summary>
    ///     The box is there from the first frame, at the height the shape says, with nothing held at all — and the
    ///     post's rows are the same rows once the picture is held. Not just as many: each row that is not the box says
    ///     what it said, in the same place.
    /// </summary>
    [Fact]
    public void Feed_ReservesThePicturesBoxBeforeItArrivesAndMovesNothingWhenItDoes()
    {
        var post = APost.With(media: [APost.APicture(shape: Wide)]);

        var waiting = PostLines.Feed(post, new Drawing(61, Now, new FakePictures(), ARaster.Sixel()), default);
        var held = PostLines.Feed(
            post,
            new Drawing(61, Now, new FakePictures().Holding("m1", 800, 200), ARaster.Sixel()),
            default);

        Assert.Equal(8, StandIn(waiting).Count);
        Assert.Equal(waiting.Count, held.Count);

        for (var at = 0; at < waiting.Count; at++)
        {
            if (waiting[at].Has(Role.StandIn))
            {
                Assert.True(string.IsNullOrWhiteSpace(held[at].Text), $"Row {at} of the box says \"{held[at].Text}\".");
            }
            else
            {
                Assert.Equal(waiting[at].Text, held[at].Text);
            }
        }

        var inset = Assert.Single(held.SelectMany(line => line.Insets));
        Assert.Equal((61, 8), (inset.Columns, inset.Rows));
    }

    /// <summary>
    ///     A picture that could not be fetched is, to the rows, one that has not arrived: its box stays, shaded, and
    ///     a failure costs no more layout than a wait does (ADR-0025).
    /// </summary>
    [Fact]
    public void Feed_LeavesTheStandInWhereAPictureNeverArrives()
    {
        var pictures = new FakePictures();
        var post = APost.With(media: [APost.APicture(shape: Wide)]);

        var lines = PostLines.Feed(post, new Drawing(61, Now, pictures, ARaster.Sixel()), default);

        Assert.Equal(8, StandIn(lines).Count);
        Assert.Empty(lines.SelectMany(line => line.Insets));
        Assert.Equal("m1", Assert.Single(lines, line => line.Wants is not null).Wants?.Id);
    }

    /// <summary>
    ///     The Stand-in is a plain shade and nothing written in it, as wide as the box: the caption is said once, on
    ///     its own row above the box, where it already was (ADR-0025).
    /// </summary>
    [Fact]
    public void Feed_ShadesTheStandInWithNoTextInIt()
    {
        var post = APost.With(media: [APost.APicture(shape: Wide, description: "A cartoon sheep")]);

        var lines = PostLines.Feed(post, new Drawing(61, Now, new FakePictures(), ARaster.Sixel()), default);

        Assert.All(StandIn(lines), line =>
        {
            Assert.Equal(new string('░', 61), line.Text);
            Assert.Equal(Role.StandIn, line.Role);
        });

        var caption = Assert.Single(lines, line => line.Text.Contains("A cartoon sheep", StringComparison.Ordinal));
        Assert.Equal(lines.ToList().IndexOf(caption) + 1, lines.ToList().FindIndex(line => line.Has(Role.StandIn)));
    }

    /// <summary>
    ///     Where the instance reported no shape the box is 16:9 at full width: 40 × 10 × 9 ÷ (16 × 20) is 11.25, so
    ///     eleven rows of forty. A guess, but a box rather than a jump (ADR-0025).
    /// </summary>
    [Fact]
    public void Feed_ReservesASixteenByNineBoxWhereTheInstanceReportedNoShape()
    {
        var lines = PostLines.Feed(
            APost.With(media: [APost.APicture(shape: null)]),
            new Drawing(40, Now, new FakePictures(), ARaster.Sixel()),
            default);

        var shaded = StandIn(lines);
        Assert.Equal(11, shaded.Count);
        Assert.All(shaded, line => Assert.Equal(40, line.Width));
    }

    /// <summary>
    ///     And under the feed's cap where 16:9 would be taller than it: at 61 columns that is 17 rows, so sixteen, and
    ///     narrowed to match — the narrowing ADR-0016 already gave a picture too tall for the room.
    /// </summary>
    [Fact]
    public void Feed_HoldsTheDefaultBoxUnderTheFeedsCap()
    {
        var lines = PostLines.Feed(
            APost.With(media: [APost.APicture(shape: null)]),
            new Drawing(61, Now, new FakePictures(), ARaster.Sixel()),
            default);

        var shaded = StandIn(lines);
        Assert.Equal(Inset.FeedRows, shaded.Count);
        Assert.All(shaded, line => Assert.Equal(57, line.Width));
    }

    /// <summary>
    ///     Pixels squarer than the shape they were reserved at are fitted to the box's height and centred across it:
    ///     a square in a 61×8 box is 16 wide (8 rows of 20 pixels over cells 10 wide), 22 columns in.
    /// </summary>
    [Fact]
    public void Feed_CentresAPictureNarrowerThanItsBoxAcrossIt()
    {
        var post = APost.With(media: [APost.APicture(shape: Wide)]);

        var waiting = PostLines.Feed(post, new Drawing(61, Now, new FakePictures(), ARaster.Sixel()), default);
        var held = PostLines.Feed(
            post,
            new Drawing(61, Now, new FakePictures().Holding("m1", 400, 400), ARaster.Sixel()),
            default);

        var top = waiting.ToList().FindIndex(line => line.Has(Role.StandIn));
        var carrying = held.ToList().FindIndex(line => line.Insets.Count > 0);
        var inset = held[carrying].Insets.Single();

        Assert.Equal(waiting.Count, held.Count);
        Assert.Equal(top, carrying);
        Assert.Equal((22, 16, 8), (inset.Column, inset.Columns, inset.Rows));
        Assert.Empty(StandIn(held));
    }

    /// <summary>
    ///     Pixels wider than the shape are fitted to the box's width and centred down it: a 4:1 picture in a 32×16
    ///     box (a square, at 61 columns, capped) is four rows tall, six rows down.
    /// </summary>
    [Fact]
    public void Feed_CentresAPictureShorterThanItsBoxDownIt()
    {
        var post = APost.With(media: [APost.APicture(shape: new PictureShape(400, 400))]);

        var waiting = PostLines.Feed(post, new Drawing(61, Now, new FakePictures(), ARaster.Sixel()), default);
        var held = PostLines.Feed(
            post,
            new Drawing(61, Now, new FakePictures().Holding("m1", 800, 200), ARaster.Sixel()),
            default);

        var top = waiting.ToList().FindIndex(line => line.Has(Role.StandIn));
        var carrying = held.ToList().FindIndex(line => line.Insets.Count > 0);
        var inset = held[carrying].Insets.Single();

        Assert.Equal(waiting.Count, held.Count);
        Assert.Equal(top + 6, carrying);
        Assert.Equal((0, 32, 4), (inset.Column, inset.Columns, inset.Rows));
        Assert.Equal(Inset.Width(held[carrying].Insets), held[carrying].Width);
    }

    /// <summary>
    ///     With captions turned off, the caption is gone from the first frame rather than from when the picture lands —
    ///     the box being there either way, a caption taken down on arrival would move the post — and the Stand-in
    ///     carries no text of its own in its place. The box's top row is then what says what it is waiting for.
    /// </summary>
    [Fact]
    public void Feed_HidesTheCaptionFromTheFirstFrameWhenAskedToAndWritesNothingInTheStandIn()
    {
        var post = APost.With(media: [APost.APicture(shape: Wide, description: "A cartoon sheep")]);

        var waiting = PostLines.Feed(
            post,
            new Drawing(61, Now, new FakePictures(), ARaster.Sixel(), HideDrawnCaption: true),
            default);
        var held = PostLines.Feed(
            post,
            new Drawing(61, Now, new FakePictures().Holding("m1", 800, 200), ARaster.Sixel(), HideDrawnCaption: true),
            default);

        foreach (var lines in new[] { waiting, held })
        {
            Assert.DoesNotContain(lines, line => line.Text.Contains("A cartoon sheep", StringComparison.Ordinal));
        }

        Assert.Equal(waiting.Count, held.Count);
        Assert.All(StandIn(waiting), line => Assert.Equal(new string('░', 61), line.Text));

        var wanting = Assert.Single(waiting, line => line.Wants is not null);
        Assert.Equal("m1", wanting.Wants?.Id);
        Assert.True(wanting.Has(Role.StandIn));
    }

    /// <summary>A video's preview is reserved and stood in for the same way, under the label that opens it.</summary>
    [Fact]
    public void Feed_ReservesAVideosPreviewTheSameWay()
    {
        var post = APost.With(media: [APost.Attached(MediaKind.Video, shape: Wide)]);

        var waiting = PostLines.Feed(post, new Drawing(61, Now, new FakePictures(), ARaster.Sixel()), default);
        var held = PostLines.Feed(
            post,
            new Drawing(61, Now, new FakePictures().Holding("m1", 800, 200), ARaster.Sixel()),
            default);

        Assert.Equal(8, StandIn(waiting).Count);
        Assert.Equal(waiting.Count, held.Count);
        Assert.Equal(
            waiting.ToList().FindIndex(line => line.Text.StartsWith("⏵ Video", StringComparison.Ordinal)) + 1,
            waiting.ToList().FindIndex(line => line.Has(Role.StandIn)));
    }

    /// <summary>
    ///     A warned post reserves nothing — no box, no shade — until asked past, since even an empty box the size of a
    ///     photograph says a photograph is there; asked past, the box is there at once at its final size, Stand-in and
    ///     all, so the post re-flows the once (ADR-0025).
    /// </summary>
    [Fact]
    public void Feed_ReservesNothingForAWarnedPostUntilAskedPastAndThenTheWholeBox()
    {
        var post = APost.With(sensitive: true, media: [APost.APicture(shape: Wide)]);
        var pictures = new FakePictures();

        var hidden = PostLines.Feed(post, new Drawing(61, Now, pictures, ARaster.Sixel()), default);

        Assert.Empty(StandIn(hidden));
        Assert.Empty(pictures.Asked);

        var revealed = PostLines.Feed(
            post,
            new Drawing(61, Now, pictures, ARaster.Sixel()),
            new Reading(Revealed: true));

        Assert.Equal(8, StandIn(revealed).Count);
    }

    /// <summary>
    ///     A link preview's picture is reserved the way an attachment's is, from the shape its card gave: the box is
    ///     under the preview's words from the first frame, shaded, and the rows are the same rows once the picture is
    ///     held (#348).
    /// </summary>
    [Fact]
    public void Feed_ReservesALinkPreviewsPictureBeforeItArrivesAndMovesNothingWhenItDoes()
    {
        var link = APost.ALinkPreview(shape: Wide);
        var post = APost.With(linkPreview: link);

        var waiting = PostLines.Feed(post, new Drawing(61, Now, new FakePictures(), ARaster.Sixel()), default);
        var held = PostLines.Feed(
            post,
            new Drawing(61, Now, new FakePictures().HoldingLinkPreview(link, 800, 200), ARaster.Sixel()),
            default);

        Assert.Equal(8, StandIn(waiting).Count);
        Assert.All(StandIn(waiting), line => Assert.Equal(new string('░', 61), line.Text));
        Assert.Equal(waiting.Count, held.Count);

        for (var at = 0; at < waiting.Count; at++)
        {
            if (waiting[at].Has(Role.StandIn))
            {
                Assert.True(string.IsNullOrWhiteSpace(held[at].Text), $"Row {at} of the box says \"{held[at].Text}\".");
            }
            else
            {
                Assert.Equal(waiting[at].Text, held[at].Text);
            }
        }

        var inset = Assert.Single(held.SelectMany(line => line.Insets));
        Assert.Equal((0, 61, 8), (inset.Column, inset.Columns, inset.Rows));
        Assert.Empty(StandIn(held));
    }

    /// <summary>
    ///     Where the card gave no shape the box is the same 16:9 an attachment's is: eleven rows of forty at forty
    ///     columns. Implausible sides never get this far — they are no shape from the wire on (#348).
    /// </summary>
    [Fact]
    public void Feed_ReservesASixteenByNineBoxForALinkPreviewWithNoShape()
    {
        var lines = PostLines.Feed(
            APost.With(linkPreview: APost.ALinkPreview(shape: null)),
            new Drawing(40, Now, new FakePictures(), ARaster.Sixel()),
            default);

        var shaded = StandIn(lines);
        Assert.Equal(11, shaded.Count);
        Assert.All(shaded, line => Assert.Equal(40, line.Width));
    }

    /// <summary>
    ///     A link preview's picture of another shape than its card said is fitted inside the box and centred, as an
    ///     attachment's is: a square in a 61×8 box is 16 wide, 22 columns in, on the box's top row.
    /// </summary>
    [Fact]
    public void Feed_CentresALinkPreviewsPictureInsideItsBox()
    {
        var link = APost.ALinkPreview(shape: Wide);
        var post = APost.With(linkPreview: link);

        var waiting = PostLines.Feed(post, new Drawing(61, Now, new FakePictures(), ARaster.Sixel()), default);
        var held = PostLines.Feed(
            post,
            new Drawing(61, Now, new FakePictures().HoldingLinkPreview(link, 400, 400), ARaster.Sixel()),
            default);

        var top = waiting.ToList().FindIndex(line => line.Has(Role.StandIn));
        var carrying = held.ToList().FindIndex(line => line.Insets.Count > 0);
        var inset = held[carrying].Insets.Single();

        Assert.Equal(waiting.Count, held.Count);
        Assert.Equal(top, carrying);
        Assert.Equal((22, 16, 8), (inset.Column, inset.Columns, inset.Rows));
    }

    /// <summary>
    ///     A link preview behind a warning reserves nothing and sends for nothing until asked past, and then its whole
    ///     box is there at once, shaded (#348, ADR-0025).
    /// </summary>
    [Fact]
    public void Feed_ReservesNothingForAWarnedPostsLinkPreviewUntilAskedPast()
    {
        var post = APost.With(sensitive: true, linkPreview: APost.ALinkPreview(shape: Wide));
        var pictures = new FakePictures();

        var hidden = PostLines.Feed(post, new Drawing(61, Now, pictures, ARaster.Sixel()), default);

        Assert.Empty(StandIn(hidden));
        Assert.DoesNotContain(hidden, line => line.Wants is not null);
        Assert.Empty(pictures.Asked);

        var revealed = PostLines.Feed(
            post,
            new Drawing(61, Now, pictures, ARaster.Sixel()),
            new Reading(Revealed: true));

        Assert.Equal(8, StandIn(revealed).Count);
    }

    /// <summary>
    ///     An avatar keeps the fixed box it already had and shows the shade in it until the face arrives: the byline's
    ///     rows say the same thing in the same columns either way, and only the four cells beside them change.
    /// </summary>
    [Fact]
    public void Feed_ShadesAnAvatarsBoxUntilItArrivesWithoutMovingTheByline()
    {
        var post = APost.With(
            account: "maria@fosstodon.org",
            avatarUrl: "https://files.mastodon.social/avatars/original.png");

        var waiting = PostLines.Feed(post, new Drawing(61, Now, new FakePictures(), ARaster.Sixel()), default);
        var held = PostLines.Feed(
            post,
            new Drawing(61, Now, new FakePictures().HoldingAvatarOf("maria@fosstodon.org"), ARaster.Sixel()),
            default);

        var shaded = StandIn(waiting);
        Assert.Equal(2, shaded.Count);
        Assert.All(shaded, line => Assert.StartsWith("░░░░ ", line.Text, StringComparison.Ordinal));
        Assert.Empty(StandIn(held));
        Assert.Equal(waiting.Count, held.Count);

        Assert.Equal(
            waiting.Select(line => line.Text.Replace(new string('░', 4), new string(' ', 4), StringComparison.Ordinal)),
            held.Select(line => line.Text));
    }
}
