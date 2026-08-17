using Wooly.Core.Posts;
using Wooly.Tests.Fakes;
using Wooly.Tui.Media;
using Wooly.Tui.Rendering;
using Wooly.Tui.Screens;
using Wooly.Tui.Theme;

namespace Wooly.Tests.Tui;

/// <summary>
///     What an instance made of a link in a post's own text, as the TUI draws it: after everything the author
///     attached, its title walked to and opened, its picture in the same box an attachment's goes in, and the rest of
///     what the instance said about the page as text (#116, ADR-0018).
/// </summary>
/// <remarks>
///     The decision rather than the pixels, the half <see cref="MediaLineTests" /> and
///     <see cref="AttachmentPreviewTests" /> already hold for an attachment: which rows a preview becomes, what they
///     say they want, and what is left of them on a terminal that draws nothing. The walk itself is
///     <see cref="LinkPreviewReferenceTests" />'.
/// </remarks>
public class LinkPreviewLineTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 29, 12, 30, 0, TimeSpan.Zero);

    /// <summary>
    ///     The first acceptance criterion: the preview is drawn after everything the author attached, which is the
    ///     order Mastodon's own web UI puts them in — text, then attachments, then the preview (ADR-0018).
    /// </summary>
    [Fact]
    public void Feed_DrawsTheLinkPreviewAfterEveryAttachment()
    {
        var lines = PostLines.Feed(
            APost.With(
                media: [APost.APicture(), APost.Attached(MediaKind.Video, id: "m2")],
                linkPreview: APost.ALinkPreview()),
            61,
            default,
            Now,
            FakePictures.DrawingNothing());

        Assert.True(
            At(lines, "Sheep, at length") > At(lines, "⏵ Video"),
            "The link preview was not drawn after the post's attachments.");
    }

    /// <summary>
    ///     What the instance said about the page, where its picture is not being drawn: the title on the row that is
    ///     walked, and the site's own name, the description and the author's name under it.
    /// </summary>
    [Fact]
    public void Feed_SaysTheTitleTheSiteTheDescriptionAndTheAuthor()
    {
        var lines = PostLines.Feed(
            APost.With(linkPreview: APost.ALinkPreview()),
            61,
            default,
            Now,
            FakePictures.DrawingNothing());

        Assert.Contains(lines, line => line.Text.Contains("⏵ Sheep, at length", StringComparison.Ordinal));
        Assert.Contains(lines, line => line.Text.Contains("Example News", StringComparison.Ordinal));
        Assert.Contains(lines, line => line.Text.Contains("What a flock does all winter", StringComparison.Ordinal));
        Assert.Contains(lines, line => line.Text.Contains("by Maria Shepherd", StringComparison.Ordinal));
    }

    /// <summary>
    ///     The walked row falls back to the site's own name, and then to the address itself, for the instances that
    ///     send a preview with no title: there is always a row to press <c>⏎</c> on, because the address is the whole
    ///     reason a preview is drawn at all (ADR-0018).
    /// </summary>
    [Theory]
    [InlineData("Example News", "⏵ Example News")]
    [InlineData(null, "⏵ https://example.com/sheep")]
    public void Feed_FallsBackToTheSiteAndThenTheAddressWhereThereIsNoTitle(string? providerName, string said)
    {
        var lines = PostLines.Feed(
            APost.With(linkPreview: APost.ALinkPreview(title: null, providerName: providerName)),
            61,
            default,
            Now,
            FakePictures.DrawingNothing());

        Assert.Contains(lines, line => line.Text.Contains(said, StringComparison.Ordinal));
    }

    /// <summary>
    ///     And a preview whose site stood in for its title says it once rather than twice — the row that is walked has
    ///     already said it.
    /// </summary>
    [Fact]
    public void Feed_NamesTheSiteOnceWhereItStoodInForTheTitle()
    {
        var lines = PostLines.Feed(
            APost.With(linkPreview: APost.ALinkPreview(title: null)),
            61,
            default,
            Now,
            FakePictures.DrawingNothing());

        Assert.Equal(1, lines.Count(line => line.Text.Contains("Example News", StringComparison.Ordinal)));
    }

    /// <summary>
    ///     The second acceptance criterion: the preview's own picture goes through the exact
    ///     <c>Drawn</c>/<c>Inset</c>/<c>IPictures</c> pipeline an attachment's does — same width-driven box, same cap.
    /// </summary>
    [Fact]
    public void Feed_DrawsThePreviewsPictureInTheSameBoxAnAttachmentsGoesIn()
    {
        var link = APost.ALinkPreview();

        var lines = PostLines.Feed(
            APost.With(linkPreview: link),
            61,
            default,
            Now,
            FakePictures.With(new CellSize(10, 20)).HoldingLinkPreview(link, 400, 200));

        var inset = Assert.Single(lines.SelectMany(line => line.Insets));

        // The same arithmetic an attachment of these proportions gets: 61 columns of 10 pixels, 2:1, is 15 rows of 20.
        Assert.Equal(0, inset.Column);
        Assert.Equal(61, inset.Columns);
        Assert.Equal(15, inset.Rows);
    }

    /// <summary>What is sent for is the picture the instance chose for the link, at the address it named it at.</summary>
    [Fact]
    public void Feed_SendsForThePictureTheInstanceChoseForTheLink()
    {
        var lines = PostLines.Feed(
            APost.With(linkPreview: APost.ALinkPreview()),
            61,
            default,
            Now,
            FakePictures.With());

        var wanted = Assert.Single(lines, line => line.Wants is not null).Wants;

        Assert.Equal("https://files.example.com/sheep/card.png", wanted?.Address);
    }

    /// <summary>
    ///     A preview the instance chose no picture for is not drawn and is not sent for — and still says everything
    ///     else it carries, which is the whole of what it is offering.
    /// </summary>
    [Fact]
    public void Feed_DrawsNoBoxAndSendsForNothingWhereTheInstanceChoseNoPicture()
    {
        var lines = PostLines.Feed(
            APost.With(linkPreview: APost.ALinkPreview(image: null)),
            61,
            default,
            Now,
            FakePictures.With());

        Assert.Empty(lines.SelectMany(line => line.Insets));
        Assert.DoesNotContain(lines, line => line.Wants is not null);
        Assert.Contains(lines, line => line.Text.Contains("⏵ Sheep, at length", StringComparison.Ordinal));
    }

    /// <summary>
    ///     A terminal offering neither sixel nor the Kitty graphics protocol gets what an attachment already falls back
    ///     to there: the words, and nothing sent for (ADR-0016).
    /// </summary>
    [Fact]
    public void Feed_FallsBackToTheWordsOnATerminalThatDrawsNothing()
    {
        var lines = PostLines.Feed(
            APost.With(linkPreview: APost.ALinkPreview()),
            61,
            default,
            Now,
            FakePictures.DrawingNothing());

        Assert.Empty(lines.SelectMany(line => line.Insets));
        Assert.DoesNotContain(lines, line => line.Wants is not null);
        Assert.Contains(lines, line => line.Text.Contains("⏵ Sheep, at length", StringComparison.Ordinal));
    }

    /// <summary>
    ///     The box goes under the row that opens the link, so nothing a reader is looking at moves when the pixels
    ///     land — the same order an attachment's caption and box are already in.
    /// </summary>
    [Fact]
    public void Feed_PutsTheBoxUnderTheRowThatOpensTheLink()
    {
        var link = APost.ALinkPreview();

        var lines = PostLines.Feed(
            APost.With(linkPreview: link),
            61,
            default,
            Now,
            FakePictures.With().HoldingLinkPreview(link, 400, 300));

        var box = lines.ToList().FindIndex(line => line.Insets.Count > 0);

        Assert.Equal(1, lines.Count(line => line.Insets.Count > 0));
        Assert.True(box > At(lines, "⏵ Sheep, at length"), "The picture was not drawn under the row that opens it.");
    }

    /// <summary>
    ///     The third acceptance criterion: the author's name is plain text. It is drawn in no role that says a key acts
    ///     on it, and it is not what the walk reaches — that half is
    ///     <see cref="LinkPreviewReferenceTests.References_NeverWalksToTheAuthorsName" />'s.
    /// </summary>
    [Fact]
    public void Feed_DrawsTheAuthorsNameAsPlainText()
    {
        var lines = PostLines.Feed(
            APost.With(linkPreview: APost.ALinkPreview()),
            61,
            default,
            Now,
            FakePictures.DrawingNothing());

        var byline = Assert.Single(lines, line => line.Text.Contains("by Maria Shepherd", StringComparison.Ordinal));

        Assert.All(byline.Spans, span => Assert.Equal(Role.Muted, span.Role));
    }

    /// <summary>A preview whose page named no author says nothing where the name would have been.</summary>
    [Fact]
    public void Feed_SaysNothingWhereThePageNamedNoAuthor()
    {
        var lines = PostLines.Feed(
            APost.With(linkPreview: APost.ALinkPreview(author: null)),
            61,
            default,
            Now,
            FakePictures.DrawingNothing());

        Assert.DoesNotContain(lines, line => line.Text.Contains(" by ", StringComparison.Ordinal));
    }

    /// <summary>
    ///     The description stays put under a picture that has landed, whatever the reader asked of
    ///     <c>hide_drawn_caption</c>: that preference drops a caption a box has taken over saying (#71), and a link
    ///     preview's description is about the page rather than about its picture.
    /// </summary>
    [Fact]
    public void Feed_KeepsTheDescriptionOnceThePictureIsDrawnEvenWhenCaptionsAreHidden()
    {
        var link = APost.ALinkPreview();

        var lines = PostLines.Feed(
            APost.With(linkPreview: link),
            61,
            default,
            Now,
            FakePictures.With().HoldingLinkPreview(link, 400, 300),
            hideDrawnCaption: true);

        Assert.NotEmpty(lines.SelectMany(line => line.Insets));
        Assert.Contains(lines, line => line.Text.Contains("What a flock does all winter", StringComparison.Ordinal));
    }

    /// <summary>
    ///     The post screen draws the same preview with the room it gives an attachment's picture, and brackets the row
    ///     the reader has walked to — the drawing and the walk being two things about one preview.
    /// </summary>
    [Fact]
    public void Whole_BracketsThePickedRowAndDrawsThePictureUnderIt()
    {
        var link = APost.ALinkPreview();
        var post = APost.With(linkPreview: link);

        var lines = PostLines.Whole(
            post,
            61,
            new Reading(Reference: LinkPreviewReference.Of(post)),
            Now,
            FakePictures.With(new CellSize(10, 20)).HoldingLinkPreview(link, 400, 400));

        var inset = Assert.Single(lines.SelectMany(line => line.Insets));

        // The post screen's own cap, which is roomier than a feed item's: a square picture at 61 columns of 10 pixels
        // is 31 rows of 20, under the 32 the post screen allows and over the 16 a feed does.
        Assert.Equal(31, inset.Rows);

        Assert.Contains(lines, line => line.Text.Contains("⏵ ‹Sheep, at length›", StringComparison.Ordinal));
    }

    /// <summary>A boost's preview is the boosted post's, along with its text and everything attached to it.</summary>
    [Fact]
    public void Feed_DrawsTheLinkPreviewOfThePostInsideABoost()
    {
        var boost = APost.With(
            id: "1",
            content: string.Empty,
            boosted: APost.With(id: "2", linkPreview: APost.ALinkPreview()));

        var lines = PostLines.Feed(boost, 61, default, Now, FakePictures.DrawingNothing());

        Assert.Contains(lines, line => line.Text.Contains("⏵ Sheep, at length", StringComparison.Ordinal));
    }

    /// <summary>A post the instance previewed no link in is untouched, which is most of them.</summary>
    [Fact]
    public void Feed_DrawsNothingExtraForAPostWithNoLinkPreview()
    {
        var lines = PostLines.Feed(APost.With(), 61, default, Now, FakePictures.With());

        Assert.DoesNotContain(lines, line => line.Text.Contains("⏵", StringComparison.Ordinal));
        Assert.DoesNotContain(lines, line => line.Wants is not null);
    }

    /// <summary>Where the row saying <paramref name="said" /> is, which is what "after" and "under" mean here.</summary>
    private static int At(IReadOnlyList<Line> lines, string said)
    {
        var at = lines.ToList().FindIndex(line => line.Text.Contains(said, StringComparison.Ordinal));

        Assert.True(at >= 0, $"No row said '{said}'.");

        return at;
    }
}
