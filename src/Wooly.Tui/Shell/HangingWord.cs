namespace Wooly.Tui.Shell;

/// <summary>
///     The word the caret sits at the end of that a hanging list opens on (#318, #576), in the text as written rather
///     than as wrapped: where on its line its sign is, and what follows the sign up to the caret. What a word is — which
///     sign starts one, where, and what it runs over — is the kind of word's own, as <see cref="MentionWord" /> is the
///     <c>@</c>-word's.
/// </summary>
/// <param name="Start">The column of its sign, on the line as written.</param>
/// <param name="Query">What follows the sign up to the caret, which is what the things on offer are matched on.</param>
public sealed record HangingWord(int Start, string Query)
{
    /// <summary>The columns the word takes before the caret: its one-letter sign and the query after it.</summary>
    public int Length => Query.Length + 1;
}
