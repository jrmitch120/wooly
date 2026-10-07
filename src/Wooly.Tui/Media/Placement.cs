using Wooly.Tui.Rendering;

namespace Wooly.Tui.Media;

/// <summary>
///     A picture a Kitty terminal holds, placed on the page to be painted as placeholder cells: the box its post reserved
///     for it, the row of the page the box starts on — which may be above the top of it, for a box half scrolled off —
///     and the id the terminal holds the picture under (ADR-0022).
/// </summary>
/// <param name="Inset">The box, and the picture drawn in it.</param>
/// <param name="Top">The row of the page the box starts on.</param>
/// <param name="Id">The id the terminal holds the picture under, which the placeholder cells name.</param>
internal sealed record Placement(Inset Inset, int Top, int Id);
