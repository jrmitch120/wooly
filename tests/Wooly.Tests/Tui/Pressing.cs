using Wooly.Tui.Shell;

namespace Wooly.Tests.Tui;

/// <summary>
///     Pressing a key at a shell with no terminal in the room: what <c>ShellWindow</c> does once it has translated the
///     press, which is to ask <see cref="Keymap" /> what the key means here and hand the verb over.
/// </summary>
/// <remarks>
///     A composition of two production functions rather than a second dispatch — so a test that presses <c>⏎</c> on
///     the follow requests screen is still asserting the collision the contract allows, and not a path only tests
///     take. What it leaves out is the handful of verbs the window keeps for itself, which need a page and a laid-out
///     <c>ShellWindow</c>: those are asserted through a real window in <see cref="ShellKeyTests" />.
/// </remarks>
internal static class Pressing
{
    /// <inheritdoc cref="Pressing" />
    /// <returns>Whether the press was used, which is <see cref="Shell.Do" />'s own answer.</returns>
    public static bool Press(this Shell shell, ShellKey key) =>
        shell.Do(Keymap.Means(key, shell.Screen), Keymap.Answer(key));
}
