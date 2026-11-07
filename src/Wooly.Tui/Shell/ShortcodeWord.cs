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
    ///     <see langword="null" /> where it is not at the end of one: a word it is <see cref="In" /> that takes at least
    ///     two letters before the caret, so <c>:)</c> and <c>:D</c> open nothing, and none after it, so the caret moved
    ///     back inside <c>:blobcat</c> is at the end of no word.
    /// </summary>
    public static HangingWord? At(string line, int caret)
    {
        caret = Math.Clamp(caret, 0, line.Length);

        if (In(line, caret) is not { } sign
            || caret - sign - 1 < Fewest
            || (caret < line.Length && InAWord(line[caret])))
        {
            return null;
        }

        return new HangingWord(sign, line[(sign + 1)..caret]);
    }

    /// <summary>
    ///     Where the <c>:</c> is of the word the caret at <paramref name="caret" /> is in on <paramref name="line" />,
    ///     however few of its letters are before the caret, or <see langword="null" /> where it is in none. A <c>:</c>
    ///     starts one only at the start of a line or after whitespace, so <c>10:30</c>, <c>re:thing</c> and
    ///     <c>http://</c> are none; and a word runs over the letters, digits and <c>_</c> a Mastodon shortcode is made
    ///     of, so a closing <c>:</c> ends one.
    /// </summary>
    public static int? In(string line, int caret)
    {
        caret = Math.Clamp(caret, 0, line.Length);
        var sign = HangingWord.RunBack(line, caret, InAWord) - 1;

        return sign >= 0 && line[sign] == ':' && HangingWord.StartsAWord(line, sign) ? sign : null;
    }

    private static bool InAWord(char letter) => char.IsAsciiLetterOrDigit(letter) || letter == '_';
}
