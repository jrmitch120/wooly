namespace Wooly.Core.Timelines;

/// <summary>
///     The rule that a hashtag is one word, in the one place every entry point can reach it. A tag goes into a request
///     path rather than a query value, so a value with a slash in it does not fetch a tag timeline at all — it fetches
///     whatever endpoint the slashes walk to, and the answer comes back rendered as posts. Living here rather than in
///     one command's argument validation is what stops the next way of reading a tag — the TUI, a saved search — from
///     having to remember the rule, or quietly not having it.
/// </summary>
public static class Hashtag
{
    /// <summary>
    ///     The tag as an instance wants it: no leading <c>#</c>, no surrounding whitespace. A user says "#cats" as
    ///     readily as "cats", and a shell that ate the <c>#</c> leaves a third spelling of the same tag.
    /// </summary>
    public static string Bare(string hashtag) => hashtag.Trim().TrimStart('#');

    /// <summary>Whether <paramref name="hashtag" /> is a tag this client can ask an instance for.</summary>
    /// <remarks>
    ///     Letters and digits are asked of <see cref="char.IsLetterOrDigit(char)" /> rather than of an ASCII range, so
    ///     that a tag written in Japanese or Greek is as readable a tag as one written in English. What that leaves out
    ///     is every character with a meaning in a URL — none of them are letters, digits or underscores — which is what
    ///     makes a tag safe to put in a path.
    /// </remarks>
    public static bool IsWellFormed(string? hashtag)
    {
        if (hashtag is null)
        {
            return false;
        }

        var bare = Bare(hashtag);

        return bare.Length > 0 && bare.All(character => char.IsLetterOrDigit(character) || character == '_');
    }

    /// <summary>
    ///     How a value that is not a hashtag is described, shared so that the command rejecting one before it is asked
    ///     for and the domain rejecting one that got past it cannot say different things about the same value.
    /// </summary>
    public static string Rejection(string hashtag) =>
        $"Give the hashtag as a single word — cats or #cats, not '{hashtag}'.";
}
