using Wooly.Core.Posts;
using Wooly.Tests.Fakes;
using Wooly.Tui.Media;
using Wooly.Tui.Rendering;
using Wooly.Tui.Screens;

namespace Wooly.Tests.Tui;

/// <summary>
///     A <c>Video</c>'s or an <c>Animation</c>'s own preview, drawn in the box beside the reference that opens it
///     (#110, ADR-0017). The decision rather than the pixels, the same half <see cref="MediaLineTests" /> holds for a
///     picture: <em>which attachments get a box</em>, <em>what is sent for to fill it</em>, and <em>what the label and
///     the description do once it lands</em>.
/// </summary>
public class AttachmentPreviewTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 29, 12, 30, 0, TimeSpan.Zero);

    /// <summary>
    ///     The second acceptance criterion: a video and an animation the instance offered a preview of get a box, off
    ///     the same <c>Drawn</c>/<c>Inset</c>/<c>IPictures</c> pipeline a picture already goes through — sized the same
    ///     way, from the preview's own proportions.
    /// </summary>
    [Theory]
    [InlineData(MediaKind.Video)]
    [InlineData(MediaKind.Animation)]
    public void Feed_DrawsAVideosOwnPreviewInABoxOfItsOwn(MediaKind kind)
    {
        var lines = PostLines.Feed(
            APost.With(media: [APost.Attached(kind)]),
            61,
            default,
            Now,
            FakePictures.With(new CellSize(10, 20)).Holding("m1", 400, 200));

        var inset = Assert.Single(lines.SelectMany(line => line.Insets));

        Assert.Equal("m1", inset.Drawn.Id);
        Assert.Equal(0, inset.Column);

        // The same arithmetic a picture of these proportions gets: 61 columns of 10 pixels, 2:1, is 15 rows of 20.
        Assert.Equal(61, inset.Columns);
        Assert.Equal(15, inset.Rows);
    }

    /// <summary>
    ///     What is sent for is the instance's own preview, never the video itself: a client that fetched the file would
    ///     be downloading a whole video to fail to decode it, on somebody's data.
    /// </summary>
    [Fact]
    public void Feed_SendsForThePreviewRatherThanForTheVideoItself()
    {
        var lines = PostLines.Feed(
            APost.With(media: [APost.Attached(MediaKind.Video)]),
            61,
            default,
            Now,
            FakePictures.With());

        var wanted = Assert.Single(lines, line => line.Wants is not null).Wants;

        Assert.Equal("https://files.mastodon.social/m1/small.png", wanted?.Address);
    }

    /// <summary>
    ///     A video an instance offered no preview of is not drawn and is not sent for. There is nothing to put in a
    ///     box, and its own address is motion rather than a picture.
    /// </summary>
    [Theory]
    [InlineData(MediaKind.Video)]
    [InlineData(MediaKind.Animation)]
    public void Feed_DrawsNothingAndSendsForNothingWhereTheInstanceOfferedNoPreview(MediaKind kind)
    {
        var lines = PostLines.Feed(
            APost.With(media: [APost.Attached(kind) with { Preview = null }]),
            61,
            default,
            Now,
            FakePictures.With());

        Assert.Empty(lines.SelectMany(line => line.Insets));
        Assert.DoesNotContain(lines, line => line.Wants is not null);
        Assert.Contains(lines, line => line.Text.Contains("⏵ ", StringComparison.Ordinal));
    }

    /// <summary>
    ///     The sixth acceptance criterion: a sound and an attachment of a kind this client has no word for never draw a
    ///     box, even on the instance that sends cover art for one. Cover art is not a frame standing in for motion, and
    ///     drawing it says nothing the label and description do not (ADR-0017).
    /// </summary>
    [Theory]
    [InlineData(MediaKind.Audio, "Audio")]
    [InlineData(MediaKind.Unknown, "Unknown")]
    public void Feed_NeverDrawsABoxForASoundOrAnUnknownKindEvenWithAPreviewToDrawIt(MediaKind kind, string label)
    {
        var lines = PostLines.Feed(
            APost.With(media: [APost.Attached(kind, description: "Sheep, at length")]),
            61,
            default,
            Now,
            FakePictures.With().Holding("m1", 400, 300));

        Assert.Empty(lines.SelectMany(line => line.Insets));
        Assert.DoesNotContain(lines, line => line.Wants is not null);
        Assert.Contains(lines, line => line.Text.Contains($"⏵ {label} Sheep, at length", StringComparison.Ordinal));
    }

    /// <summary>
    ///     The third and ninth acceptance criteria: a terminal offering neither sixel nor the Kitty graphics protocol
    ///     falls back to the label and the description for every kind — the same floor ADR-0016 put a photograph on.
    /// </summary>
    [Theory]
    [InlineData(MediaKind.Video, "Video")]
    [InlineData(MediaKind.Animation, "Animation")]
    [InlineData(MediaKind.Audio, "Audio")]
    [InlineData(MediaKind.Unknown, "Unknown")]
    public void Feed_FallsBackToTheLabelAndDescriptionOnATerminalThatDrawsNothing(MediaKind kind, string label)
    {
        var lines = PostLines.Feed(
            APost.With(media: [APost.Attached(kind, description: "Sheep, at length")]),
            61,
            default,
            Now,
            FakePictures.DrawingNothing());

        Assert.Empty(lines.SelectMany(line => line.Insets));
        Assert.DoesNotContain(lines, line => line.Wants is not null);
        Assert.Contains(lines, line => line.Text.Contains($"⏵ {label} Sheep, at length", StringComparison.Ordinal));
    }

    /// <summary>
    ///     The fourth acceptance criterion: the label is in the same place before and after the preview lands, and it
    ///     is still there afterwards. The same stability ADR-0016 gave a picture's caption, for the same reason — a
    ///     reader's eye must not be moved by pixels arriving under what they are reading.
    /// </summary>
    [Fact]
    public void Feed_KeepsTheLabelWhereItWasOnceThePreviewLands()
    {
        var post = APost.With(media: [APost.Attached(MediaKind.Video, description: "Sheep, at length")]);

        var waiting = PostLines.Feed(post, 61, default, Now, FakePictures.With());
        var landed = PostLines.Feed(post, 61, default, Now, FakePictures.With().Holding("m1", 400, 300));

        Assert.NotEmpty(landed.SelectMany(line => line.Insets));

        Assert.Equal(At(waiting, "⏵ Video"), At(landed, "⏵ Video"));
    }

    /// <summary>
    ///     The fifth acceptance criterion: the description under the label goes once the preview has actually landed,
    ///     the same way and behind the same preference a picture's caption already does (#71). The label itself stays —
    ///     it is not describing the attachment anymore, it is what <c>⏎</c> acts on.
    /// </summary>
    [Fact]
    public void Feed_HidesTheDescriptionOnceThePreviewIsActuallyDrawnWhenAskedTo()
    {
        var lines = PostLines.Feed(
            APost.With(media: [APost.Attached(MediaKind.Video, description: "Sheep, at length")]),
            61,
            default,
            Now,
            FakePictures.With().Holding("m1", 400, 300),
            hideDrawnCaption: true);

        Assert.NotEmpty(lines.SelectMany(line => line.Insets));
        Assert.DoesNotContain(lines, line => line.Text.Contains("Sheep, at length", StringComparison.Ordinal));
        Assert.Contains(lines, line => line.Text.Contains("⏵ Video", StringComparison.Ordinal));
    }

    /// <summary>
    ///     The other half of the fifth: the description stands while the preview is still on its way, and where none is
    ///     ever coming. Hiding it before there is a box would be an arrival flicker rather than a quieter post.
    /// </summary>
    [Fact]
    public void Feed_KeepsTheDescriptionWhileThePreviewIsStillComingOrIsNeverComing()
    {
        var described = APost.With(media: [APost.Attached(MediaKind.Video, description: "Sheep, at length")]);

        var waiting = PostLines.Feed(described, 61, default, Now, FakePictures.With(), hideDrawnCaption: true);
        var never = PostLines.Feed(described, 61, default, Now, FakePictures.DrawingNothing(), hideDrawnCaption: true);

        Assert.Empty(waiting.SelectMany(line => line.Insets));

        foreach (var lines in new[] { waiting, never })
        {
            Assert.Contains(
                lines,
                line => line.Text.Contains("⏵ Video Sheep, at length", StringComparison.Ordinal));
        }
    }

    /// <summary>The default, as it is for a picture: the description stays put under a preview that has landed.</summary>
    [Fact]
    public void Feed_KeepsTheDescriptionOnceDrawnByDefault()
    {
        var lines = PostLines.Feed(
            APost.With(media: [APost.Attached(MediaKind.Video, description: "Sheep, at length")]),
            61,
            default,
            Now,
            FakePictures.With().Holding("m1", 400, 300));

        Assert.NotEmpty(lines.SelectMany(line => line.Insets));
        Assert.Contains(lines, line => line.Text.Contains("⏵ Video Sheep, at length", StringComparison.Ordinal));
    }

    /// <summary>
    ///     The box goes under the label rather than over it, so nothing above it moves when the pixels land — and the
    ///     eighth acceptance criterion, as far as a test can hold it: a preview is exactly one still box, drawn once.
    /// </summary>
    [Fact]
    public void Feed_PutsTheOneBoxUnderTheLabelThatOpensIt()
    {
        var lines = PostLines.Feed(
            APost.With(media: [APost.Attached(MediaKind.Video)]),
            61,
            default,
            Now,
            FakePictures.With().Holding("m1", 400, 300));

        var box = lines.ToList().FindIndex(line => line.Insets.Count > 0);

        Assert.Equal(1, lines.Count(line => line.Insets.Count > 0));
        Assert.True(box > At(lines, "⏵ Video"), "The preview was not drawn under the label that opens it.");
    }

    /// <summary>
    ///     A post screen gives a preview the same room it gives a picture, and a picked reference is still bracketed
    ///     with a box under it — the walk and the drawing are two things about one attachment, not two attachments.
    /// </summary>
    [Fact]
    public void Whole_BracketsThePickedLabelAndStillDrawsThePreviewUnderIt()
    {
        var post = APost.With(media: [APost.Attached(MediaKind.Video, description: "Sheep, at length")]);
        var pictures = FakePictures.With(new CellSize(10, 20)).Holding("m1", 400, 400);

        var reference = AttachmentReferences.Of(post).Single();

        var lines = PostLines.Whole(post, 61, new Reading(Reference: reference), Now, pictures);

        var inset = Assert.Single(lines.SelectMany(line => line.Insets));

        // The post screen's own cap, which is roomier than a feed item's — a square preview at 61 columns of 10 pixels
        // is 31 rows of 20, under the 32 the post screen allows and over the 16 a feed does.
        Assert.Equal(31, inset.Rows);

        Assert.Contains(lines, line => line.Text.Contains("⏵ ‹Video› Sheep, at length", StringComparison.Ordinal));
    }

    /// <summary>
    ///     A post carrying one of everything draws the three there are pixels for and labels all four, in the order
    ///     their author attached them.
    /// </summary>
    [Fact]
    public void Feed_DrawsThePictureAndTheMotionAndLabelsTheRest()
    {
        var pictures = FakePictures.With();
        var media = new List<PostMedia>();

        var kinds = new[] { MediaKind.Image, MediaKind.Video, MediaKind.Animation, MediaKind.Audio };

        for (var at = 0; at < kinds.Length; at++)
        {
            media.Add(APost.Attached(kinds[at], id: $"m{at}"));
            pictures.Holding($"m{at}", 400, 300);
        }

        var lines = PostLines.Feed(APost.With(media: media), 61, default, Now, pictures);

        Assert.Equal(["m0", "m1", "m2"], lines.SelectMany(line => line.Insets).Select(inset => inset.Drawn.Id));

        Assert.Contains(lines, line => line.Text.Contains("⏵ Video", StringComparison.Ordinal));
        Assert.Contains(lines, line => line.Text.Contains("⏵ Animation", StringComparison.Ordinal));
        Assert.Contains(lines, line => line.Text.Contains("⏵ Audio", StringComparison.Ordinal));
    }

    /// <summary>Where the row saying <paramref name="said" /> is, which is what "in the same position" means.</summary>
    private static int At(IReadOnlyList<Line> lines, string said)
    {
        var at = lines.ToList().FindIndex(line => line.Text.Contains(said, StringComparison.Ordinal));

        Assert.True(at >= 0, $"No row said '{said}'.");

        return at;
    }
}
