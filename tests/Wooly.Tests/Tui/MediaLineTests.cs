using Wooly.Core.Posts;
using Wooly.Core.Timelines;
using Wooly.Tests.Fakes;
using Wooly.Tui.Media;
using Wooly.Tui.Rendering;
using Wooly.Tui.Screens;
using Wooly.Tui.Shell;
using Wooly.Tui.Theme;

namespace Wooly.Tests.Tui;

/// <summary>
///     What a post's attachments become in the TUI: which of them get a picture drawn in place, how big its box is,
///     and what the rest get instead. The decision, not the drawing — ADR-0005 and ADR-0014 leave pixels to a manual
///     smoke test, and this is the half a test can hold: <em>a video is linked rather than drawn</em>, <em>a terminal
///     that draws nothing links everything</em>, <em>a picture keeps its own proportions</em>.
/// </summary>
public class MediaLineTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 29, 12, 30, 0, TimeSpan.Zero);

    /// <summary>The first acceptance criterion, as far as a test without a terminal can see it.</summary>
    [Fact]
    public void Feed_GivesAPictureABoxToBeDrawnIn()
    {
        var lines = PostLines.Feed(
            APost.With(media: [APost.APicture()]),
            61,
            revealed: false,
            Now,
            FakePictures.With().Holding("m1", 400, 300));

        var inset = Assert.Single(lines.SelectMany(line => line.Insets));

        Assert.Equal("m1", inset.Media.Id);
        Assert.Equal(0, inset.Column);
        Assert.True(inset.Columns > 0);
        Assert.True(inset.Rows > 0);
    }

    /// <summary>
    ///     The box takes the full width it is allowed, which is the whole difference between an inline picture and a
    ///     thumbnail — and the rows follow from the picture's own proportions and the size of a cell, so a wide
    ///     photograph is drawn wide and a tall one tall.
    /// </summary>
    [Theory]

    // 610 pixels across at 10 per cell; a 2:1 picture is 305 down, which is 15 rows of 20 pixels.
    [InlineData(400, 200, 61, 15)]

    // The same width, a square picture: 31 rows, which is past the cap — so the cap fixes the height and the width
    // comes back from it, 16 rows of 20 pixels being 320 square and so 32 columns of 10.
    [InlineData(400, 400, 32, Inset.FeedRows)]
    public void Feed_DrawsAPictureAtFullWidthInItsOwnProportions(
        int pictureWidth,
        int pictureHeight,
        int expectedColumns,
        int expectedRows)
    {
        var lines = PostLines.Feed(
            APost.With(media: [APost.APicture()]),
            61,
            revealed: false,
            Now,
            FakePictures.With(new CellSize(10, 20)).Holding("m1", pictureWidth, pictureHeight));

        var inset = Assert.Single(lines.SelectMany(line => line.Insets));

        Assert.Equal(expectedRows, inset.Rows);
        Assert.Equal(expectedColumns, inset.Columns);
    }

    /// <summary>
    ///     A picture too tall for the room is brought back to the cap and narrowed to match, rather than taking a
    ///     screen and a half of a feed — and it is still the shape it was.
    /// </summary>
    [Fact]
    public void Feed_ShrinksAPictureTooTallForTheRoomRatherThanCroppingOrStretchingIt()
    {
        var lines = PostLines.Feed(
            APost.With(media: [APost.APicture()]),
            120,
            revealed: false,
            Now,
            FakePictures.With(new CellSize(10, 20)).Holding("m1", 300, 900));

        var inset = Assert.Single(lines.SelectMany(line => line.Insets));

        Assert.Equal(Inset.FeedRows, inset.Rows);

        // A picture one third as wide as it is tall, in cells twice as tall as they are wide: the box should be about
        // two thirds as many columns as rows.
        Assert.InRange(inset.Columns, (Inset.FeedRows * 2 / 3) - 1, (Inset.FeedRows * 2 / 3) + 1);
    }

    /// <summary>
    ///     A post looked at on its own may give a picture more room than the same post has on a feed. The reader
    ///     drilled into it, which is them saying which post they care about.
    /// </summary>
    [Fact]
    public void Whole_GivesATallPictureMoreRoomThanAFeedItemDoes()
    {
        var pictures = FakePictures.With(new CellSize(10, 20)).Holding("m1", 400, 400);
        var post = APost.With(media: [APost.APicture()]);

        var inFeed = Assert.Single(PostLines.Feed(post, 61, false, Now, pictures).SelectMany(line => line.Insets));
        var inWhole = Assert.Single(PostLines.Whole(post, 61, false, Now, pictures).SelectMany(line => line.Insets));

        // The feed's cap is what the picture hits; the post screen's is roomy enough that it does not.
        Assert.Equal(Inset.FeedRows, inFeed.Rows);
        Assert.True(inWhole.Rows > inFeed.Rows);
        Assert.True(inWhole.Rows <= Inset.WholeRows);
    }

    /// <summary>
    ///     The user's own verdict on the cell-based rung, made a rule: a terminal offering neither sixel nor the Kitty
    ///     graphics protocol draws nothing at all, and every attachment on it reads the way the CLI writes one — a link
    ///     and what it shows (ADR-0016).
    /// </summary>
    [Fact]
    public void Feed_LinksEvenAPictureOnATerminalThatDrawsNothing()
    {
        var lines = PostLines.Feed(
            APost.With(media: [APost.APicture(description: "A cartoon sheep")]),
            61,
            revealed: false,
            Now,
            FakePictures.DrawingNothing());

        Assert.Empty(lines.SelectMany(line => line.Insets));
        Assert.Contains(lines, line => line.Text.Contains("⏵ A cartoon sheep", StringComparison.Ordinal));
        Assert.Contains(
            lines,
            line => line.Text.Contains("https://files.mastodon.social/m1/original.png", StringComparison.Ordinal));
    }

    /// <summary>
    ///     A terminal that draws nothing fetches nothing. Nobody asked for the pixels, because the rows were settled
    ///     without them — so a reader on Terminal.app pays no data for previews that could never have been shown.
    /// </summary>
    [Fact]
    public void Feed_SendsForNoPixelsOnATerminalThatDrawsNothing()
    {
        var pictures = FakePictures.DrawingNothing();

        PostLines.Feed(APost.With(media: [APost.APicture()]), 61, revealed: false, Now, pictures);

        Assert.Empty(pictures.Asked);
        Assert.Empty(pictures.Sent);
    }

    /// <summary>
    ///     Working out a post's rows never sends for anything, however many posts there are. What a post is made of
    ///     does not depend on where the reader has scrolled to, so an account of nothing but photographs works out rows
    ///     for every post it holds — and a lookup that also fetched would fetch the whole gallery to draw the handful
    ///     that fit, which is how this came to run a machine out of memory (ADR-0016).
    /// </summary>
    [Fact]
    public void Feed_SendsForNothingHoweverManyPicturesAreOnTheScreensPosts()
    {
        var pictures = FakePictures.With();

        var posts = Enumerable.Range(0, 40)
                              .Select(at => APost.With(id: $"{at}", media: [APost.APicture(id: $"m{at}")]))
                              .ToList();

        var home = new Destination(DestinationKind.Home, "Home", Timeline.Home);

        new FeedScreen(home, posts).Lines(61, Now, pictures);

        Assert.Empty(pictures.Sent);
    }

    /// <summary>
    ///     A row waiting for a picture says which attachment it is waiting for, which is what lets the view — the one
    ///     thing that knows where the scroll has got to — send for the few that are near the screen.
    /// </summary>
    [Fact]
    public void Feed_SaysWhichAttachmentARowIsWaitingFor()
    {
        var waiting = PostLines.Feed(
            APost.With(media: [APost.APicture()]),
            61,
            revealed: false,
            Now,
            FakePictures.With());

        Assert.Equal("m1", Assert.Single(waiting, line => line.Wants is not null).Wants?.Id);

        // Nothing to wait for where nothing could be drawn: the attachment is linked instead.
        var linked = PostLines.Feed(
            APost.With(media: [APost.APicture()]),
            61,
            revealed: false,
            Now,
            FakePictures.DrawingNothing());

        Assert.DoesNotContain(linked, line => line.Wants is not null);
    }

    /// <summary>A gutter put in front of a row does not lose what that row was waiting for.</summary>
    [Fact]
    public void Whole_KeepsWhatARowIsWaitingForBehindAGutter()
    {
        var post = APost.With(media: [APost.APicture()]);

        var lines = new PostScreen(post, []).Lines(61, Now, FakePictures.With());

        Assert.Equal("m1", Assert.Single(lines, line => line.Wants is not null).Wants?.Id);
    }

    /// <summary>
    ///     A screen laid out with no pictures at all — every test, and the shell before a terminal has answered —
    ///     links everything rather than reserving rows for a box that may never come.
    /// </summary>
    [Fact]
    public void Feed_LinksEverythingWhenThereIsNothingToDrawWith()
    {
        var lines = PostLines.Feed(APost.With(media: [APost.APicture()]), 61, revealed: false, Now);

        Assert.Empty(lines.SelectMany(line => line.Insets));
        Assert.Contains(lines, line => line.Text.StartsWith("⏵ ", StringComparison.Ordinal));
    }

    /// <summary>
    ///     A picture still on its way gets its description and no box: the rows appear underneath it when the pixels
    ///     land, rather than a hole opening above the text a reader is part-way through.
    /// </summary>
    [Fact]
    public void Feed_SaysWhatIsComingWhileAPicturesPixelsAreStillOnTheirWay()
    {
        var lines = PostLines.Feed(
            APost.With(media: [APost.APicture(description: "A cartoon sheep")]),
            61,
            revealed: false,
            Now,
            FakePictures.With());

        Assert.Empty(lines.SelectMany(line => line.Insets));
        Assert.Contains(lines, line => line.Text.Contains("▒▒▒▒ A cartoon sheep", StringComparison.Ordinal));

        // Not linked: this terminal can draw, so the address is not what the reader is being offered.
        Assert.DoesNotContain(lines, line => line.Text.StartsWith("⏵ ", StringComparison.Ordinal));
    }

    /// <summary>The description stands above the picture, so it does not move when the picture lands under it.</summary>
    [Fact]
    public void Feed_PutsTheDescriptionAboveThePicture()
    {
        var lines = PostLines.Feed(
            APost.With(media: [APost.APicture()]),
            61,
            revealed: false,
            Now,
            FakePictures.With().Holding("m1", 400, 300));

        var described = lines.ToList().FindIndex(line => line.Text.StartsWith("▒▒▒▒", StringComparison.Ordinal));
        var drawn = lines.ToList().FindIndex(line => line.Insets.Count > 0);

        Assert.True(described >= 0 && drawn > described, "The picture was not drawn under its description.");
    }

    /// <summary>
    ///     The fourth acceptance criterion. A video and a sound have no frame to draw, so they get a link and what
    ///     their author said they are — even on a terminal that draws pictures perfectly well.
    /// </summary>
    [Theory]
    [InlineData(MediaKind.Animation)]
    [InlineData(MediaKind.Video)]
    [InlineData(MediaKind.Audio)]
    [InlineData(MediaKind.Unknown)]
    public void Feed_LinksWhatItCannotDrawRatherThanDrawingIt(MediaKind kind)
    {
        var lines = PostLines.Feed(
            APost.With(media: [APost.Attached(kind, description: "Sheep, at length")]),
            61,
            revealed: false,
            Now,
            FakePictures.With().Holding("m1", 400, 300));

        Assert.Empty(lines.SelectMany(line => line.Insets));
        Assert.Contains(lines, line => line.Text.Contains("⏵ Sheep, at length", StringComparison.Ordinal));
    }

    /// <summary>
    ///     A real attachment address is longer than the 61 columns the contract gives a screen, so it is wrapped over
    ///     however many rows it takes rather than clipped. A link with its end cut off is a link nobody can follow,
    ///     which is not "shown as a link" (story 51).
    /// </summary>
    [Fact]
    public void Feed_WrapsALinkTooLongForTheRowRatherThanCuttingItsEndOff()
    {
        const string address =
            "https://files.mastodon.social/media_attachments/files/113/427/981/002/443/original/"
            + "0f6a1c2b3d4e5f60.mp4";

        var lines = PostLines.Feed(
            APost.With(media: [APost.Attached(MediaKind.Video) with { Url = address }]),
            61,
            revealed: false,
            Now);

        // The rows under the ⏵ that says what it is, which are the address and nothing else.
        var written = string.Concat(
            lines.SkipWhile(line => !line.Text.StartsWith("⏵ ", StringComparison.Ordinal))
                 .Skip(1)
                 .TakeWhile(line => line.Text.StartsWith("  ", StringComparison.Ordinal))
                 .Select(line => line.Text.Trim()));

        Assert.Equal(address, written);
        Assert.All(lines, line => Assert.True(line.Width <= 61, $"'{line.Text}' is {line.Width} columns"));
    }

    /// <summary>
    ///     A post carrying both draws the one it can and links the other, rather than treating the post as one kind of
    ///     thing because of what it happens to lead with.
    /// </summary>
    [Fact]
    public void Feed_DrawsThePicturesAndLinksTheRestOfWhatOnePostCarries()
    {
        var lines = PostLines.Feed(
            APost.With(media: [APost.Attached(MediaKind.Video, id: "m1"), APost.APicture(id: "m2")]),
            61,
            revealed: false,
            Now,
            FakePictures.With().Holding("m2", 400, 300));

        Assert.Equal("m2", Assert.Single(lines.SelectMany(line => line.Insets)).Media.Id);
        Assert.Contains(lines, line => line.Text.StartsWith("⏵ ", StringComparison.Ordinal));
    }

    /// <summary>Four pictures — Mastodon's most — each get a box, in the order their author attached them.</summary>
    [Fact]
    public void Feed_DrawsEveryPictureAPostCarries()
    {
        var pictures = FakePictures.With();
        var media = new List<PostMedia>();

        for (var at = 1; at <= 4; at++)
        {
            media.Add(APost.APicture(id: $"m{at}"));
            pictures.Holding($"m{at}", 400, 300);
        }

        var insets = PostLines.Feed(APost.With(media: media), 61, revealed: false, Now, pictures)
                              .SelectMany(line => line.Insets)
                              .ToList();

        Assert.Equal(["m1", "m2", "m3", "m4"], insets.Select(inset => inset.Media.Id));
    }

    /// <summary>
    ///     The narrow case, taken past what the contract asks: at any width a terminal can be, no box runs past the
    ///     room. The row standing in for a box always measures it, so what scrolls and what is drawn cannot come apart.
    /// </summary>
    [Theory]
    [InlineData(1)]
    [InlineData(4)]
    [InlineData(20)]
    [InlineData(61)]
    [InlineData(200)]
    public void Feed_KeepsABoxInsideTheRoomAtAnyWidth(int width)
    {
        var post = APost.With(media: [APost.APicture(id: "m1"), APost.APicture(id: "m2")]);

        var pictures = FakePictures.With().Holding("m1", 400, 300).Holding("m2", 300, 900);

        var lines = PostLines.Feed(post, width, revealed: false, Now, pictures);

        foreach (var line in lines.Where(line => line.Insets.Count > 0))
        {
            Assert.Equal(Inset.Width(line.Insets), line.Width);
            Assert.All(line.Insets, inset => Assert.True(inset.Column + inset.Columns <= width));
        }
    }

    /// <summary>
    ///     Anything put in front of a row moves the picture along with the text. A post screen draws a gutter on every
    ///     row it owns, and a box left where the gutter now is would be drawn over the selection mark.
    /// </summary>
    [Fact]
    public void Whole_MovesAPictureAlongWithTheGutterInFrontOfIt()
    {
        var post = APost.With(media: [APost.APicture()]);
        var pictures = FakePictures.With().Holding("m1", 400, 300);

        var withoutGutter = PostLines.Whole(post, 60, revealed: false, Now, pictures)
                                     .SelectMany(line => line.Insets)
                                     .Single();

        var onScreen = new PostScreen(post, []).Lines(61, Now, pictures)
                                               .SelectMany(line => line.Insets)
                                               .Single();

        Assert.Equal(withoutGutter.Column + PickedPosts.Gutter(picked: false).Width, onScreen.Column);
        Assert.Equal(withoutGutter.Columns, onScreen.Columns);
    }

    /// <summary>
    ///     A picture is a picture whichever screen it turns up on, so every screen that shows a post hands the pictures
    ///     down to it rather than one of them quietly dropping them.
    /// </summary>
    [Fact]
    public void EveryScreenShowingAPostGivesItsPicturesABox()
    {
        var post = APost.With(media: [APost.APicture()]);
        var pictures = FakePictures.With().Holding("m1", 400, 300);

        Assert.NotEmpty(new PostScreen(post, []).Lines(61, Now, pictures).SelectMany(line => line.Insets));
        Assert.NotEmpty(new PostScreen(APost.With(id: "1"), [post]).Lines(61, Now, pictures).SelectMany(line => line.Insets));

        var home = new Destination(DestinationKind.Home, "Home", Timeline.Home);

        Assert.NotEmpty(new FeedScreen(home, [post]).Lines(61, Now, pictures).SelectMany(line => line.Insets));
    }

    /// <summary>
    ///     A row with nothing in front of it and nothing on it is not carrying somebody else's picture. Guards the
    ///     shared <see cref="Line.Blank" />, which a box uses for the rows it covers.
    /// </summary>
    [Fact]
    public void ABlankRowCarriesNoPicture()
    {
        Assert.Empty(Line.Blank.Insets);
        Assert.Empty(Line.Of("anything", Role.Body).Insets);
    }
}
