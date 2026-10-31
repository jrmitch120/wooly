using Wooly.Tui.Rendering;

namespace Wooly.Tui.Media;

/// <summary>
///     A picture the view paints over the rows itself, placed on the page: the box its post reserved for it, the row of
///     the page the box starts on — which may be above the top of it, for a box half scrolled off — and the id a Kitty
///     terminal holds the picture under, to be painted as placeholder cells naming it (ADR-0022); or no id, for a
///     custom emoji no box was left for on a way that draws through boxes, painted as the shaded room it was before it
///     landed (ADR-0035).
/// </summary>
/// <param name="Inset">The box, and the picture drawn in it.</param>
/// <param name="Top">The row of the page the box starts on.</param>
/// <param name="Id">
///     The id the terminal holds the picture under, which the placeholder cells name, or <see langword="null" /> where
///     the box is painted as its shade.
/// </param>
internal sealed record Placement(Inset Inset, int Top, int? Id);
