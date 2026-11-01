using Wooly.Core;

namespace Wooly.Tui.Rendering;

/// <summary>
///     A <b>custom emoji</b> written into some text as its <b>shortcode</b>, and where: one the text's owner lists,
///     since a <c>:word:</c> it does not list is the words someone wrote (ADR-0035).
/// </summary>
/// <param name="CustomEmoji">The custom emoji written.</param>
/// <param name="At">Where its opening colon is in the text.</param>
public readonly record struct Shortcode(CustomEmoji CustomEmoji, int At)
{
    /// <summary>Where it stops in the text, one past its closing colon.</summary>
    public int End => At + CustomEmoji.Written.Length;

    /// <summary>
    ///     The shortcodes of <paramref name="listed" /> written in <paramref name="text" />, left to right.
    /// </summary>
    /// <remarks>
    ///     Read as the instance's own web client reads them: each colon opens a shortcode that runs to the next, and is
    ///     one where what is between them is listed — so <c>:US_AZ:Phoenix</c> is an emoji and a word, and
    ///     <c>10:30:45</c> is a time, <c>30</c> being nobody's emoji. A colon that opened nothing listed may still
    ///     close a shortcode it opens: <c>:x:php:</c> is <c>:x</c> and <c>:php:</c>.
    /// </remarks>
    public static IReadOnlyList<Shortcode> In(string text, IReadOnlyList<CustomEmoji> listed)
    {
        if (listed.Count == 0)
        {
            return [];
        }

        // The first of a shortcode listed twice, which is the same name for what can only be one picture.
        var named = new Dictionary<string, CustomEmoji>(StringComparer.Ordinal);

        foreach (var customEmoji in listed)
        {
            named.TryAdd(customEmoji.Shortcode, customEmoji);
        }

        var lookup = named.GetAlternateLookup<ReadOnlySpan<char>>();
        var found = new List<Shortcode>();
        var at = text.IndexOf(':');

        while (at >= 0)
        {
            var end = text.IndexOf(':', at + 1);

            if (end < 0)
            {
                break;
            }

            if (lookup.TryGetValue(text.AsSpan(at + 1, end - at - 1), out var customEmoji))
            {
                found.Add(new Shortcode(customEmoji, at));
                at = text.IndexOf(':', end + 1);
            }
            else
            {
                at = end;
            }
        }

        return found;
    }
}
