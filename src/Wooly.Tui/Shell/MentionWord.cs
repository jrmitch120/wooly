namespace Wooly.Tui.Shell;

/// <summary>
///     The @-word the caret sits at the end of, in the text as written rather than as wrapped (#318): where on its line
///     it starts, and what follows its <c>@</c>.
/// </summary>
/// <param name="Start">The column of its <c>@</c>, on the line as written.</param>
/// <param name="Query">What follows the <c>@</c> up to the caret, which is what the people to mention are matched on.</param>
public sealed record MentionWord(int Start, string Query)
{
    /// <summary>
    ///     The @-word the caret at <paramref name="caret" /> is at the end of on <paramref name="line" />, or
    ///     <see langword="null" /> where it is not at the end of one. An <c>@</c> starts one only at the start of a line
    ///     or after whitespace, so the <c>@</c> inside <c>name@example.com</c> opens nothing; and a word runs over
    ///     letters, digits, <c>_</c>, <c>.</c>, <c>-</c> and <c>@</c>, so a full address is one word.
    /// </summary>
    public static MentionWord? At(string line, int caret)
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

        return new MentionWord(start, line[(start + 1)..caret]);
    }

    private static bool InAWord(char letter) => char.IsLetterOrDigit(letter) || letter is '_' or '.' or '-' or '@';
}
