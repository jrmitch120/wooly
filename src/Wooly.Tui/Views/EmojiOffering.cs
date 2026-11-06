using Wooly.Core;
using Wooly.Tui.Screens;
using Wooly.Tui.Shell;

namespace Wooly.Tui.Views;

/// <summary>
///     The instance's custom emoji, offered on a <c>:</c>-word (#528, #578): those whose shortcode holds what follows the
///     <c>:</c>, drawn as their picture and their shortcode (<see cref="EmojiLines" />, #580), and picking one writes its
///     shortcode and a space.
/// </summary>
/// <remarks>
///     Matched on the shortcode alone, ignoring case. Those starting with the query come first, then those only holding
///     it; within each, the shorter first, then alphabetically, ignoring case. A shortcode typed in full is the shortest
///     that starts with itself, so it stays, first. As many as a hanging list offers at most
///     (<see cref="HangingWord.MostOffered" />).
///     <para>
///         The instance is asked the first time a word wants its emoji, and on a word once at most (#579): a read that
///         answered nothing is asked again only on a new word, so a down instance is not asked on every keystroke. What
///         it answers after the keystroke that asked is offered from the next one, never by a redraw on its own.
///     </para>
///     <para>
///         The pictures are the content panel's (<paramref name="pictures" />), from the one cache, so an emoji already
///         drawn in a post is not fetched again. Whether a row has room for one is the panel's <b>Raster</b>'s to say,
///         as the list is offered and as it is drawn alike.
///     </para>
/// </remarks>
internal sealed class EmojiOffering(Drafting.Drafting drafting, PanelPictures pictures) : IHangingOffering
{
    public HangingWord? At(string line, int caret) => ShortcodeWord.At(line, caret);

    public int? In(string line, int caret) => ShortcodeWord.In(line, caret);

    public IReadOnlyList<HangingOffer> Matching(HangingWord word, bool typed, bool newWord)
    {
        if (newWord)
        {
            drafting.AskForEmoji();
        }

        return Ranked(drafting.EmojiToWrite(typed), word);
    }

    private IReadOnlyList<HangingOffer> Ranked(IReadOnlyList<CustomEmoji> offered, HangingWord word) =>
    [
        .. offered.Where(emoji => emoji.Shortcode.Contains(word.Query, StringComparison.OrdinalIgnoreCase))
                  .OrderBy(emoji => emoji.Shortcode.StartsWith(word.Query, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                  .ThenBy(emoji => emoji.Shortcode.Length)
                  .ThenBy(emoji => emoji.Shortcode, StringComparer.OrdinalIgnoreCase)
                  .Take(HangingWord.MostOffered)
                  .Select(emoji => Offer(emoji, word.Query)),
    ];

    private HangingOffer Offer(CustomEmoji emoji, string query) => new(
        _ => EmojiLines.Row(emoji, query, pictures.Pictures, pictures.Raster),
        EmojiLines.Columns(emoji, pictures.Pictures, pictures.Raster),
        () => $"{emoji.Written} ");
}
