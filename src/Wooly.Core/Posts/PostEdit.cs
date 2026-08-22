namespace Wooly.Core.Posts;

/// <summary>
///     A change to a post that is already published. Deliberately smaller than <see cref="PostDraft" />, because most of
///     what a draft says cannot be changed afterwards: who can see a published post, and what it replies to, are settled
///     the moment it goes out.
///     <para>
///         Everything this does not mention is left as it was — see <see cref="IPostAuthor.Edit" />, where that promise
///         is kept.
///     </para>
/// </summary>
public sealed record PostEdit
{
    /// <summary>The text the post should now have.</summary>
    public required string Text { get; init; }

    /// <summary>
    ///     What to put the post behind, distinguishing three things the one field has to say: <see langword="null" />
    ///     leaves whatever warning the post already had, an empty string takes it away, and any other text replaces it.
    ///     Silence has to mean "leave it" rather than "take it away", because a warning removed by an edit that never
    ///     mentioned it would show a reader what they had asked not to be shown.
    /// </summary>
    /// <remarks>
    ///     Silence is the CLI's to say, and only the CLI's: <c>--cw</c> can be absent from a command line.
    ///     The TUI edits behind a field pre-filled from the post being changed, and an author looking at that row has
    ///     no "said nothing about it" left to say — so it fills this in every time (#140).
    /// </remarks>
    public string? ContentWarning { get; init; }

    /// <summary>Whether this edit has anything to say about the content warning.</summary>
    public bool ChangesContentWarning => ContentWarning is not null;

    /// <summary>
    ///     The warning the post should end up behind, for an edit that changes it at all: empty where the author asked
    ///     for none. Whitespace amounts to none, the same as it does when a post is first composed — a warning made of
    ///     spaces would hide a post behind nothing, which is worse than either hiding it or not.
    /// </summary>
    public string ContentWarningWanted =>
        string.IsNullOrWhiteSpace(ContentWarning) ? string.Empty : ContentWarning;
}
