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

    /// <summary>Those keys, after whatever this screen calls moving the selection and before the way out of it.</summary>
    public static IReadOnlyList<KeyHint> Around(KeyHint moving, params KeyHint[] after) =>
        [moving, .. OnAPost, .. after, new KeyHint("?", "keys")];

    /// <summary>
    ///     Those keys with <paramref name="instead" /> in place of the one that shares its letter, for a screen where
    ///     one of them means something else — <c>d</c> dismisses a notification where it deletes a post
    ///     (<c>docs/tui-shell.md</c>). The key is still listed, so it is still announced; only what it does changes.
    /// </summary>
    public static IReadOnlyList<KeyHint> Saying(KeyHint instead) =>
        [.. OnAPost.Select(key => key.Key == instead.Key ? instead : key)];
}
