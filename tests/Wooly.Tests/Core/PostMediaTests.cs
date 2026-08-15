using Wooly.Core.Posts;
using Wooly.Tests.Fakes;

namespace Wooly.Tests.Core;

/// <summary>
///     The questions both surfaces ask an attachment and neither should answer for itself: whether a terminal can draw
///     it, whether it is reached by opening its address, and what to say it shows. Asserted here rather than once per
///     surface, because the point of them living on the attachment is that the CLI and the TUI cannot come to disagree
///     about the same file (ADR-0016).
/// </summary>
public class PostMediaTests
{
    /// <summary>
    ///     A still picture, and a video or an animation the instance offered a preview of — the three things there are
    ///     pixels to put in a box for. A sound's cover art is not a frame standing in for motion and does not earn a
    ///     box, and an attachment of a kind this client has no word for cannot promise a box means anything (ADR-0017).
    /// </summary>
    [Theory]
    [InlineData(MediaKind.Image, true)]
    [InlineData(MediaKind.Animation, true)]
    [InlineData(MediaKind.Video, true)]
    [InlineData(MediaKind.Audio, false)]
    [InlineData(MediaKind.Unknown, false)]
    public void IsDrawable_IsTrueOfAPictureAndOfAVideoOrAnimationWithAPreview(MediaKind kind, bool expected) =>
        Assert.Equal(expected, APost.Attached(kind).IsDrawable);

    /// <summary>
    ///     A video or an animation the instance offered no preview of has nothing to draw: its own file is motion
    ///     rather than a picture, and sending for it would fetch a whole video to fail to decode it. A still picture is
    ///     the one kind whose own file stands in for a missing preview, because that file is already a picture.
    /// </summary>
    [Theory]
    [InlineData(MediaKind.Image, true)]
    [InlineData(MediaKind.Animation, false)]
    [InlineData(MediaKind.Video, false)]
    public void IsDrawable_IsFalseOfAVideoOrAnimationTheInstanceOfferedNoPreviewOf(MediaKind kind, bool expected) =>
        Assert.Equal(expected, (APost.Attached(kind) with { Preview = null }).IsDrawable);

    /// <summary>
    ///     Which attachments are reached by opening their address rather than by looking at them: everything but a
    ///     still picture, which is the whole of itself once drawn and is linked the CLI's way where it cannot be
    ///     (ADR-0017). Orthogonal to <c>IsDrawable</c> — a video is both drawn and opened, and the preview being drawn
    ///     is exactly why it needs a label saying what opening it reaches.
    /// </summary>
    [Theory]
    [InlineData(MediaKind.Image, false)]
    [InlineData(MediaKind.Animation, true)]
    [InlineData(MediaKind.Video, true)]
    [InlineData(MediaKind.Audio, true)]
    [InlineData(MediaKind.Unknown, true)]
    public void Opens_IsTrueOfEverythingButAStillPicture(MediaKind kind, bool expected) =>
        Assert.Equal(expected, APost.Attached(kind).Opens);

    /// <summary>What the author said it shows is what it shows; this client does not improve on it.</summary>
    [Fact]
    public void Shows_IsTheDescriptionItsAuthorGaveIt() =>
        Assert.Equal("A cartoon sheep", APost.APicture(description: "A cartoon sheep").Shows);

    /// <summary>
    ///     Where nobody described it, saying so is the answer. A reader who is told "a picture, undescribed" knows the
    ///     description is missing; one shown an empty line cannot tell that from a picture of nothing.
    /// </summary>
    [Theory]
    [InlineData(MediaKind.Image, "a picture, undescribed")]
    [InlineData(MediaKind.Animation, "an animation, undescribed")]
    [InlineData(MediaKind.Video, "a video, undescribed")]
    [InlineData(MediaKind.Audio, "some audio, undescribed")]
    [InlineData(MediaKind.Unknown, "an attachment, undescribed")]
    public void Shows_NamesTheKindAndSaysSoWhereNobodyDescribedIt(MediaKind kind, string expected) =>
        Assert.Equal(expected, APost.Attached(kind, description: null).Shows);

    /// <summary>Every kind has a word, including one this client has no word for.</summary>
    [Fact]
    public void MediaKindName_NamesEveryKind() =>
        Assert.All(Enum.GetValues<MediaKind>(), kind => Assert.False(string.IsNullOrWhiteSpace(MediaKindName.Of(kind))));
}
