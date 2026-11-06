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
///     that starts with itself, so it stays, first. As many as the people to mention at most.
///     <para>
///         The pictures are the content panel's (<paramref name="pictures" />), from the one cache, so an emoji already
///         drawn in a post is not fetched again. Whether a row has room for one is the panel's <b>Raster</b>'s to say,
///         as the list is offered and as it is drawn alike.
///     </para>
/// </remarks>
internal sealed class EmojiOffering(Drafting.Drafting drafting, OverlayPictures pictures) : IHangingOffering
{
    public HangingWord? At(string line, int caret) => ShortcodeWord.At(line, caret);

    public IReadOnlyList<HangingOffer> Matching(HangingWord word) =>
    [
        .. drafting.EmojiToWrite()
                   .Where(emoji => emoji.Shortcode.Contains(word.Query, StringComparison.OrdinalIgnoreCase))
                   .OrderBy(emoji => emoji.Shortcode.StartsWith(word.Query, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                   .ThenBy(emoji => emoji.Shortcode.Length)
                   .ThenBy(emoji => emoji.Shortcode, StringComparer.OrdinalIgnoreCase)
                   .Take(PeopleToMention.Most)
                   .Select(emoji => Offer(emoji, word.Query)),
    ];

    private HangingOffer Offer(CustomEmoji emoji, string query) => new(
        _ => EmojiLines.Row(emoji, query, pictures.Pictures, pictures.Raster),
        EmojiLines.Columns(emoji, pictures.Pictures, pictures.Raster),
        () => $"{emoji.Written} ");
}
