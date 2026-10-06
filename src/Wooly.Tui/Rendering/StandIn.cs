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
}
