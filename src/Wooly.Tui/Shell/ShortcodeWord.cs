namespace Wooly.Tui.Shell;

/// <summary>
///     The <c>:</c>-word, which the instance's custom emoji are offered on (#528, #578): what follows its <c>:</c> is
///     the **shortcode** looked for. The twin of <see cref="MentionWord" />, which keeps its own rules.
/// </summary>
public static class ShortcodeWord
{
    /// <summary>The fewest letters after the <c>:</c> that make a word, so <c>:D</c> and <c>:P</c> open nothing.</summary>
    private const int Fewest = 2;

    /// <summary>
    ///     The <c>:</c>-word the caret at <paramref name="caret" /> is at the end of on <paramref name="line" />, or
    ///     <see langword="null" /> where it is not at the end of one. A <c>:</c> starts one only at the start of a line
    ///     or after whitespace, so <c>10:30</c>, <c>re:thing</c> and <c>http://</c> open nothing; a word runs over the
    ///     letters, digits and <c>_</c> a Mastodon shortcode is made of, so <c>:)</c> is none and a closing <c>:</c>
    ///     ends one; and it takes at least two of them.
    /// </summary>
    public static HangingWord? At(string line, int caret)
    {
        caret = Math.Clamp(caret, 0, line.Length);
        var start = caret;

        while (start > 0 && InAWord(line[start - 1]))
        {
            start--;
        }

        var sign = start - 1;

        if (caret - start < Fewest
            || sign < 0
            || line[sign] != ':'
            || (sign > 0 && !char.IsWhiteSpace(line[sign - 1])))
        {
            return null;
        }

        return new HangingWord(sign, line[start..caret]);
    }

    private static bool InAWord(char letter) => char.IsAsciiLetterOrDigit(letter) || letter == '_';
}
