using Wooly.Core.Posts;
using Wooly.Tests.Fakes;

namespace Wooly.Tests.Core;

/// <summary>
///     The two questions both surfaces ask an attachment and neither should answer for itself: whether a terminal can
///     draw it, and what to say it shows. Asserted here rather than once per surface, because the point of the pair
///     living on the attachment is that the CLI and the TUI cannot come to disagree about the same file (ADR-0016).
/// </summary>
public class PostMediaTests
{
    /// <summary>
    ///     A still picture is the only thing a terminal can draw. A video and a sound have no frame to stand for them,
    ///     and an animation drawn as one frame would be the misleading inline rendering story 51 rules out.
    /// </summary>
    [Theory]
    [InlineData(MediaKind.Image, true)]
    [InlineData(MediaKind.Animation, false)]
    [InlineData(MediaKind.Video, false)]
    [InlineData(MediaKind.Audio, false)]
    [InlineData(MediaKind.Unknown, false)]
    public void IsDrawable_IsTrueOnlyOfAStillPicture(MediaKind kind, bool expected) =>
        Assert.Equal(expected, APost.Attached(kind).IsDrawable);

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
