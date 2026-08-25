using Wooly.Tests.Fakes;
using Wooly.Tui.Rendering;
using Wooly.Tui.Screens;

namespace Wooly.Tests.Tui;

/// <summary>
///     What a screen is given that is not about the screen (#148): the case that matters is the one saying nothing
///     beyond the room and the moment, since that is every test and every screen laid out with no terminal in the
///     room.
/// </summary>
/// <remarks>
///     What the four facts <em>do</em> is asserted where they are drawn — a picture's box in
///     <see cref="MediaLineTests" />, a hidden caption there too, the moment in <see cref="PostBylineTests" />. What is
///     left here is the seam itself: that a caller says only what it cares about, and that the one thing which changes
///     on the way down changes nothing else.
/// </remarks>
public class DrawingTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 29, 12, 30, 0, TimeSpan.Zero);

    /// <summary>
    ///     Saying nothing about the terminal links every attachment and keeps every caption, which is the whole of
    ///     what the two absent arguments used to mean.
    /// </summary>
    [Fact]
    public void Default_DrawsNothingAndKeepsTheCaption()
    {
        var post = APost.With(media: [APost.APicture(description: "A cartoon sheep")]);

        var lines = PostLines.Feed(post, new Drawing(61, Now), default);

        Assert.Empty(lines.SelectMany(line => line.Insets));
        Assert.Contains(lines, line => line.Text.Contains("A cartoon sheep", StringComparison.Ordinal));
    }

    /// <summary>
    ///     Narrowing is the one thing a drawing does on its way down a screen, and it leaves the other three where
    ///     they were — a row in a gutter still draws its picture and still honours the preference.
    /// </summary>
    [Fact]
    public void In_TakesTheRoomAndLeavesEverythingElseAlone()
    {
        var drawing = new Drawing(61, Now, FakePictures.With(), HideDrawnCaption: true);

        var narrowed = drawing.In(59);

        Assert.Equal(59, narrowed.Width);
        Assert.Equal(drawing with { Width = 59 }, narrowed);
    }
}
