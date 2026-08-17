namespace Wooly.Tui.Shell;

/// <summary>
///     A key whose meaning depends on which screen is on top. The contract allows exactly this — <c>d</c> dismisses a
///     notification and deletes a post — and makes the status row the thing that keeps it workable
///     (<c>docs/tui-shell.md</c>).
/// </summary>
/// <remarks>
///     Named for the key rather than for either of its meanings, because the key is the only thing the two have in
///     common. The window binds these and knows nothing about screens; <see cref="Shell.Press" /> is the one place a
///     key and a screen meet, so that the whole of the collision can be read in one table.
/// </remarks>
public enum ShellKey
{
    /// <summary><c>⏎</c>: read the picked post, open a search result, or read whoever is asking to follow.</summary>
    Enter,

    /// <summary><c>a</c>: the author of the picked post, or letting the picked follow request in.</summary>
    Author,

    /// <summary><c>d</c>: dismissing the picked notification, or taking down the picked post.</summary>
    Discard,

    /// <summary><c>x</c>: turning the picked follow request away, or showing what the picked post is hiding.</summary>
    Reject,
}
