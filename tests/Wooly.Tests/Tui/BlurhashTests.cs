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

    /// <summary>
    ///     A hash is decoded once by the blurs that hold it and answered from them after, since rows are worked out on
    ///     every frame; and each holder of blurs decodes its own, so nothing one shell or test decodes is another's.
    /// </summary>
    [Fact]
    public void Blurs_DecodesAHashOnceAndHoldsItForThemAlone()
    {
        var blurs = new Blurs();

        var blur = blurs.Of("00TI:j");

        Assert.NotNull(blur);
        Assert.Equal(Blurs.Side, blur.Width);
        Assert.Same(blur, blurs.Of("00TI:j"));
        Assert.NotSame(blur, new Blurs().Of("00TI:j"));
    }

    /// <summary>
    ///     Past <see cref="Blurs.MostBlurs" /> the blur decoded longest ago is let go of, and decoded again if it is
    ///     asked for again.
    /// </summary>
    [Fact]
    public void Blurs_LetsGoOfTheBlurDecodedLongestAgoPastTheMost()
    {
        var blurs = new Blurs();
        var first = blurs.Of("00TI:j");

        for (var at = 0; at < Blurs.MostBlurs; at++)
        {
            _ = blurs.Of(string.Concat("00", Base83(at / 83), Base83(at % 83), ":j"));
        }

        Assert.NotSame(first, blurs.Of("00TI:j"));
    }

    /// <summary>One digit of the blurhash's base 83, so a test can make as many distinct hashes as it needs.</summary>
    private static string Base83(int digit) =>
        "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz#$%*+,-.:;=?@[]^_{|}~"[digit].ToString();
}
