namespace Wooly.Core.Configuration;

/// <summary>What a <see cref="ThemeConfig" /> says about one role, as written.</summary>
/// <remarks>
///     Either half may be left unsaid, and most themes leave the background unsaid for every role but the selected
///     row: a role that sets its own background is a role told apart by the row it is on rather than by the text on
///     it, and every other one belongs on the theme's own page.
/// </remarks>
/// <param name="Foreground">The colour to draw it in, or <see langword="null" /> to keep the one being overridden.</param>
/// <param name="Background">The colour to draw it on, or <see langword="null" /> for the theme's own.</param>
public sealed record ThemeRole(string? Foreground, string? Background = null);
