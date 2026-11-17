using System.Text.Json.Serialization;
using Wooly.Core.Posts;

namespace Wooly.Cli.Output;

/// <summary>
///     Where a translated post's words came from, as <c>post translate --json</c> spells it beside the post's own fields
///     (#614): the language the instance took the post to be written in, the one it translated into, and who
///     translated it. Its presence is what tells a script a translation from the original.
/// </summary>
/// <param name="From">The code of the language the translator detected, or absent where it did not say.</param>
/// <param name="To">The code of the language the instance actually translated into, which may not be the one asked for.</param>
/// <param name="Provider">The translator, as the instance named it.</param>
internal sealed record TranslationDocument(
    [property: JsonPropertyName("from")] string? From,
    [property: JsonPropertyName("to")] string To,
    [property: JsonPropertyName("provider")] string Provider)
{
    /// <summary>How <paramref name="translation" />'s provenance is written down.</summary>
    public static TranslationDocument Of(Translation translation) =>
        new(translation.From, translation.To, translation.Provider);
}
