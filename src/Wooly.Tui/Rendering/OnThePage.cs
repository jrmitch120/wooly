namespace Wooly.Tui.Rendering;

/// <summary>
///     What <c>→</c> and <c>←</c> are told of the page before they walk: what to reclaim first, if anything, and which
///     references of the thing they will walk are on the page (#477).
/// </summary>
/// <remarks>
///     Both read off one lot of rows by the view, the only thing that knows the room and the scroll, so the walk never
///     enters off a reclaim and a page worked out apart.
/// </remarks>
/// <param name="Reclaiming">
///     The first thing whose start is on the page (<see cref="Scroll.Reclaim" />), where the pick has none of its rows
///     on it, or <see langword="null" /> while it is still visible.
/// </param>
/// <param name="References">
///     The references of the thing reclaimed, or of the pick where nothing is, with a row on the page — in the order
///     they are drawn, so the first is the one <c>→</c> enters at and the last the one <c>←</c> does.
/// </param>
public readonly record struct OnThePage(int? Reclaiming, IReadOnlyList<Reference> References)
{
    /// <summary>Nothing known of the page: no reclaim, and a walk entered at the first reference or the last.</summary>
    public static OnThePage Unknown { get; } = new(null, []);
}
