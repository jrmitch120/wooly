using HashtagRule = Wooly.Core.Timelines.Hashtag;

namespace Wooly.Core.Search;

/// <summary>
///     What is typed into search when it names one thing rather than asks after something, and so goes straight to
///     the thing it names instead of listing results (<c>CONTEXT.md</c>).
/// </summary>
/// <remarks>
///     A front end's rule rather than the search's: what is typed still searches for exactly what it says wherever a
///     caller asks it to, which is why the CLI's <c>search</c> never consults this.
/// </remarks>
public static class DirectQuery
{
    /// <summary>
    ///     The tag <paramref name="typed" /> names, where it is a <c>#</c> followed by one well-formed tag word — or
    ///     <see langword="null" /> where it is anything else, and is searched for as typed.
    /// </summary>
    /// <remarks>
    ///     Well-formed by the rule a tag timeline already takes a tag by (<see cref="HashtagRule" />), so that
    ///     what opens directly is exactly what could have been opened at all. Exactly one <c>#</c>: that rule would
    ///     take <c>##cats</c> for <c>cats</c>, and a reader who typed two meant something other than the tag.
    /// </remarks>
    public static string? Tag(string typed)
    {
        var text = typed.Trim();

        return text.Length > 1 && text[0] == '#' && text[1] != '#' && HashtagRule.IsWellFormed(text)
            ? HashtagRule.Bare(text)
            : null;
    }
}
