using Wooly.Tui.Rendering;
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
    ///     Told that the caret has come into a word of this kind, as soon as it has — its sign alone, before it has the
    ///     letters to open on (#528): a word the caret stays in — typed on, taken back below what opens it, or gone
    ///     inside and back to its end — is told once. Nothing is done with it by default.
    /// </summary>
    void Entering()
    {
    }

    /// <summary>
    ///     Told, before anything is offered, that what came in since may be offered now: on a keystroke in the editor
    ///     that follows the word — an edit or a caret move — and on a redraw while the list stands open on this same
    ///     word <see cref="Awaiting" /> it, but never on any other redraw on a change of the shell's, so the list never
    ///     jumps open by itself. Nothing is done with it by default.
    /// </summary>
    void TakeUp()
    {
    }

    /// <summary>
    ///     The row the list stands open on, nothing to pick, where nothing is <see cref="Matching" /> the word because
    ///     what would match it is still on its way — or <see langword="null" />, which keeps the list closed. Nothing is
    ///     ever on its way by default.
    /// </summary>
    Line? Awaiting() => null;

    /// <summary>What is on offer for <paramref name="word" />, best first; nothing, which keeps the list closed.</summary>
    /// <param name="word">The word the caret is at the end of.</param>
    IReadOnlyList<HangingOffer> Matching(HangingWord word);
}
