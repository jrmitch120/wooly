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

    /// <summary>What is on offer for <paramref name="word" />, best first; nothing, which keeps the list closed.</summary>
    IReadOnlyList<HangingOffer> Matching(HangingWord word);
}
