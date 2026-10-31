namespace Wooly.Tui.Rendering;

/// <summary>
///     A picture a row carries ahead of a keypress (<see cref="Line.Ahead" />): the inset it would be drawn through, and
///     how many rows under the row carrying it that inset would be set — the row a box sets its picture on being its
///     top only where the picture is the box's own shape (<see cref="PictureBox.Insets" />).
/// </summary>
/// <remarks>
///     Said rather than taken for nought, because the rows the keypress would put the picture on are not drawn yet, so
///     there is no row of them to carry it; and a sixel's cut is encoded for where the box meets the page's edges, which
///     the row it is set on decides.
/// </remarks>
/// <param name="Inset">The picture and the box of cells it would be drawn in.</param>
/// <param name="Below">How many rows under the row carrying it the inset would be set.</param>
public sealed record InsetAhead(Inset Inset, int Below)
{
    /// <summary>
    ///     This picture moved <paramref name="by" /> columns to the right, as <see cref="Inset.ShiftedBy" />.
    /// </summary>
    public InsetAhead ShiftedBy(int by) => this with { Inset = Inset.ShiftedBy(by) };
}
