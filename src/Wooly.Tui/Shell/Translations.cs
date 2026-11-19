using Wooly.Core.Posts;

namespace Wooly.Tui.Shell;

/// <summary>
///     Every <b>Translation</b> an instance has answered this session, by the instance, the post and the language asked
///     for — so that <c>t</c> on a post translated before, on this screen or another, shows it again without asking
///     again (#615, ADR-0037).
/// </summary>
/// <remarks>
///     The shell's rather than a screen's: which posts are <em>showing</em> translated is each screen's own and lapses
///     with it (<see cref="Screens.Translated" />), but what the instance said is true for as long as the post says
///     what it said. So each translation is held with the words it was made from, and handed back only for a post
///     that still says them: an edit made anywhere — here, which <see cref="Forget" /> answers at once, or elsewhere,
///     which a refresh brings in with no <b>Change</b> to say so — is a miss, and asked anew. Not kept past the session.
/// </remarks>
internal sealed class Translations
{
    /// <summary>Between one part of a post's words and the next, where no post's words can have it.</summary>
    private const char Between = '\u001f';

    private readonly Dictionary<string, Dictionary<(string PostId, string? Target), Kept>> _known =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    ///     What the instance answered for <paramref name="post" /> into <paramref name="target" />, or
    ///     <see langword="null" /> where it has not been asked this session — or was asked about words the post no
    ///     longer says.
    /// </summary>
    public Translation? Known(string instance, Post post, string? target)
    {
        var shown = post.Boosted ?? post;

        return _known.GetValueOrDefault(instance)?.GetValueOrDefault((shown.Id, target)) is { } kept
               && kept.Words == WordsOf(shown)
            ? kept.Translation
            : null;
    }

    /// <summary>Holds <paramref name="translation" /> of <paramref name="post" />, as it reads now, for the session.</summary>
    public void Keep(string instance, Post post, string? target, Translation translation)
    {
        var shown = post.Boosted ?? post;

        if (!_known.TryGetValue(instance, out var known))
        {
            _known[instance] = known = [];
        }

        known[(shown.Id, target)] = new Kept(translation, WordsOf(shown));
    }

    /// <summary>
    ///     Lets go of every translation of the post <paramref name="postId" /> names on <paramref name="instance" />,
    ///     whatever language it was asked into — the post having been edited, so that none of them says what it says now.
    /// </summary>
    public void Forget(string instance, string postId)
    {
        if (_known.GetValueOrDefault(instance) is not { } known)
        {
            return;
        }

        foreach (var key in known.Keys.Where(key => key.PostId == postId).ToList())
        {
            known.Remove(key);
        }
    }

    /// <summary>
    ///     Everything of <paramref name="post" /> an instance translates, as one value: its text, warning, poll options
    ///     and attachments' descriptions — what an edit can change under a translation.
    /// </summary>
    private static string WordsOf(Post post) => string.Join(
        Between,
        [
            post.Content,
            post.ContentWarning ?? "",
            .. post.Poll?.Options.Select(option => option.Text) ?? [],
            .. post.Media.Select(attached => $"{attached.Id}{Between}{attached.Description}"),
        ]);

    /// <summary>A translation, and the words of the post it was made from.</summary>
    private sealed record Kept(Translation Translation, string Words);
}
