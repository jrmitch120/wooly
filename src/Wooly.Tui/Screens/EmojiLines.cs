using Wooly.Core;
using Wooly.Tui.Rendering;
using Wooly.Tui.Theme;

namespace Wooly.Tui.Screens;

/// <summary>
///     What a custom emoji says in the list a <c>:</c>-word hangs (#578): its shortcode as written, in the custom emoji
///     role, with the letters the word matched picked out as a mention row picks them out (<see cref="MentionLines" />).
///     The list round them is <see cref="PickLines" />'s.
/// </summary>
public static class EmojiLines
{
    /// <summary>One custom emoji, given what the <c>:</c>-word typed so far is: <c>:shortcode:</c>.</summary>
    public static IEnumerable<Span> Row(CustomEmoji emoji, string query)
    {
        var at = emoji.Shortcode.IndexOf(query, StringComparison.OrdinalIgnoreCase);

        return MentionLines.Lit(emoji.Written, at < 0 ? at : at + 1, query.Length, Role.CustomEmoji);
    }

    /// <summary>What a custom emoji's shortcode takes, colons and all.</summary>
    public static int Columns(CustomEmoji emoji) => Glyphs.Columns(emoji.Written);
}
