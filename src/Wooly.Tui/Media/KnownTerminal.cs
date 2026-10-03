namespace Wooly.Tui.Media;

/// <summary>
///     Whether the terminal this runs in says, in its own environment, that it is one that draws the Kitty graphics
///     protocol's Unicode placeholders (ADR-0022).
/// </summary>
/// <remarks>
///     Asked of the environment because nothing else can be: the Kitty protocol's own query says whether a terminal
///     takes Kitty graphics, not whether it draws placeholders, and WezTerm takes them and prints its placeholders as
///     boxes (wezterm/wezterm#7924). So a terminal draws placeholders only where it names itself as one known to, and
///     draws through a box otherwise. Read before the first frame, so the first picture waits on nothing (#292).
/// </remarks>
internal static class KnownTerminal
{
    /// <summary>
    ///     Whether the variables <paramref name="variable" /> reads name a terminal that draws Kitty's placeholders:
    ///     Ghostty, or kitty itself.
    /// </summary>
    /// <remarks>
    ///     Never inside Windows Terminal, which draws sixel and not Kitty, nor WezTerm, whatever else their environment
    ///     carries — a <c>TERM</c> passed on from somewhere else included. Nor inside tmux or screen, which inherit the
    ///     variables of the terminal they were started in but do not pass the protocol through.
    /// </remarks>
    /// <param name="variable">An environment variable's value by name, or <see langword="null" /> where it is unset.</param>
    public static bool DrawsPlaceholders(Func<string, string?> variable)
    {
        if (Set("WT_SESSION") || Set("WEZTERM_PANE") || Set("TMUX") || Set("STY")
            || variable("TERM_PROGRAM") is "WezTerm" or "WarpTerminal")
        {
            return false;
        }

        return variable("TERM_PROGRAM") is "ghostty"
            || variable("TERM") is "xterm-ghostty" or "xterm-kitty"
            || Set("KITTY_WINDOW_ID")
            || Set("GHOSTTY_RESOURCES_DIR");

        bool Set(string name) => !string.IsNullOrEmpty(variable(name));
    }
}
