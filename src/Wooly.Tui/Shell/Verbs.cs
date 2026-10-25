namespace Wooly.Tui.Shell;

/// <summary>What can be asked of a <see cref="Verb" /> rather than of the screen it was pressed on.</summary>
public static class Verbs
{
    /// <summary>
    ///     Whether <paramref name="verb" /> is one of the eighteen that need a terminal, and so are
    ///     <c>ShellWindow</c>'s to carry out rather than the shell's or a screen's (<see cref="Verb" />).
    /// </summary>
    /// <remarks>
    ///     One list for the two places that have to agree on it: the window takes exactly these before it hands a verb
    ///     on, and <see cref="Shell.Do" /> answers each of them unused rather than passing it to a screen. Kept apart,
    ///     a window verb added to one and not the other would be a key the shell spent on nothing — its fallback hands
    ///     every verb it does not name to the screen (#232).
    /// </remarks>
    public static bool NeedsATerminal(this Verb verb) => verb is
        Verb.Quit or Verb.Send
        or Verb.NextPost or Verb.PreviousPost or Verb.ToTheTop or Verb.ToTheFoot
        or Verb.NextEntry or Verb.PreviousEntry or Verb.FirstEntry or Verb.LastEntry
        or Verb.NextSection or Verb.PreviousSection
        or Verb.ScrollDown or Verb.ScrollUp or Verb.PageDown or Verb.PageUp
        or Verb.Narrower or Verb.Wider;

    /// <summary>
    ///     Whether <paramref name="verb" /> needs something <b>picked</b> to act on, whatever is on the status row — and
    ///     so, with the pick scrolled off the page, only reclaims on its first press, so that nothing lands on a thing
    ///     the reader cannot see (#476). Which of the screen's things it would act on is not asked: a key on a post with
    ///     nothing to refuse it, or on a thing with no post at all, reclaims as surely as one the row offers.
    /// </summary>
    /// <remarks>
    ///     A property of the verb rather than of the hint the row draws for it, because the row leaves off a key that
    ///     would be refused — <c>d</c>, <c>p</c> and <c>e</c> on somebody else's post, <c>x</c> with nothing hidden —
    ///     and a key left off must still not act on a post off the page. <c>D clear all</c> is here too, needing a list to
    ///     pick from as <see cref="Screens.KeyHint.NeedsAPick" /> says, and so is the attachments screen's <c>s</c>.
    ///     The two whose meaning turns on the screen — <c>F</c>, <c>m</c> — are <see cref="Keymap.NeedsAPick" />'s.
    ///     <c>←</c> and <c>→</c> are not here: they reclaim and enter in the same press (#477).
    /// </remarks>
    public static bool NeedsAPick(this Verb verb) => verb is
        Verb.OpenPost or Verb.OpenAuthor or Verb.OpenReference or Verb.Reply or Verb.Edit
        or Verb.Boost or Verb.Favorite or Verb.Pin or Verb.Delete or Verb.Reveal or Verb.Toggle or Verb.Vote
        or Verb.Dismiss or Verb.ClearAll or Verb.OpenActor or Verb.StopSuggesting or Verb.OpenPerson
        or Verb.AcceptRequest or Verb.RejectRequest or Verb.OpenAsker or Verb.OpenConversation or Verb.OpenResult
        or Verb.ActAs or Verb.MakeDefault or Verb.RemoveProfile or Verb.SignInAgain
        or Verb.Describe or Verb.ToggleSensitive;
}
