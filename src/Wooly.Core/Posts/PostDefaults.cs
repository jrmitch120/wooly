namespace Wooly.Core.Posts;

/// <summary>
///     What a post goes out at where its author says nothing about it: a visibility and a language, which travel
///     together. An account's own settings on its instance say one pair (#339), the config file's preferences another,
///     and a compose screen starts on whichever of them speaks first (ADR-0024). Either half may be unknown — the
///     instance did not answer, answered with something this client has no word for, or nobody set it.
/// </summary>
/// <param name="Visibility">The visibility, or <see langword="null" /> where it is not known.</param>
/// <param name="Language">
///     The language as a code <see cref="PostLanguageName" /> lists, or <see langword="null" /> where it is not known or
///     not set.
/// </param>
public sealed record PostDefaults(PostVisibility? Visibility, string? Language)
{
    /// <summary>Nothing known about either.</summary>
    public static readonly PostDefaults Unknown = new(null, null);

    /// <summary>These, with each half that is not known taken from <paramref name="fallback" />.</summary>
    public PostDefaults Or(PostDefaults fallback) =>
        new(Visibility ?? fallback.Visibility, Language ?? fallback.Language);
}
