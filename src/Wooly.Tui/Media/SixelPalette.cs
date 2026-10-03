using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing.Processors.Quantization;
using Terminal.Gui.Drawing;

// Both libraries have a Color and this file is where they meet; the palette is handed back in Terminal.Gui's.
using Color = Terminal.Gui.Drawing.Color;

namespace Wooly.Tui.Media;

/// <summary>
///     The colours a sixel is drawn in, chosen from the whole of the picture by ImageSharp's Wu quantizer (#292,
///     ADR-0023).
/// </summary>
/// <remarks>
///     In place of Terminal.Gui's own builder, which merges colours in the order it meets them — a column at a time
///     from the left edge — and stops at the first 64. A photograph has 64 colours in its first column or two, so the
///     palette was the colours of the picture's left edge and everything else was drawn in the nearest of those: a dark
///     sign in the middle of a pale illustration came out salmon in WezTerm. Built per picture, so a crop of a picture
///     is drawn in the colours of that crop.
///     <para>
///         The one file outside the theme and the decoder besides the placeholders that builds a colour, and for the
///         decoder's reason: these are the photograph's own colours (ADR-0014, ADR-0016).
///     </para>
/// </remarks>
internal sealed class SixelPalette : IPaletteBuilder
{
    /// <inheritdoc />
    public List<Color> BuildPalette(List<Color>? colors, int maxColors)
    {
        if (colors is null || colors.Count == 0 || maxColors <= 0)
        {
            return [];
        }

        // A picture of fewer colours than there is room for is drawn in exactly its own.
        var distinct = colors.Distinct().Take(maxColors + 1).ToList();

        if (distinct.Count <= maxColors)
        {
            return distinct;
        }

        // The pixels laid out as a one-row image, which is all the quantizer needs: it reads colours, not shapes.
        using var image = new Image<Rgba32>(colors.Count, 1);

        image.ProcessPixelRows(rows =>
        {
            var row = rows.GetRowSpan(0);

            for (var at = 0; at < row.Length; at++)
            {
                row[at] = new Rgba32((byte)colors[at].R, (byte)colors[at].G, (byte)colors[at].B, (byte)colors[at].A);
            }
        });

        var options = new QuantizerOptions { MaxColors = Math.Min(maxColors, 256), Dither = null };

        using var quantizer = new WuQuantizer(options).CreatePixelSpecificQuantizer<Rgba32>(Configuration.Default);
        using var _ = quantizer.BuildPaletteAndQuantizeFrame(image.Frames.RootFrame, image.Bounds);

        return [.. quantizer.Palette.ToArray().Select(colour => new Color(colour.R, colour.G, colour.B, colour.A))];
    }
}
