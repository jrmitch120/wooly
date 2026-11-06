using Wooly.Tui.Shell;

namespace Wooly.Tui.Views;

/// <summary>
///     What a <see cref="HangingList" /> offers for one kind of word (#576): the word it opens on and the things that
///     match it. Everything about being the list — its keys, the pointer, <c>esc</c>'s memory, where it hangs and the
///     one undoable replacement — is the list's; this is only what is offered. People for an <c>@</c>-word are one
///     (<see cref="MentionOffering" />).
/// </summary>
internal interface IHangingOffering
{
    /// <summary>
    ///     The word of this kind the caret at <paramref name="caret" /> is at the end of on <paramref name="line" />, as
    ///     written, or <see langword="null" /> where it is at the end of none.
    /// </summary>
    HangingWord? At(string line, int caret);

    /// <summary>
    ///     Where the sign starts of the word of this kind the caret at <paramref name="caret" /> is in on
    ///     <paramref name="line" />, as written — at its end, inside it, or in one too short yet to open on — or
    ///     <see langword="null" /> where it is in none. The caret leaving it is what ends <c>esc</c>'s memory of a word,
    ///     and makes the next one new. By default, a word is only ever left where the caret is at the end of none.
    /// </summary>
    int? In(string line, int caret) => At(line, caret)?.Start;

    /// <summary>
    ///     Told, before anything is offered on it, that the list is about to offer on a word of this kind for the first
    ///     time since the caret came into it: a word the caret stays in — typed on, taken back below what opens it, or
    ///     gone inside and back to its end — is told once. Nothing is done with it by default.
    /// </summary>
    void Entering()
    {
    }

    /// <summary>
    ///     Told, before anything is offered, that a keystroke in the editor follows the word — an edit or a caret move —
    ///     rather than a redraw on a change of the shell's. Nothing is done with it by default.
    /// </summary>
    void Typed()
    {
    }

    /// <summary>What is on offer for <paramref name="word" />, best first; nothing, which keeps the list closed.</summary>
    /// <param name="word">The word the caret is at the end of.</param>
    IReadOnlyList<HangingOffer> Matching(HangingWord word);
}
