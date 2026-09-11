namespace Wooly.Tui.Rendering;

/// <summary>
///     Somebody else's words with the marks taken out that this client and a terminal would count differently — so
///     that a row laid out to fill its width is a row drawn to fill its width, and no wider.
/// </summary>
/// <remarks>
///     A display name is written by somebody who has no idea a terminal has columns. The only such mark that costs
///     anything is the <em>variation selector</em>: <c>U+FE0F</c> after a character asks for the emoji form of it,
///     and <c>An Amazing Wizard➡️KICKSTARTER</c> — <c>U+27A1 U+FE0F</c> — is three numbers where a layout needs one.
///     The characters count thirty; Terminal.Gui packs the pair into a single cell and counts twenty-nine; the
///     terminal paints the emoji two columns wide and counts thirty. Every cell after it is painted a column right of
///     the one Terminal.Gui put it in, so the last cell of a full-width row crosses the right margin and the terminal
///     wraps it — which is how the <c>y</c> of <c>3y</c> came to be drawn on the rail, and an age to read as a count
///     with no unit (#206).
///     <para>
///         Dropped rather than measured, because measuring only settles what <em>this</em> client thinks: the cell
///         Terminal.Gui reserves is one cell whatever the layout believes, and nothing here can widen it. Without the
///         selector a terminal draws the plain form of the character, one column, which is the width both sides had
///         already counted.
///     </para>
///     <para>
///         The narrow half of the problem. A name written in a script whose characters are two columns wide is
///         counted wrong in the other direction, and none of this helps it (#207).
///     </para>
/// </remarks>
public static class Glyphs
{
    private const char FirstSelector = '\uFE00';

    private const char LastSelector = '\uFE0F';

    /// <summary>
    ///     <paramref name="text" /> as a terminal will draw it, which is the form every layout is measured against.
    /// </summary>
    /// <remarks>
    ///     The text itself where it carries no selector, which is nearly everything this client draws: the scan is
    ///     the whole cost on that path, and every row is laid out afresh on every frame.
    /// </remarks>
    public static string Plain(string text) =>
        text.AsSpan().IndexOfAnyInRange(FirstSelector, LastSelector) < 0
            ? text
            : new string([.. text.Where(character => character is < FirstSelector or > LastSelector)]);
}
