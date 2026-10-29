namespace Wooly.Core.Configuration;

/// <summary>
///     How the TUI's <b>Scrollbar</b> draws its thumb, or that it draws none (#505): the config file's <c>scrollbar</c>,
///     spelled as <see cref="ScrollbarStyleName" /> spells it. Only the thumb's glyph differs between the styles that draw
///     one; the track, the colours, the thumb's length and place, where it answers the mouse and its drag are the same.
/// </summary>
public enum ScrollbarStyle
{
    /// <summary><c>█</c>, the whole cell: the default, and what an absent key means.</summary>
    Block,

    /// <summary><c>┃</c>, a heavy line on the edge's <c>│</c>, told from the track by weight where there is no colour.</summary>
    Line,

    /// <summary><c>▌</c>, the cell's left half, toward the screen.</summary>
    LeftHalf,

    /// <summary><c>▐</c>, the cell's right half, toward the terminal's edge.</summary>
    RightHalf,

    /// <summary>No scrollbar: the right edge drawn and answering as it was before there was one (#501).</summary>
    None,
}
