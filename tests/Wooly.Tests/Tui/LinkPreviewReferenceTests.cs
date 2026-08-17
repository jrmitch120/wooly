using Wooly.Core.Posts;
using Wooly.Tests.Fakes;
using Wooly.Tui.Rendering;
using Wooly.Tui.Screens;
using Wooly.Tui.Shell;

namespace Wooly.Tests.Tui;

/// <summary>
///     A link preview's own address, walked and opened the way an attachment's already is — after every attachment
///     reference, and behind the post's warning with them (#116, ADR-0018). The breadth of the walk itself is
///     <see cref="ReferenceWalkTests" />'s and <see cref="ReferenceOpenTests" />'s; this is what changes about it once
///     a preview joins.
/// </summary>
public class LinkPreviewReferenceTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 29, 12, 30, 0, TimeSpan.Zero);

    /// <summary>A post with one of each text reference, walked in #83 already, ahead of what a preview adds.</summary>
    private const string Said = "Thanks @maria@fosstodon.org — notes at https://example.com/notes #dotnet";

    /// <summary>
    ///     The second acceptance criterion: the walk reaches the preview last of all — after every reference the post's
    ///     text carries, and after every attachment reference.
    /// </summary>
    [Fact]
    public void References_ReachesTheLinkPreviewAfterEveryAttachmentReference()
    {
        var screen = Feed(APost.With(
            content: Said,
            media: [APost.Attached(MediaKind.Video, id: "m1"), APost.Attached(MediaKind.Audio, id: "m2")],
            linkPreview: APost.ALinkPreview()));

        Assert.Equal(6, screen.References.Count);

        for (var at = 0; at < 5; at++)
        {
            screen.WalkReference(1);
        }

        Assert.Equal("https://files.mastodon.social/m2/original.png", screen.Reference?.Text);

        screen.WalkReference(1);

        Assert.Equal("https://example.com/sheep", screen.Reference?.Text);
    }

    /// <summary>
    ///     The third acceptance criterion: the author's name is not a reference of its own, so a post never carries
    ///     three things reaching for the same handful of places (ADR-0018). The preview adds exactly one.
    /// </summary>
    [Fact]
    public void References_NeverWalksToTheAuthorsName()
    {
        var screen = Feed(APost.With(content: "Hello", linkPreview: APost.ALinkPreview()));

        var walked = Assert.Single(screen.References);

        Assert.Equal("https://example.com/sheep", walked.Text);
    }

    /// <summary>
    ///     The preview's address is walked even where the post's own text already reaches it, which is the redundancy
    ///     ADR-0018 accepted deliberately: it is the address that repeats, not what is being offered.
    /// </summary>
    [Fact]
    public void References_WalksThePreviewEvenWhereTheTextAlreadyReachesTheSameAddress()
    {
        var screen = Feed(APost.With(
            content: "Read this: https://example.com/sheep",
            linkPreview: APost.ALinkPreview()));

        Assert.Equal(
            ["https://example.com/sheep", "https://example.com/sheep"],
            screen.References.Select(reference => reference.Text));
    }

    /// <summary>
    ///     The second acceptance criterion's other half: <c>⏎</c> hands the preview's address to the browser through
    ///     the exact call a picked link and a picked attachment already go through, refusal for refusal.
    /// </summary>
    [Fact]
    public async Task Enter_OpensThePreviewsAddressTheSameWayALinkAlreadyOpens()
    {
        var built = new AShell
        {
            Timelines = FakeTimelineReader.Holding(
                APost.With(id: "110", content: "Hello", linkPreview: APost.ALinkPreview())),
        };

        var shell = await built.Opened();

        shell.WalkReference(1);
        await shell.OpenReference();

        built.Host.Drain();

        Assert.Equal("https://example.com/sheep", Assert.Single(built.Browser.Opened).AbsoluteUri);
        Assert.Equal(1, shell.Depth);
        Assert.Null(shell.Notice);
    }

    /// <summary>A refusal — no browser at all — reads exactly the way it does for a picked link (#85).</summary>
    [Fact]
    public async Task Enter_SaysSoWhenThereIsNoBrowserToOpenThePreviewWith()
    {
        var built = new AShell
        {
            Timelines = FakeTimelineReader.Holding(
                APost.With(id: "110", content: "Hello", linkPreview: APost.ALinkPreview())),
            Browser = FakeWebBrowser.WithNothingToOpen(),
        };

        var shell = await built.Opened();

        shell.WalkReference(1);
        await shell.OpenReference();

        built.Host.Drain();

        Assert.Equal("No browser available.", shell.Notice);
        Assert.True(shell.NoticeIsError);
    }

    /// <summary>
    ///     The fourth acceptance criterion, as the walk sees it: a warned post's preview is not walked to until the
    ///     reader has asked past the warning, exactly as its attachments are not — <c>←</c>/<c>→</c> would otherwise
    ///     bracket a row nobody can see.
    /// </summary>
    [Theory]
    [MemberData(nameof(Warned))]
    public void References_DoesNotWalkAWarnedPostsLinkPreviewUntilItIsRevealed(string? contentWarning, bool sensitive)
    {
        var screen = Feed(Hiding(contentWarning, sensitive));

        Assert.Empty(screen.References);
        Assert.False(screen.WalkReference(1));

        Assert.True(screen.Reveal());

        Assert.Equal(2, screen.References.Count);
        Assert.True(screen.WalkReference(1));
    }

    /// <summary>
    ///     The fourth acceptance criterion as the rows see it: nothing about a warned post's preview is drawn, and
    ///     nothing is sent for — no title, no site, no description, no author and no picture (#113's rule, ADR-0018).
    /// </summary>
    [Theory]
    [MemberData(nameof(Warned))]
    public void Feed_DrawsNothingAndSendsForNothingWhileTheWarningStands(string? contentWarning, bool sensitive)
    {
        var pictures = FakePictures.With().HoldingLinkPreview(APost.ALinkPreview(), 400, 300);

        var lines = PostLines.Feed(Hiding(contentWarning, sensitive), 61, default, Now, pictures);

        Assert.Empty(lines.SelectMany(line => line.Insets));
        Assert.DoesNotContain(lines, line => line.Wants is not null);

        foreach (var said in new[] { "Sheep, at length", "Example News", "What a flock does all winter", "Maria" })
        {
            Assert.DoesNotContain(lines, line => line.Text.Contains(said, StringComparison.Ordinal));
        }
    }

    /// <summary>Asked past, it reads exactly as it does on a post with nothing hiding it — the whole claim.</summary>
    [Fact]
    public void Feed_ReadsAsAnUnwarnedPostsPreviewOnceTheReaderHasAskedPastTheWarning()
    {
        var pictures = FakePictures.With().HoldingLinkPreview(APost.ALinkPreview(), 400, 300);

        var revealed = PostLines.Feed(
            Hiding(contentWarning: null, sensitive: true),
            61,
            new Reading(Revealed: true),
            Now,
            pictures);

        var plain = PostLines.Feed(Hiding(contentWarning: null, sensitive: false), 61, default, Now, pictures);

        Assert.Equal(plain.Select(line => line.Text), revealed.Select(line => line.Text));
    }

    /// <summary>
    ///     The flag on a post carrying nothing attached hides nothing, a preview included: it is a mark over the media
    ///     and there is none under it, which is what <see cref="Post.IsWarned" /> already settles (#113). A preview is
    ///     gated on that one question rather than on a second reading of the flag, so there is nowhere for the two to
    ///     disagree — and an author who wrote a warning did write it about the link in their own text, which is the
    ///     half that does hide.
    /// </summary>
    [Fact]
    public void Feed_KeepsThePreviewOfASensitivePostThatCarriesNoAttachments()
    {
        var post = APost.With(sensitive: true, linkPreview: APost.ALinkPreview());

        var lines = PostLines.Feed(post, 61, default, Now, FakePictures.DrawingNothing());

        Assert.Contains(lines, line => line.Text.Contains("⏵ Sheep, at length", StringComparison.Ordinal));
        Assert.Single(Feed(post).References);
    }

    /// <summary>
    ///     A boost is asked about by the post inside it, since that is the post the preview was made for — the same
    ///     rule a warning and an attachment already follow.
    /// </summary>
    [Fact]
    public void References_WalksThePreviewOfThePostInsideABoost()
    {
        var screen = Feed(APost.With(
            id: "1",
            content: string.Empty,
            boosted: APost.With(id: "2", content: "Hello", linkPreview: APost.ALinkPreview())));

        Assert.Equal("https://example.com/sheep", Assert.Single(screen.References).Text);
    }

    /// <summary>Walking onto another post lets a picked preview go, the same as it already does a picked link (#83).</summary>
    [Fact]
    public void Move_ClearsAPickedLinkPreviewTheSameWayItClearsALink()
    {
        var screen = Feed(
            APost.With(id: "1", content: "Hello", linkPreview: APost.ALinkPreview()),
            APost.With(id: "2", content: "Hello again"));

        screen.WalkReference(1);
        Assert.NotNull(screen.Reference);

        screen.Move(1);

        Assert.Null(screen.Reference);
    }

    /// <summary>The three ways a post carrying attachments is warned: the flag, a spoiler text, or both.</summary>
    public static TheoryData<string?, bool> Warned => new()
    {
        { null, true },
        { "spoilers", false },
        { "spoilers", true },
    };

    /// <summary>A post carrying a video and a link preview, put behind whichever half of the warning is asked about.</summary>
    private static Post Hiding(string? contentWarning, bool sensitive) => APost.With(
        contentWarning: contentWarning,
        sensitive: sensitive,
        media: [APost.Attached(MediaKind.Video)],
        linkPreview: APost.ALinkPreview());

    private static FeedScreen Feed(params Post[] posts) =>
        new(new Destination(DestinationKind.Home, "Home"), posts);
}
