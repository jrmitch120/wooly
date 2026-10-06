using Terminal.Gui.Drawing;

namespace Wooly.Tui.Media;

/// <summary>
///     An instance's blurhash, decoded into the small picture a <b>drawn</b> picture's <b>Stand-in</b> is drawn from
///     while the picture itself is on its way (ADR-0025, #349).
/// </summary>
/// <remarks>
///     The algorithm is the blurhash project's own (github.com/woltapp/blurhash), which is short and public: a size flag,
///     a quantised maximum, an average colour, then a handful of cosine components, all in a base 83 of its own.
///     Written here rather than taken as a package because it is a page of arithmetic, and a dependency is a supply line
///     to keep for good.
///     <para>
///         Whatever does not decode — a hash the wrong length for its own size flag, a character outside its alphabet, a
///         value past what its field can hold — is <see langword="null" /> rather than a failure, so the box it was for
///         shows the shaded fill instead: an instance's mistake costs a blur, never a post (#349).
///     </para>
/// </remarks>
public static class Blurhash
{
    private const string Digits =
        "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz#$%*+,-.:;=?@[]^_{|}~";

    /// <summary>
    ///     <paramref name="hash" /> decoded to a picture <paramref name="width" /> by <paramref name="height" /> pixels,
    ///     or <see langword="null" /> where it is not a blurhash or the size is nothing.
    /// </summary>
    public static Picture? Decode(string hash, int width, int height)
    {
        if (width < 1 || height < 1 || hash.Length < 6)
        {
            return null;
        }

        if (Number(hash, 0, 1) is not { } size || Number(hash, 1, 1) is not { } quantisedMost)
        {
            return null;
        }

        var across = (size % 9) + 1;
        var down = (size / 9) + 1;

        if (hash.Length != 4 + (2 * across * down) || Number(hash, 2, 4) is not ({ } average and <= 0xFFFFFF))
        {
            return null;
        }

        var most = (quantisedMost + 1) / 166.0;
        var components = new (double R, double G, double B)[across * down];

        components[0] = (Linear(average >> 16), Linear((average >> 8) & 0xFF), Linear(average & 0xFF));

        for (var at = 1; at < components.Length; at++)
        {
            if (Number(hash, 4 + (at * 2), 2) is not ({ } detail and < 19 * 19 * 19))
            {
                return null;
            }

            components[at] = (
                Signed(detail / (19 * 19), most),
                Signed(detail / 19 % 19, most),
                Signed(detail % 19, most));
        }

        var pixels = new Color[width, height];

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                double r = 0, g = 0, b = 0;

                for (var j = 0; j < down; j++)
                {
                    for (var i = 0; i < across; i++)
                    {
                        var basis = Math.Cos(Math.PI * x * i / width) * Math.Cos(Math.PI * y * j / height);
                        var (cr, cg, cb) = components[i + (j * across)];

                        r += cr * basis;
                        g += cg * basis;
                        b += cb * basis;
                    }
                }

                pixels[x, y] = new Color(Gamma(r), Gamma(g), Gamma(b));
            }
        }

        return new Picture(pixels);
    }

    /// <summary>
    ///     The <paramref name="length" /> characters from <paramref name="start" /> read in the hash's base 83, or
    ///     <see langword="null" /> where any of them is not one of its digits.
    /// </summary>
    private static int? Number(string hash, int start, int length)
    {
        var value = 0;

        for (var at = start; at < start + length; at++)
        {
            var digit = Digits.IndexOf(hash[at], StringComparison.Ordinal);

            if (digit < 0)
            {
                return null;
            }

            value = (value * 83) + digit;
        }

        return value;
    }

    /// <summary>One quantised component channel, back to a signed linear value under <paramref name="most" />.</summary>
    private static double Signed(int quantised, double most)
    {
        var centred = (quantised - 9) / 9.0;

        return Math.CopySign(centred * centred, centred) * most;
    }

    private static double Linear(int channel)
    {
        var v = channel / 255.0;

        return v <= 0.04045 ? v / 12.92 : Math.Pow((v + 0.055) / 1.055, 2.4);
    }

    private static int Gamma(double linear)
    {
        var v = Math.Clamp(linear, 0, 1);

        return v <= 0.0031308
            ? (int)((v * 12.92 * 255) + 0.5)
            : (int)((((1.055 * Math.Pow(v, 1 / 2.4)) - 0.055) * 255) + 0.5);
    }
}
