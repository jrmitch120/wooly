namespace Wooly.Tui.Shell;

/// <summary>
///     The @-word, which the people to mention are offered on (#318): what follows its <c>@</c> is who is looked for.
/// </summary>
public static class MentionWord
{
    /// <summary>
    ///     The @-word the caret at <paramref name="caret" /> is at the end of on <paramref name="line" />, or
    ///     <see langword="null" /> where it is not at the end of one. An <c>@</c> starts one only at the start of a line
    ///     or after whitespace, so the <c>@</c> inside <c>name@example.com</c> opens nothing; and a word runs over
    ///     letters, digits, <c>_</c>, <c>.</c>, <c>-</c> and <c>@</c>, so a full address is one word.
    /// </summary>
    public static HangingWord? At(string line, int caret)
    {
        caret = Math.Clamp(caret, 0, line.Length);
        var start = caret;

        while (start > 0 && InAWord(line[start - 1]))
        {
            start--;
        }

        if (start >= caret || line[start] != '@' || (start > 0 && !char.IsWhiteSpace(line[start - 1])))
        {
            return null;
        }

        return new HangingWord(start, line[(start + 1)..caret]);
    }

    private static bool InAWord(char letter) => char.IsLetterOrDigit(letter) || letter is '_' or '.' or '-' or '@';
}
