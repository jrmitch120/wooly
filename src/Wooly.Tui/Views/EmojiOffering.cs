using Wooly.Core;
using Wooly.Tui.Screens;
using Wooly.Tui.Shell;

namespace Wooly.Tui.Views;

/// <summary>
///     The instance's custom emoji, offered on a <c>:</c>-word (#528, #578): those whose shortcode holds what follows the
///     <c>:</c>, drawn as their shortcode (<see cref="EmojiLines" />), and picking one writes its shortcode and a space.
/// </summary>
/// <remarks>
///     Matched on the shortcode alone, ignoring case. Those starting with the query come first, then those only holding
///     it; within each, the shorter first, then alphabetically, ignoring case. A shortcode typed in full is the shortest
///     that starts with itself, so it stays, first. As many as the people to mention at most.
///     <para>
///         The instance is asked the first time a word wants its emoji, and on a word once at most (#579): a read that
///         answered nothing is asked again only on a new word, so a down instance is not asked on every keystroke. What
///         it answers after the keystroke that asked is offered from the next one, never by a redraw on its own.
///     </para>
/// </remarks>
internal sealed class EmojiOffering(Drafting.Drafting drafting) : IHangingOffering
{
    public HangingWord? At(string line, int caret) => ShortcodeWord.At(line, caret);

    public IReadOnlyList<HangingOffer> Matching(HangingWord word, bool typed, bool newWord)
    {
        if (newWord)
        {
            drafting.AskForEmoji();
        }

        return Ranked(drafting.EmojiToWrite(typed), word);
    }

    private static IReadOnlyList<HangingOffer> Ranked(IReadOnlyList<CustomEmoji> offered, HangingWord word) =>
    [
        .. offered.Where(emoji => emoji.Shortcode.Contains(word.Query, StringComparison.OrdinalIgnoreCase))
                  .OrderBy(emoji => emoji.Shortcode.StartsWith(word.Query, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                  .ThenBy(emoji => emoji.Shortcode.Length)
                  .ThenBy(emoji => emoji.Shortcode, StringComparer.OrdinalIgnoreCase)
                  .Take(PeopleToMention.Most)
                  .Select(emoji => Offer(emoji, word.Query)),
    ];

    private static HangingOffer Offer(CustomEmoji emoji, string query) => new(
        _ => EmojiLines.Row(emoji, query),
        EmojiLines.Columns(emoji),
        () => $"{emoji.Written} ");
}
