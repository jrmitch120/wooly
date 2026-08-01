using Wooly.Core.Posts;
using Wooly.Tests.Fakes;
using Wooly.Tui.Rendering;
using Wooly.Tui.Screens;
using Wooly.Tui.Theme;

namespace Wooly.Tests.Tui;

/// <summary>
///     What a post's attachments become in the TUI: which of them get a box drawn in place, how big the boxes are, and
///     what the rest get instead. The decision, not the drawing — ADR-0005 and ADR-0014 leave pixels to a manual smoke
///     test, and this is the half a test can hold: <em>a video is linked rather than drawn</em>, <em>four pictures
///     share the width</em>, <em>a gutter moves the boxes along with the text</em>.
/// </summary>
public class MediaLineTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 29, 12, 30, 0, TimeSpan.Zero);

    /// <summary>The first acceptance criterion, as far as a test without a terminal can see it.</summary>
    [Fact]
    public void Feed_GivesAPictureABoxToBeDrawnIn()
    {
        var lines = PostLines.Feed(APost.With(media: [APost.APicture()]), 61, revealed: false, Now);

        var inset = Assert.Single(lines.SelectMany(line => line.Insets));

        Assert.Equal("m1", inset.Media.Id);
        Assert.Equal(Inset.FeedRows, inset.Rows);
        Assert.Equal(0, inset.Column);
        Assert.True(inset.Columns > 0);
    }

    /// <summary>
    ///     The band is as many rows tall as it says it is, so the rows it covers belong to it rather than to whatever
    ///     was written next.
    /// </summary>
    [Fact]
    public void Feed_KeepsAsManyRowsAsThePictureBoxIsTall()
    {
        var withPicture = PostLines.Feed(APost.With(media: [APost.APicture()]), 61, revealed: false, Now);
        var without = PostLines.Feed(APost.With(), 61, revealed: false, Now);

        // The band, plus the one row saying what it shows.
        Assert.Equal(without.Count + Inset.FeedRows + 1, withPicture.Count);
    }

    /// <summary>
    ///     A post looked at on its own gets a bigger picture than the same post has on a feed. The reader drilled into
    ///     it, which is them saying which post they care about.
    /// </summary>
    [Fact]
    public void Whole_GivesAPictureMoreRoomThanAFeedItemDoes()
    {
        var feed = PostLines.Feed(APost.With(media: [APost.APicture()]), 61, revealed: false, Now);
        var whole = PostLines.Whole(APost.With(media: [APost.APicture()]), 61, revealed: false, Now);

        var inFeed = Assert.Single(feed.SelectMany(line => line.Insets));
        var inWhole = Assert.Single(whole.SelectMany(line => line.Insets));

        Assert.Equal(Inset.WholeRows, inWhole.Rows);
        Assert.True(inWhole.Rows > inFeed.Rows);
        Assert.True(inWhole.Columns > inFeed.Columns);
    }

    /// <summary>
    ///     The fourth acceptance criterion. A video and a sound have no frame to draw, so they get a link and what
    ///     their author said they are — never a box that would sit empty pretending to be a picture.
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
            Now);

        Assert.Empty(lines.SelectMany(line => line.Insets));

        Assert.Contains(lines, line => line.Text.Contains("⏵ Sheep, at length", StringComparison.Ordinal));
        Assert.Contains(
            lines,
            line => line.Text.Contains("https://files.mastodon.social/m1/original.png", StringComparison.Ordinal));
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
            Now);

        Assert.Equal("m2", Assert.Single(lines.SelectMany(line => line.Insets)).Media.Id);
        Assert.Contains(lines, line => line.Text.Contains("⏵ ", StringComparison.Ordinal));
    }

    /// <summary>
    ///     Four pictures — Mastodon's most — share one band rather than taking four, which would bury the post that
    ///     carries them. They do not overlap, and the band does not run past the room it was given.
    /// </summary>
    [Fact]
    public void Feed_LaysFourPicturesSideBySideWithinTheRoomItHas()
    {
        var post = APost.With(media:
        [
            APost.APicture(id: "m1"),
            APost.APicture(id: "m2"),
            APost.APicture(id: "m3"),
            APost.APicture(id: "m4"),
        ]);

        var lines = PostLines.Feed(post, 61, revealed: false, Now);
        var insets = lines.SelectMany(line => line.Insets).ToList();

        Assert.Equal(4, insets.Count);
        Assert.Equal(["m1", "m2", "m3", "m4"], insets.Select(inset => inset.Media.Id));
        Assert.All(insets, inset => Assert.Equal(Inset.FeedRows, inset.Rows));

        for (var at = 1; at < insets.Count; at++)
        {
            Assert.True(
                insets[at].Column >= insets[at - 1].Column + insets[at - 1].Columns + 1,
                "Two pictures were laid out on top of each other.");
        }

        Assert.True(insets[^1].Column + insets[^1].Columns <= 61, "The band ran past the room it was given.");
    }

    /// <summary>
    ///     The narrow case, taken past what the contract asks: at any width a terminal can be, no box runs past the
    ///     room — and at a width with no room for one, there is no band rather than a band drawn over the text. The row
    ///     standing in for a band always measures it, so what scrolls and what is drawn cannot come apart.
    /// </summary>
    [Theory]
    [InlineData(1)]
    [InlineData(4)]
    [InlineData(20)]
    [InlineData(61)]
    [InlineData(200)]
    public void Feed_KeepsTheBandInsideTheRoomAtAnyWidth(int width)
    {
        var post = APost.With(media: [APost.APicture(id: "m1"), APost.APicture(id: "m2")]);

        var lines = PostLines.Feed(post, width, revealed: false, Now);

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

        var withoutGutter = PostLines.Whole(post, 60, revealed: false, Now)
                                     .SelectMany(line => line.Insets)
                                     .Single();

        var onScreen = new PostScreen(post, []).Lines(61, Now)
                                               .SelectMany(line => line.Insets)
                                               .Single();

        Assert.Equal(withoutGutter.Column + PickedPosts.Gutter(picked: false).Width, onScreen.Column);
        Assert.Equal(withoutGutter.Columns, onScreen.Columns);
    }

    /// <summary>
    ///     A picture is a picture whichever screen it turns up on, so the drawn boxes ride the same rows the text does
    ///     everywhere a post is shown.
    /// </summary>
    [Fact]
    public void EveryScreenShowingAPostGivesItsPicturesABox()
    {
        var post = APost.With(media: [APost.APicture()]);

        Assert.NotEmpty(new PostScreen(post, []).Lines(61, Now).SelectMany(line => line.Insets));
        Assert.NotEmpty(new PostScreen(APost.With(id: "1"), [post]).Lines(61, Now).SelectMany(line => line.Insets));
    }

    /// <summary>
    ///     A row with nothing in front of it and nothing on it is not carrying somebody else's picture. Guards the
    ///     shared <see cref="Line.Blank" />, which the band uses for the rows its boxes cover.
    /// </summary>
    [Fact]
    public void ABlankRowCarriesNoPicture()
    {
        Assert.Empty(Line.Blank.Insets);
        Assert.Empty(Line.Of("anything", Role.Body).Insets);
    }
}
