namespace Wooly.Tui.Screens;

/// <summary>
///     The keys that act on whichever post is picked out. Listed once, because the shell acts on
///     <see cref="Screen.Picked" /> without caring which screen it came from — so a screen whose status row left one
///     of these out would be a screen where a key fires unannounced.
/// </summary>
public static class PostKeys
{
    /// <summary>What every screen with posts on it answers to, in the order the status row reads best.</summary>
    public static IReadOnlyList<KeyHint> OnAPost { get; } =
    [
        new("⏎", "read"),
        new("a", "author"),
        new("c", "compose"),
        new("r", "reply"),
        new("b", "boost"),
        new("f", "favorite"),
        new("p", "pin"),
        new("e", "edit"),
        new("d", "delete"),
        new("x", "show warning"),
    ];

    /// <summary>
    ///     Walking the screen a row at a time, which every screen with rows on it answers to and none of them owns.
    ///     Said beside whatever the screen calls walking its own things, because the two are one movement split in
    ///     half and a status row naming only one of them would be the half that cannot reach the foot of a tall post
    ///     (#51).
    /// </summary>
    public static KeyHint Scrolling { get; } = new("↓/↑", "row");

    /// <summary>Those keys, after whatever this screen calls moving the selection and before the way out of it.</summary>
    public static IReadOnlyList<KeyHint> Around(KeyHint moving, params KeyHint[] after) =>
        Around(moving, [], after);

    /// <summary>
    ///     The same, with the keys this screen alone answers to in front of the shared ones — and standing in for any
    ///     of them they share a letter with, so that <c>d</c> is announced as dismiss where it dismisses.
    /// </summary>
    /// <remarks>
    ///     In front rather than behind, because the status row is one row and a list longer than it is cut off at the
    ///     right (<c>docs/tui-shell.md</c>). The keys a reader can find on no other screen are the ones that have to
    ///     survive the cut; the ones that mean the same thing everywhere can be learned somewhere else.
    /// </remarks>
    /// <param name="moving">What this screen calls moving the selection.</param>
    /// <param name="its">The keys this screen alone answers to.</param>
    /// <param name="after">The way out of it, and anything else that belongs at the end.</param>
    public static IReadOnlyList<KeyHint> Around(KeyHint moving, IReadOnlyList<KeyHint> its, params KeyHint[] after)
    {
        var taken = its.Select(key => key.Key).ToHashSet(StringComparer.Ordinal);

        return
        [
            moving,
            Scrolling,
            .. its,
            .. OnAPost.Where(key => !taken.Contains(key.Key)),
            .. after,
            new KeyHint("?", "keys"),
        ];
    }
}
