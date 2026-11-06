namespace Wooly.Tui.Shell;

/// <summary>
///     The word the caret sits at the end of that a hanging list opens on (#318, #576), in the text as written rather
///     than as wrapped: where on its line its sign is, and what follows the sign up to the caret. What a word is — which
///     sign starts one, and what it runs over — is the kind of word's own, as <see cref="MentionWord" /> is the
///     <c>@</c>-word's; walking back from the caret, and where a sign may start one, are the same for every kind.
/// </summary>
/// <param name="Start">The column of its sign, on the line as written.</param>
/// <param name="Query">What follows the sign up to the caret, which is what the things on offer are matched on.</param>
public sealed record HangingWord(int Start, string Query)
{
    /// <summary>The most things a hanging list offers on a word, whatever kind of word it is, so it never buries the text.</summary>
    public const int MostOffered = 5;

    /// <summary>The columns the word takes before the caret: its one-letter sign and the query after it.</summary>
    public int Length => Query.Length + 1;

    /// <summary>
    ///     Where the run of letters <paramref name="inAWord" /> takes that ends at <paramref name="caret" /> on
    ///     <paramref name="line" /> starts: at the caret itself where the letter before it is none of them.
    /// </summary>
    internal static int RunBack(string line, int caret, Func<char, bool> inAWord)
    {
        var start = caret;

        while (start > 0 && inAWord(line[start - 1]))
        {
            start--;
        }

        return start;
    }

    /// <summary>
    ///     Whether a sign at <paramref name="sign" /> on <paramref name="line" /> may start a word: only at the start of
    ///     the line or after whitespace, so one inside a word, as in <c>name@example.com</c> or <c>10:30</c>, starts none.
    /// </summary>
    internal static bool StartsAWord(string line, int sign) => sign == 0 || char.IsWhiteSpace(line[sign - 1]);
}
