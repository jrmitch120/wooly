namespace Wooly.Core.Posts;

/// <summary>
///     What amounts to a content warning, and what amounts to none. One rule read by everything that composes one — the
///     TUI's field, the CLI's <c>--cw</c>, and <see cref="PostEdit.ContentWarningWanted" /> — rather than the same
///     expression written out once per surface (#146).
/// </summary>
/// <remarks>
///     Only the reading is shared. What "none" then looks like on the way out is the *thing being sent*'s to say and
///     stays where it is said: <see langword="null" /> on a <see cref="PostDraft" />, since an instance reads an empty
///     warning as no warning at all; the empty string on a <see cref="PostEdit" />, which keeps null for the third
///     state a field has no way to say (leave the warning alone).
/// </remarks>
public static class ContentWarnings
{
    /// <summary>
    ///     The warning <paramref name="said" /> puts a post behind, or <see langword="null" /> where what is there
    ///     amounts to no warning: nothing at all, or nothing but spaces. A warning made of spaces would hide a post
    ///     behind a blank, which is worse than either hiding it or not.
    /// </summary>
    /// <remarks>
    ///     Whitespace decides only between a warning and none; it is not tidied out of one there is. What the author
    ///     left in the field is what they wrote, and a client that trimmed it would be editing their words on the way
    ///     past.
    /// </remarks>
    public static string? Written(string? said) => string.IsNullOrWhiteSpace(said) ? null : said;
}
