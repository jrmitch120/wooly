using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Dithering;
using SixLabors.ImageSharp.Processing.Processors.Quantization;
using Terminal.Gui.Drawing;

// Both libraries have a Color and this file is where they meet; the palette is handed back in Terminal.Gui's.
using Color = Terminal.Gui.Drawing.Color;

namespace Wooly.Tui.Media;

/// <summary>
///     The colours a sixel is drawn in, chosen from the whole of the picture by ImageSharp's Wu quantizer, and the
///     picture brought down to them before it is encoded (#292, ADR-0023).
/// </summary>
/// <remarks>
///     In place of Terminal.Gui's own palette builder, which merges colours in the order it meets them — a column at a
///     time from the left edge — and stops at the first 64. A photograph has 64 colours in its first column or two, so
///     the palette was the colours of the picture's left edge and everything else was drawn in the nearest of those: a
///     dark sign in the middle of a pale illustration came out salmon in WezTerm.
///     <para>
///         The picture is quantized here as well as its palette chosen, so that every pixel the encoder is handed is a
///         palette colour exactly, which it finds in a dictionary rather than searching the whole palette for the
///         nearest of. That is what makes 256 colours cost no more to encode than 64. Dithering is there to be asked
///         for, and is not: at 256 colours it bought little and made every sixel larger (ADR-0023).
///     </para>
///     <para>
///         The one file outside the theme and the decoder besides the placeholders that builds a colour, and for the
///         decoder's reason: these are the photograph's own colours (ADR-0014, ADR-0016).
///     </para>
/// </remarks>
internal sealed class SixelPalette : IStaticPaletteBuilder
{
    private readonly List<Color> _colours;

    private SixelPalette(List<Color> colours) => _colours = colours;

    /// <summary>The colours chosen, in the order the encoder numbers them.</summary>
    public IReadOnlyList<Color> Colours => _colours;

    /// <summary>
    ///     <paramref name="pixels" /> brought down to at most <paramref name="colours" /> colours chosen from the whole
    ///     of them — dithered where <paramref name="dithered" /> says — and the palette they are now drawn in.
    /// </summary>
    public static (Color[,] Pixels, SixelPalette Palette) Quantized(Color[,] pixels, int colours, bool dithered)
    {
        var width = pixels.GetLength(0);
        var height = pixels.GetLength(1);

        if (width == 0 || height == 0 || colours <= 0)
        {
            return (pixels, new SixelPalette([]));
        }

        using var image = new Image<Rgba32>(width, height);

        image.ProcessPixelRows(rows =>
        {
            for (var y = 0; y < rows.Height; y++)
            {
                var row = rows.GetRowSpan(y);

                for (var x = 0; x < row.Length; x++)
                {
                    var pixel = pixels[x, y];

                    row[x] = new Rgba32((byte)pixel.R, (byte)pixel.G, (byte)pixel.B, (byte)pixel.A);
                }
            }
        });

        var options = new QuantizerOptions
        {
            MaxColors = Math.Clamp(colours, 1, 256),
            Dither = dithered ? KnownDitherings.Bayer8x8 : null,
        };

        using var quantizer = new WuQuantizer(options).CreatePixelSpecificQuantizer<Rgba32>(Configuration.Default);
        using var indexed = quantizer.BuildPaletteAndQuantizeFrame(image.Frames.RootFrame, image.Bounds);

        var palette = indexed.Palette.ToArray().Select(Colour).ToList();
        var quantized = new Color[width, height];

        for (var y = 0; y < height; y++)
        {
            var row = indexed.DangerousGetRowSpan(y);

            for (var x = 0; x < width; x++)
            {
                quantized[x, y] = palette[row[x]];
            }
        }

        return (quantized, new SixelPalette(palette));
    }

    /// <inheritdoc />
    public List<Color> BuildPalette(int maxColors) => _colours;

    /// <inheritdoc />
    public List<Color> BuildPalette(List<Color>? colors, int maxColors) => _colours;

    private static Color Colour(Rgba32 colour) => new(colour.R, colour.G, colour.B, colour.A);
}
