using Wooly.Core;
using Wooly.Tui.Media;
using Wooly.Tui.Rendering;
using Wooly.Tui.Theme;

namespace Wooly.Tui.Screens;

/// <summary>
///     What a custom emoji says in the list a <c>:</c>-word hangs (#578, #580): its picture in the two-column box every
///     custom emoji is drawn in, fitted whole and centred (ADR-0035), then a space, then its shortcode as written, in the
///     custom emoji role, with the letters the word matched picked out as a mention row picks them out
///     (<see cref="MentionLines" />). Where the <b>Raster</b> draws no pictures, or they are off, the shortcode alone.
///     The list round them is <see cref="PickLines" />'s.
/// </summary>
/// <remarks>
///     Which of the two is the Raster's alone, never whether the picture has landed, as everywhere a custom emoji is
///     drawn: the box is the Stand-in's shade until it lands, so a row takes the same room either way and nothing in
///     the list moves when a picture comes.
/// </remarks>
public static class EmojiLines
{
    /// <summary>The columns a row spends on its picture: the box, and the space after it.</summary>
    private const int Pictured = CustomEmojiBox.Columns + 1;

    /// <summary>
    ///     One custom emoji, given what the <c>:</c>-word typed so far is: its picture and <c>:shortcode:</c> where
    ///     <paramref name="pictures" /> and <paramref name="raster" /> draw one, and <c>:shortcode:</c> where they do not.
    /// </summary>
    public static Line Row(CustomEmoji emoji, string query, IPictures? pictures, Raster? raster)
    {
        var at = emoji.Shortcode.IndexOf(query, StringComparison.OrdinalIgnoreCase);
        var written = MentionLines.Lit(emoji.Written, at < 0 ? at : at + 1, query.Length, Role.CustomEmoji);

        if (!Drawing.Draws(pictures, raster, out _))
        {
            return new Line([.. written]);
        }

        var box = PictureBox.Fixed(
            Drawn.CustomEmoji(emoji),
            CustomEmojiBox.Columns,
            CustomEmojiBox.Rows,
            pictures,
            raster);

        return new Line([box.Cells(0) with { CustomEmoji = emoji }, new Span(" ", Role.Body), .. written])
        {
            Insets = box.Insets(0, 0),
        }.Wanting([box.Wanted!]);
    }

    /// <summary>What a row for <paramref name="emoji" /> takes: its shortcode, colons and all, and its picture where one is drawn.</summary>
    public static int Columns(CustomEmoji emoji, IPictures? pictures, Raster? raster) =>
        Glyphs.Columns(emoji.Written) + (Drawing.Draws(pictures, raster, out _) ? Pictured : 0);
}
