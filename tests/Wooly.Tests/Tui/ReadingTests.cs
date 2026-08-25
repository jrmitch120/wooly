using Wooly.Tests.Fakes;
using Wooly.Tui.Rendering;
using Wooly.Tui.Screens;
using Wooly.Tui.Theme;

namespace Wooly.Tests.Tui;

/// <summary>
///     What a reader has done to one post, said once (#95): the case that matters is the untouched one, since that is
///     what most of a feed is and what <see langword="default" /> has to mean.
/// </summary>
/// <remarks>
///     The rest of what <see cref="Reading" /> carries is asserted where it is drawn — a warning asked past in
///     <see cref="RoleTests" /> and <see cref="ScreenRevealTests" />, a picked reference in
///     <see cref="ReferenceWalkTests" />. What is left here is the seam itself: that a post nobody has touched needs
///     no arguments at all.
/// </remarks>
public class ReadingTests
{
    /// <summary>
    ///     Drawing from <see langword="default" /> honours the warning it was not asked past and picks out nothing,
    ///     which is the whole of what the two absent arguments used to mean.
    /// </summary>
    [Fact]
    public void Default_HonoursTheWarningAndPicksOutNothing()
    {
        var post = APost.With(contentWarning: "spoilers", content: "Thanks @maria@fosstodon.org");

        var lines = PostLines.Feed(post, new Drawing(61, DateTimeOffset.UnixEpoch), default);

        Assert.Contains(lines, line => line.Spans.Any(span => span.Role == Role.ContentWarning));
        Assert.DoesNotContain(lines, line => line.Text.Contains('‹'));
    }
}
