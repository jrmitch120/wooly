namespace Wooly.Core.Configuration;

/// <summary>
///     A theme as the config file has it: a background, and a colour against each role the writer named (#46). Kept as
///     the names and the words the user wrote rather than as anything drawable — which roles exist, and what
///     <c>#12111a</c> or <c>bright-red</c> resolve to, are the TUI's to answer, and this layer draws nothing.
/// </summary>
/// <remarks>
///     So a theme naming a role nobody has heard of parses here and is refused there, where the vocabulary is. That
///     split is why a CLI command can rewrite this file without having to understand a theme in it.
/// </remarks>
public sealed record ThemeConfig
{
    /// <summary>
    ///     What the page is behind everything, or <see langword="null" /> where this theme does not say — in which
    ///     case the built-in it is read against supplies one. A background is a property of the theme rather than a
    ///     role of its own, because no view will ever ask to draw something in the background.
    /// </summary>
    public string? Background { get; init; }

    /// <summary>
    ///     What this theme puts against each role it names, keyed by the name written in the file. A theme names the
    ///     roles it wants to change and no others.
    /// </summary>
    public IReadOnlyDictionary<string, ThemeRole> Roles { get; init; } =
        new Dictionary<string, ThemeRole>(StringComparer.Ordinal);
}
