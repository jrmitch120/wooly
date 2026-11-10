using Wooly.Tui.Rendering;

namespace Wooly.Tui.Screens;

/// <summary>
///     What a run on the rows of a post or a notification group stands for where a click on it means more than a pick
///     (<see cref="Span.Item" />): the one place these are handed out, each its own number below nought (#592).
/// </summary>
/// <remarks>
///     Below nought because a notification group's avatar strip stands each face for its slot, from nought up
///     (<see cref="AvatarStrip" />), and the post a group carries is drawn by <see cref="PostLines" /> on the same rows.
///     Numbered by their order here rather than one by one, so that a part added is a number no other part has —
///     written down beside each drawing, the names line's and the <b>Counts</b>' were kept apart by a comment alone.
/// </remarks>
public enum RunPart
{
    /// <summary>A notification group's names and the kind's sentence on its names line, which open its actors (#456).</summary>
    Names,

    /// <summary>The boost run of a post's <b>Counts</b>, which is <c>b</c> (#593).</summary>
    Boost,

    /// <summary>The favorite run of a post's <b>Counts</b>, which is <c>f</c> (#593).</summary>
    Favorite,

    /// <summary>The reply run of a post's <b>Counts</b>, which is <c>r</c> (#594).</summary>
    Reply,
}

/// <summary>Between a <see cref="RunPart" /> and the number a run carries for it.</summary>
public static class RunParts
{
    /// <summary>The number a run stands for <paramref name="part" /> by (<see cref="Span.Item" />), below nought.</summary>
    public static int Item(this RunPart part) => -1 - (int)part;

    /// <summary>
    ///     Which part a run carrying <paramref name="item" /> stands for, or <see langword="null" /> where it stands for
    ///     none of them: nothing, or a face of an avatar strip.
    /// </summary>
    public static RunPart? Of(int? item) => item is < 0 and { } below ? (RunPart)(-1 - below) : null;
}
