namespace Wooly.Core.Posts;

/// <summary>
///     What an account's own settings on its instance say a post goes out at where the author says nothing (#339): the
///     default visibility and posting language set in the account's preferences. Either may be unknown — the instance
///     did not answer, or answered with something this client has no word for.
/// </summary>
/// <param name="Visibility">The account's default visibility, or <see langword="null" /> where it is not known.</param>
/// <param name="Language">
///     The account's posting language as a code <see cref="PostLanguageName" /> lists, or <see langword="null" /> where
///     it is not known or not set.
/// </param>
public sealed record PostDefaults(PostVisibility? Visibility, string? Language)
{
    /// <summary>Nothing known about either.</summary>
    public static readonly PostDefaults Unknown = new(null, null);
}
