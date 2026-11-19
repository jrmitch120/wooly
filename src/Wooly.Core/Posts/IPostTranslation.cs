using Wooly.Core.Profiles;

namespace Wooly.Core.Posts;

/// <summary>
///     Asks a reader's instance for a post's <b>Translation</b> — the instance being the only translator there is
///     (ADR-0037). The narrow port ADR-0005 asks for, shared by both front ends: the CLI's <c>post translate</c> and the
///     TUI's <c>t</c> ask through this, and their tests fake this rather than the network. A port of its own rather than
///     a corner of <see cref="IPostEngagement" />, so that one fake tells the whole of translation: both the translation
///     and the languages map that says where one would be given (#616).
/// </summary>
/// <remarks>
///     Raw over <see cref="HttpClient" /> through <c>RawMastodonCall</c>, Mastonet 3.1.3 having no translation at all
///     (ADR-0036's precedent).
/// </remarks>
public interface IPostTranslation
{
    /// <summary>
    ///     The post <paramref name="postId" /> names, translated by its instance into <paramref name="target" />, or into
    ///     whatever the instance chooses — the account's web-interface language — where no target is known.
    /// </summary>
    /// <param name="postId">
    ///     The post whose words are wanted. A boost carries none of its own, so a caller holding one asks for the boosted
    ///     post's id rather than the wrapper's.
    /// </param>
    /// <param name="target">
    ///     The code of the language wanted (<see cref="PostLanguageName" />), or <see langword="null" /> to send none. The
    ///     instance may still answer in another — it honours only its own interface locales — and the answer's
    ///     <see cref="Translation.To" /> says which it used.
    /// </param>
    /// <exception cref="Errors.TranslationRefusedException">
    ///     The instance would not translate it — a post only followers or the people it mentions can see, a pair it
    ///     cannot translate (its own language among them), or no translator at all — in the instance's own words.
    /// </exception>
    /// <exception cref="Errors.InstanceFailedException">
    ///     The instance's translator failed to answer — rate-limited, out of quota or down — with what the instance said.
    /// </exception>
    /// <exception cref="Errors.RateLimitedException">The profile has spent its quota with the instance.</exception>
    Task<Translation> Translate(
        ActiveProfile profile,
        string postId,
        string? target,
        CancellationToken cancellationToken);

    /// <summary>
    ///     The instance's languages map: which languages its translator can take a post in each language to (#616). What
    ///     the TUI asks once per instance, to offer <c>t</c> only where it would be given; the CLI never asks it.
    /// </summary>
    /// <returns>
    ///     The map, or <see cref="TranslationLanguages.None" /> where the instance has no translator — or, older than 4.2,
    ///     no such endpoint, which is the same thing to a reader.
    /// </returns>
    /// <exception cref="Errors.InstanceFailedException">The instance failed to answer.</exception>
    /// <exception cref="Errors.RateLimitedException">The profile has spent its quota with the instance.</exception>
    Task<TranslationLanguages> Languages(ActiveProfile profile, CancellationToken cancellationToken);
}
