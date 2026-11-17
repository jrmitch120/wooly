namespace Wooly.Core.Posts;

/// <summary>
///     What an instance's translator can do: each language a post can be written in, with the languages it can take a
///     post in it to — Mastodon's <c>GET /api/v1/instance/translation_languages</c>, read once per instance (ADR-0037).
///     What settles whether a <b>Translation</b> is offered, so that a reader is never offered one the instance would
///     refuse.
/// </summary>
public sealed class TranslationLanguages
{
    /// <summary>Mastodon's code for a post that says nothing of its language.</summary>
    public const string Undetermined = "und";

    /// <summary>
    ///     The map with nothing in it: an instance with no translator, or one older than 4.2 without the endpoint.
    ///     Takes nothing anywhere.
    /// </summary>
    public static TranslationLanguages None { get; } = new(new Dictionary<string, IReadOnlyList<string>>());

    /// <param name="targets">Each language a post can be in, with the languages it can be taken to.</param>
    public TranslationLanguages(IReadOnlyDictionary<string, IReadOnlyList<string>> targets) => Targets = targets;

    /// <summary>
    ///     Each language a post can be in, under the code the instance gave it — <see cref="Undetermined" /> for a post
    ///     that says none — with the codes of the languages it can be taken to, region codes as the instance wrote them.
    /// </summary>
    public IReadOnlyDictionary<string, IReadOnlyList<string>> Targets { get; }

    /// <summary>
    ///     Whether a post written in <paramref name="from" /> can be taken to <paramref name="to" /> — compared by base
    ///     language, so that <c>en-GB</c> answers for <c>en</c> — and never where the two are the same language, which an
    ///     instance refuses. A post with no language is asked about as <see cref="Undetermined" />.
    /// </summary>
    /// <param name="from">The post's language, or <see langword="null" /> where it says none.</param>
    /// <param name="to">
    ///     The language wanted, or <see langword="null" /> where none is known — when the instance chooses, which no API
    ///     says, and so the only question left is whether it can translate at all.
    /// </param>
    public bool Takes(string? from, string? to)
    {
        if (Targets.Count == 0)
        {
            return false;
        }

        if (to is null)
        {
            return true;
        }

        var source = Base(from ?? Undetermined);
        var target = Base(to);

        return source != target
               && Targets.Any(pair =>
                   Base(pair.Key) == source && pair.Value.Any(language => Base(language) == target));
    }

    /// <summary>A language's code without its region or script: <c>en</c> out of <c>en-GB</c>, lower-cased.</summary>
    private static string Base(string code)
    {
        var cut = code.IndexOfAny(['-', '_']);

        return (cut < 0 ? code : code[..cut]).ToLowerInvariant();
    }
}
