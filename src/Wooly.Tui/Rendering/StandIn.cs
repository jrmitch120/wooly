using Wooly.Tui.Media;
using Wooly.Tui.Theme;

namespace Wooly.Tui.Rendering;

/// <summary>
///     What a <b>drawn</b> picture's box shows while the picture is not here: a plain shaded fill, the size of the box,
///     with nothing written in it (ADR-0025, CONTEXT.md's <b>Stand-in</b>).
/// </summary>
/// <remarks>
///     Said once for an attachment's box and an avatar's, so the two cannot come to shade differently — a byline
///     waiting on a face and a post waiting on a photograph are the same wait. Lighter than the <c>▒▒▒▒</c> a
///     picture's caption is marked with, so a box still waiting reads as a space being kept rather than as something to
///     look at; and in its own role, so a theme can make it recede further still.
/// </remarks>
public static class StandIn
{
    /// <summary>What every cell of a Stand-in is shaded with.</summary>
    public const char Shade = '░';

    /// <summary>One row of a Stand-in <paramref name="columns" /> wide.</summary>
    public static Span Row(int columns) => new(new string(Shade, Math.Max(0, columns)), Role.StandIn);

    /// <summary>
    ///     The blur to draw over <paramref name="box" />'s shade, the size of the whole box, or <see langword="null" />
    ///     where there is no <paramref name="blurhash" />, no <paramref name="blurs" /> to decode it, or it does not
    ///     decode — which leaves the shade alone (#349).
    /// </summary>
    /// <remarks>
    ///     Drawn through the same raster path as the picture it stands in for, on sixel and Kitty alike, and stretched
    ///     to the box rather than fitted to it: a blur has no proportions of its own to keep, and filling the box is
    ///     what makes the picture arriving read as sharpening. Named apart from that picture (<see cref="Drawn.Blur" />)
    ///     so that the box drawing one lets go of it, and the terminal deletes it, when the other takes its place
    ///     (ADR-0022). Its pixels come from <see cref="Blurs" />, never from the picture cache, so a screen of blurs
    ///     can never crowd out the pictures they stand in for (ADR-0025).
    /// </remarks>
    /// <param name="box">The box the Stand-in fills.</param>
    /// <param name="blurhash">The instance's blurhash of the picture, where it sent one.</param>
    /// <param name="blurs">What decodes the blur and holds it, or <see langword="null" /> where none is drawn.</param>
    public static Inset? WithBlur(Inset box, string? blurhash, Blurs? blurs) =>
        blurhash is not null && blurs?.Of(blurhash) is { } blur
            ? box with { Drawn = Drawn.Blur(box.Drawn), Blur = blur }
            : null;
}
