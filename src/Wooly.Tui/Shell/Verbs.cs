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
    ///     and a key left off must still not act on a post off the page. <c>D clear all</c> is not here, though its
    ///     hint needs a list to show (<see cref="Screens.KeyHint.NeedsAPick" />): it clears every notification rather
    ///     than the picked one, so it acts at once. The attachments screen's <c>s</c> is. The two whose meaning turns
    ///     on the screen — <c>F</c>, <c>m</c> — are <see cref="Keymap.NeedsAPick" />'s.
    ///     <c>←</c> and <c>→</c> are not here: they reclaim and enter in the same press (#477).
    /// </remarks>
    public static bool NeedsAPick(this Verb verb) => verb is
        Verb.OpenPost or Verb.OpenAuthor or Verb.OpenReference or Verb.Reply or Verb.Edit
        or Verb.Boost or Verb.Favorite or Verb.Pin or Verb.Bookmark or Verb.Delete or Verb.Reveal or Verb.Toggle or Verb.Vote
        or Verb.Dismiss or Verb.OpenActor or Verb.OpenPostActors or Verb.StopSuggesting or Verb.OpenPerson
        or Verb.AcceptRequest or Verb.RejectRequest or Verb.OpenAsker or Verb.OpenConversation or Verb.OpenResult
        or Verb.ActAs or Verb.MakeDefault or Verb.RemoveProfile or Verb.SignInAgain
        or Verb.Describe or Verb.ToggleSensitive;

    /// <summary>
    ///     Whether <paramref name="verb" /> puts one of a post's marks on or takes it off, whichever it is not wearing
    ///     (ADR-0009) — so that a second of it undoes the first.
    /// </summary>
    public static bool IsAMark(this Verb verb) => verb is Verb.Boost or Verb.Favorite or Verb.Pin or Verb.Bookmark;

    /// <summary>
    ///     Whether <paramref name="verb" /> acts on the post <b>Picked</b> (<see cref="Screens.Screen.Picked" />), and so
    ///     on nothing where what is picked is no post: a mark or a reply.
    /// </summary>
    /// <remarks>
    ///     What settles whether a click on a post's <b>Counts</b> is its key there or only picks (#592): asked of what the
    ///     click picked rather than of which screen it was on, so that no screen has to say it draws posts it does not
    ///     pick.
    /// </remarks>
    public static bool ActsOnAPost(this Verb verb) => verb is Verb.Reply || verb.IsAMark();
}
