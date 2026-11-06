using Wooly.Tui.Rendering;
using Wooly.Tui.Screens;
using Wooly.Tui.Shell;

namespace Wooly.Tui.Views;

/// <summary>
///     The people to mention, offered on an @-word (#318): who matches what follows its <c>@</c>, drawn as mention rows
///     (<see cref="MentionLines" />), and picking one writes their mention, as the profile acted as writes it, and a
///     space.
/// </summary>
internal sealed class MentionOffering(Shell.Shell shell) : IHangingOffering
{
    public HangingWord? At(string line, int caret) => MentionWord.At(line, caret);

    public IReadOnlyList<HangingOffer> Matching(HangingWord word, bool typed, bool newWord) =>
    [
        .. shell.PeopleMatching(word.Query).Select(person => new HangingOffer(
            picked => new Line([.. MentionLines.Row(person, picked, word.Query)]),
            MentionLines.Columns(person),
            () => $"{shell.MentionOf(person)} ")),
    ];
}
