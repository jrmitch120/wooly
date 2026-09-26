namespace Wooly.Tui.Shell;

/// <summary>What can be asked of a <see cref="Verb" /> rather than of the screen it was pressed on.</summary>
public static class Verbs
{
    /// <summary>
    ///     Whether <paramref name="verb" /> is one of the twelve that need a terminal, and so are <c>ShellWindow</c>'s
    ///     to carry out rather than the shell's or a screen's (<see cref="Verb" />).
    /// </summary>
    /// <remarks>
    ///     One list for the two places that have to agree on it: the window takes exactly these before it hands a verb
    ///     on, and <see cref="Shell.Do" /> answers each of them unused rather than passing it to a screen. Kept apart,
    ///     a window verb added to one and not the other would be a key the shell spent on nothing — its fallback hands
    ///     every verb it does not name to the screen (#232).
    /// </remarks>
    public static bool NeedsATerminal(this Verb verb) => verb is
        Verb.Quit or Verb.Send
        or Verb.NextPost or Verb.PreviousPost or Verb.FirstPost or Verb.LastPost
        or Verb.NextSection or Verb.PreviousSection
        or Verb.ScrollDown or Verb.ScrollUp or Verb.PageDown or Verb.PageUp;
}
