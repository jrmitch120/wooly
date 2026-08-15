using Wooly.Core.Posts;
using Wooly.Tests.Fakes;
using Wooly.Tui.Rendering;
using Wooly.Tui.Screens;
using Wooly.Tui.Shell;

namespace Wooly.Tests.Tui;

/// <summary>
///     A <c>Video</c>, <c>Animation</c>, <c>Audio</c> or <c>Unknown</c> attachment's own address, walked and opened
///     the way a link inside a post's text already is (#109, ADR-0017). The breadth of the walk itself —
///     every screen, brackets, the status row — is already <see cref="ReferenceWalkTests" />'s and
///     <see cref="ReferenceOpenTests" />'s to cover; this is what changes about it once an attachment joins.
/// </summary>
public class AttachmentReferenceTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 29, 12, 30, 0, TimeSpan.Zero);

    /// <summary>A post with one of each text reference, walked in #83 already, ahead of two attachments.</summary>
    private const string Said = "Thanks @maria@fosstodon.org — notes at https://example.com/notes #dotnet";

    /// <summary>
    ///     The walk reaches an attachment only after every one of the post's own text references, in attachment order
    ///     — the first acceptance criterion.
    /// </summary>
    [Fact]
    public void References_ReachesAttachmentsAfterTheTextOnesInAttachmentOrder()
    {
        var post = APost.With(
            content: Said,
            media: [APost.Attached(MediaKind.Video, id: "m1"), APost.Attached(MediaKind.Audio, id: "m2")]);

        var feed = new FeedScreen(new Destination(DestinationKind.Home, "Home"), [post]);

        Assert.Equal(5, feed.References.Count);

        for (var at = 0; at < 3; at++)
        {
            feed.WalkReference(1);
        }

        feed.WalkReference(1);
        Assert.Equal("https://files.mastodon.social/m1/original.png", feed.Reference?.Text);

        feed.WalkReference(1);
        Assert.Equal("https://files.mastodon.social/m2/original.png", feed.Reference?.Text);
    }

    /// <summary>The second acceptance criterion: an <c>Image</c> attachment never enters the walk.</summary>
    [Fact]
    public void References_NeverIncludesAnImageAttachment()
    {
        var post = APost.With(
            content: "Hello",
            media: [APost.APicture(id: "m1"), APost.Attached(MediaKind.Video, id: "m2")]);

        var feed = new FeedScreen(new Destination(DestinationKind.Home, "Home"), [post]);

        Assert.Single(feed.References);
        Assert.Equal("https://files.mastodon.social/m2/original.png", feed.References[0].Text);
    }

    /// <summary>
    ///     The third and fourth acceptance criteria: the label is a permanent, capitalized, one-word span — bracketed
    ///     while picked — with the author's own description alongside it.
    /// </summary>
    [Theory]
    [InlineData(MediaKind.Animation, "Animation")]
    [InlineData(MediaKind.Video, "Video")]
    [InlineData(MediaKind.Audio, "Audio")]
    [InlineData(MediaKind.Unknown, "Unknown")]
    public void Drawn_BracketsTheKindsLabelWithTheDescriptionAlongsideItWhilePicked(MediaKind kind, string label)
    {
        var post = APost.With(content: "Hello", media: [APost.Attached(kind, description: "Sheep, at length")]);
        var reference = AttachmentReferences.Of(post).Single();

        var unpicked = PostLines.Feed(post, 61, default, Now);
        var picked = PostLines.Feed(post, 61, new Reading(Reference: reference), Now);

        Assert.Contains(unpicked, line => line.Text.Contains($"⏵ {label} Sheep, at length"));
        Assert.Contains(picked, line => line.Text.Contains($"⏵ ‹{label}› Sheep, at length"));
    }

    /// <summary>
    ///     The label is shown even where the author gave no description — it is not standing in for one anymore, it is
    ///     what is walked to and opened.
    /// </summary>
    [Fact]
    public void Drawn_ShowsTheLabelEvenWithNoDescription()
    {
        var post = APost.With(content: "Hello", media: [APost.Attached(MediaKind.Video, description: null)]);

        var lines = PostLines.Feed(post, 61, default, Now);

        Assert.Contains(lines, line => line.Text.Contains("⏵ Video"));
    }

    /// <summary>
    ///     The eighth acceptance criterion: the raw address is no longer printed as wrapped rows under the mark, the
    ///     way it was before #109 and still is for an undrawn <c>Image</c> (<see cref="MediaLineTests" />).
    /// </summary>
    [Fact]
    public void Drawn_NeverPrintsTheRawAddressAnymore()
    {
        var post = APost.With(
            content: "Hello",
            media: [APost.Attached(MediaKind.Video) with { Url = "https://files.mastodon.social/m1/original.mp4" }]);

        var lines = PostLines.Feed(post, 61, default, Now);

        Assert.DoesNotContain(lines, line => line.Text.Contains("https://files.mastodon.social/m1/original.mp4"));
    }

    /// <summary>
    ///     The fifth and sixth acceptance criteria: <c>⏎</c> on a picked attachment reference opens its address the
    ///     exact way a picked link reference already does — the same <c>IWebBrowser</c> call, the same refusals.
    /// </summary>
    [Fact]
    public async Task Enter_OpensThePickedAttachmentsAddressTheSameWayALinkAlreadyOpens()
    {
        var built = new AShell
        {
            Timelines = FakeTimelineReader.Holding(
                APost.With(id: "110", content: "Hello", media: [APost.Attached(MediaKind.Video, id: "m1")])),
        };

        var shell = await built.Opened();

        shell.WalkReference(1);
        await shell.OpenReference();

        built.Host.Drain();

        Assert.Equal(
            "https://files.mastodon.social/m1/original.png",
            Assert.Single(built.Browser.Opened).AbsoluteUri);
        Assert.Equal(1, shell.Depth);
        Assert.Null(shell.Notice);
    }

    /// <summary>A refusal — no browser at all — reads exactly the way it does for a picked link (#85).</summary>
    [Fact]
    public async Task Enter_SaysSoWhenThereIsNoBrowserToOpenTheAttachmentWith()
    {
        var built = new AShell
        {
            Timelines = FakeTimelineReader.Holding(
                APost.With(id: "110", content: "Hello", media: [APost.Attached(MediaKind.Video, id: "m1")])),
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
    ///     The seventh acceptance criterion, first half: <c>esc</c> clears a picked attachment reference before it
    ///     pops the screen — exactly the level a picked link reference already stands on (#83).
    /// </summary>
    [Fact]
    public void Esc_ClearsAPickedAttachmentReferenceTheSameWayItClearsALink()
    {
        var post = APost.With(content: "Hello", media: [APost.Attached(MediaKind.Video, id: "m1")]);
        var feed = new FeedScreen(new Destination(DestinationKind.Home, "Home"), [post]);

        feed.WalkReference(1);
        Assert.NotNull(feed.Reference);

        Assert.True(feed.ClearReference());
        Assert.Null(feed.Reference);
    }

    /// <summary>
    ///     The seventh acceptance criterion, second half: walking onto a different post lets a picked attachment
    ///     reference go, the same as it already does for a picked link (#83).
    /// </summary>
    [Fact]
    public void Move_ClearsAPickedAttachmentReferenceTheSameWayItClearsALink()
    {
        var feed = new FeedScreen(
            new Destination(DestinationKind.Home, "Home"),
            [
                APost.With(id: "1", content: "Hello", media: [APost.Attached(MediaKind.Video, id: "m1")]),
                APost.With(id: "2", content: "Hello again"),
            ]);

        feed.WalkReference(1);
        Assert.NotNull(feed.Reference);

        feed.Move(1);

        Assert.Null(feed.Reference);
    }
}
