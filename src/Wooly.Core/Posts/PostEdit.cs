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
    public string? ContentWarning { get; init; }

    /// <summary>Whether this edit has anything to say about the content warning.</summary>
    public bool ChangesContentWarning => ContentWarning is not null;
}
