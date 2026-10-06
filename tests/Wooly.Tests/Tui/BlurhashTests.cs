using Terminal.Gui.Drawing;
using Wooly.Tui.Media;

namespace Wooly.Tests.Tui;

/// <summary>
///     Decoding an instance's blurhash into the small picture a <b>Stand-in</b> is drawn from (ADR-0025, #349). The
///     algorithm is the public one from the blurhash project; these pin it against hashes whose pixels can be worked out
///     by hand, and pin that anything else is no picture rather than a failure.
/// </summary>
public class BlurhashTests
{
    /// <summary>
    ///     One component and no detail is a flat colour, the average the hash carries: <c>0</c> says one component
    ///     each way, <c>0</c> no detail, and <c>TI:j</c> is <c>0xFF0000</c> in the hash's base 83.
    /// </summary>
    [Fact]
    public void Decode_DrawsAHashWithNoDetailAsItsAverageColourThroughout()
    {
        var picture = Blurhash.Decode("00TI:j", 4, 3);

        Assert.NotNull(picture);
        Assert.Equal(4, picture.Width);
        Assert.Equal(3, picture.Height);

        for (var x = 0; x < 4; x++)
        {
            for (var y = 0; y < 3; y++)
            {
                Assert.Equal(new Color(255, 0, 0), picture.Pixels[x, y]);
            }
        }
    }

    /// <summary>
    ///     A hash with detail in it is a blur and not a flat colour: the blurhash project's own example, which is
    ///     four components across and three down, decodes at whatever size it is asked for, and not to one colour.
    /// </summary>
    [Fact]
    public void Decode_DrawsAHashWithDetailAsABlurAtTheSizeAskedFor()
    {
        var picture = Blurhash.Decode("LEHV6nWB2yk8pyo0adR*.7kCMdnj", 32, 18);

        Assert.NotNull(picture);
        Assert.Equal(32, picture.Width);
        Assert.Equal(18, picture.Height);
        Assert.NotEqual(picture.Pixels[0, 0], picture.Pixels[31, 17]);
    }

    /// <summary>
    ///     Whatever an instance sends that is not a blurhash is no picture, so the box shows the shaded fill rather
    ///     than the client failing: too short, a length the size flag does not account for, a character outside the
    ///     hash's alphabet.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("0")]
    [InlineData("00TI:")]
    [InlineData("00TI:jj")]
    [InlineData("LEHV6nWB2yk8pyo0adR*.7kCMdn")]
    [InlineData("00TI!j")]
    [InlineData("00TI j")]
    public void Decode_GivesNoPictureForAHashThatIsMalformed(string hash)
    {
        Assert.Null(Blurhash.Decode(hash, 8, 8));
    }

    /// <summary>A size of nothing is no picture either, rather than an empty array to draw.</summary>
    [Theory]
    [InlineData(0, 8)]
    [InlineData(8, 0)]
    public void Decode_GivesNoPictureAtASizeOfNothing(int width, int height)
    {
        Assert.Null(Blurhash.Decode("00TI:j", width, height));
    }
}
