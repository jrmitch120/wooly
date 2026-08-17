using Wooly.Core.Posts;
using Wooly.Tests.Fakes;
using Wooly.Tui.Rendering;
using Wooly.Tui.Screens;
using Wooly.Tui.Shell;

namespace Wooly.Tests.Tui;

/// <summary>
///     A post's attachments as part of what its warning covers: nothing drawn, nothing sent for and nothing walked
///     until the reader has asked past it with <c>x</c> — by a spoiler text, by the instance's own sensitive flag, or
///     by both (#113, ADR-0016's amendment).
/// </summary>
/// <remarks>
///     The half a test can hold, which is the same half <see cref="MediaLineTests" /> and
///     <see cref="AttachmentPreviewTests" /> hold: what the rows are and what they say they want, rather than the
///     pixels. What <c>x</c> itself answers to is <see cref="ScreenRevealTests" />'.
/// </remarks>
public class SensitiveMediaTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 29, 12, 30, 0, TimeSpan.Zero);

    /// <summary>The three ways a post carrying attachments is warned: the flag, a spoiler text, or both.</summary>
    public static TheoryData<string?, bool> Warned => new()
    {
        { null, true },
        { "spoilers", false },
        { "spoilers", true },
    };

    /// <summary>
    ///     The first and third acceptance criteria: a warned post's attachments draw no box and print no label, no
    ///     description and no address, however they arrived and whatever kind they are.
    /// </summary>
    [Theory]
    [MemberData(nameof(Warned))]
    public void Feed_DrawsNothingForAWarnedPostsAttachments(string? contentWarning, bool sensitive)
    {
        var lines = PostLines.Feed(
            Hiding(contentWarning, sensitive),
            61,
            default,
            Now,
            FakePictures.With().Holding("m1", 400, 300).Holding("m2", 400, 300));

        Assert.Empty(lines.SelectMany(line => line.Insets));

        foreach (var said in new[] { "▒▒▒▒", "⏵", "A cartoon sheep", "https://files.mastodon.social" })
        {
            Assert.DoesNotContain(lines, line => line.Text.Contains(said, StringComparison.Ordinal));
        }
    }

    /// <summary>
    ///     The fifth acceptance criterion, and the point rather than a side effect: a hidden attachment carries no
    ///     <c>Wants</c>, so the view has nothing to ask <c>IPictures</c> for and a reader scrolling a feed of sensitive
    ///     posts pays no data for pixels they have not asked to see.
    /// </summary>
    [Theory]
    [MemberData(nameof(Warned))]
    public void Feed_SendsForNothingWhileAWarnedPostsAttachmentsAreHidden(string? contentWarning, bool sensitive)
    {
        var pictures = FakePictures.With();

        var lines = PostLines.Feed(Hiding(contentWarning, sensitive), 61, default, Now, pictures);

        Assert.DoesNotContain(lines, line => line.Wants is not null);
        Assert.Empty(pictures.Asked);
    }

    /// <summary>
    ///     The seventh acceptance criterion: asked past, the post reads exactly as the same post reads with nothing
    ///     hiding it — box, label, description, address and all. Said as the two being the same rows rather than as a
    ///     list of what each of them says, because "unchanged" is the whole claim.
    /// </summary>
    [Fact]
    public void Feed_ReadsExactlyAsItDoesTodayOnceTheReaderHasAskedPastTheWarning()
    {
        var pictures = FakePictures.With().Holding("m1", 400, 300).Holding("m2", 400, 300);

        var revealed = PostLines.Feed(
            Hiding(contentWarning: null, sensitive: true),
            61,
            new Reading(Revealed: true),
            Now,
            pictures);

        var plain = PostLines.Feed(Hiding(contentWarning: null, sensitive: false), 61, default, Now, pictures);

        Assert.Equal(plain.Select(line => line.Text), revealed.Select(line => line.Text));

        Assert.Equal(
            plain.SelectMany(line => line.Insets).Select(inset => inset.Drawn.Id),
            revealed.SelectMany(line => line.Insets).Select(inset => inset.Drawn.Id));
    }

    /// <summary>The post screen honours it the same way, since both draw their attachments down the one path.</summary>
    [Fact]
    public void Whole_HidesAWarnedPostsAttachmentsAndShowsThemOnceAsked()
    {
        var post = Hiding(contentWarning: null, sensitive: true);
        var pictures = FakePictures.With().Holding("m1", 400, 300);

        Assert.Empty(PostLines.Whole(post, 61, default, Now, pictures).SelectMany(line => line.Insets));

        Assert.NotEmpty(PostLines
                        .Whole(post, 61, new Reading(Revealed: true), Now, pictures)
                        .SelectMany(line => line.Insets));
    }

    /// <summary>
    ///     A post marked sensitive with nothing written over it says so where its attachments would have been, and says
    ///     which key shows them. Without it the flag would hide a photograph and leave no sign that a key meant
    ///     anything — hidden and unaskable-for are not the same thing.
    /// </summary>
    [Fact]
    public void Feed_SaysASensitivePostIsHidingSomethingWhereNoWarningAlreadyDoes()
    {
        var lines = PostLines.Feed(Hiding(contentWarning: null, sensitive: true), 61, default, Now, FakePictures.With());

        Assert.Contains(lines, line => line.Text.Contains("⚠ Sensitive media", StringComparison.Ordinal));
        Assert.Contains(lines, line => line.Text.Contains("x  show it", StringComparison.Ordinal));
    }

    /// <summary>
    ///     And a post whose warning is already asking says it once: the warning over the text is what <c>x</c> answers,
    ///     and a second prompt under it would be the same offer made twice.
    /// </summary>
    [Fact]
    public void Feed_LeavesTheWarningToAskWhereThePostCarriesOne()
    {
        var lines = PostLines.Feed(
            Hiding(contentWarning: "spoilers", sensitive: true),
            61,
            default,
            Now,
            FakePictures.With());

        Assert.DoesNotContain(lines, line => line.Text.Contains("Sensitive media", StringComparison.Ordinal));
        Assert.Equal(1, lines.Count(line => line.Text.Contains("x  show it", StringComparison.Ordinal)));
    }

    /// <summary>
    ///     The flag counts for nothing on a post carrying no attachments, which an instance is free to send: it marks
    ///     media, so with none under it there is nothing behind anything. Nothing is said, and <c>x</c> is not spent
    ///     saying it — a key reported as used is a shell claiming to have acted.
    /// </summary>
    [Fact]
    public void Feed_HidesNothingOnASensitivePostCarryingNoAttachments()
    {
        var post = APost.With(sensitive: true);

        var lines = PostLines.Feed(post, 61, default, Now, FakePictures.With());

        Assert.DoesNotContain(lines, line => line.Text.Contains("Sensitive media", StringComparison.Ordinal));
        Assert.False(Feed(post).Reveal());
    }

    /// <summary>
    ///     The eighth acceptance criterion: a post that is neither sensitive nor warned is untouched, which is most of
    ///     them.
    /// </summary>
    [Fact]
    public void Feed_DrawsThePicturesOfAPostThatIsNeitherSensitiveNorWarned()
    {
        var lines = PostLines.Feed(
            APost.With(media: [APost.APicture()]),
            61,
            default,
            Now,
            FakePictures.With().Holding("m1", 400, 300));

        Assert.NotEmpty(lines.SelectMany(line => line.Insets));
    }

    /// <summary>
    ///     The sixth acceptance criterion: an attachment nobody can see is not walked to either. <c>←</c>/<c>→</c>
    ///     would otherwise bracket a label that is not on screen, and <c>⏎</c> would open a video the reader never
    ///     asked for.
    /// </summary>
    [Theory]
    [MemberData(nameof(Warned))]
    public void References_DoesNotWalkAWarnedPostsAttachmentsUntilItIsRevealed(string? contentWarning, bool sensitive)
    {
        var screen = Feed(Hiding(contentWarning, sensitive));

        Assert.Empty(screen.References);
        Assert.False(screen.WalkReference(1));

        Assert.True(screen.Reveal());

        Assert.Single(screen.References);
        Assert.True(screen.WalkReference(1));
    }

    /// <summary>
    ///     What the flag does not touch: a sensitive post's own text is on screen — Mastodon hides its media, not its
    ///     words — so the references in it are still walked, brackets and all.
    /// </summary>
    [Fact]
    public void References_KeepsWalkingTheTextOfASensitivePostThatCarriesNoWarning()
    {
        var screen = Feed(APost.With(content: "Notes at https://example.com/notes", sensitive: true));

        var walked = Assert.Single(screen.References);

        Assert.Equal("https://example.com/notes", walked.Text);
    }

    /// <summary>
    ///     A boost is asked about by the post inside it, since that is the post whose media was marked — the same rule
    ///     <see cref="Revealed" /> already follows for a warning.
    /// </summary>
    [Fact]
    public void Feed_HidesTheAttachmentsOfAWarnedPostInsideABoost()
    {
        var boost = APost.With(id: "1", content: string.Empty, boosted: Hiding(contentWarning: null, sensitive: true));

        var lines = PostLines.Feed(boost, 61, default, Now, FakePictures.With().Holding("m1", 400, 300));

        Assert.Empty(lines.SelectMany(line => line.Insets));

        var screen = Feed(boost);

        Assert.Empty(screen.References);
        Assert.True(screen.Reveal());
        Assert.Single(screen.References);
    }

    /// <summary>A post carrying a picture and a video, put behind whichever half of the warning is being asked about.</summary>
    private static Post Hiding(string? contentWarning, bool sensitive) => APost.With(
        contentWarning: contentWarning,
        sensitive: sensitive,
        media: [APost.APicture(), APost.Attached(MediaKind.Video, id: "m2")]);

    private static FeedScreen Feed(params Post[] posts) =>
        new(new Destination(DestinationKind.Home, "Home"), posts);
}
