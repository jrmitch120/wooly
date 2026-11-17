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
///     what it said — until it is edited, which <see cref="Forget" /> answers. Not kept past the session.
/// </remarks>
internal sealed class Translations
{
    private readonly Dictionary<(string Instance, string PostId, string? Target), Translation> _known = [];

    /// <summary>
    ///     What the instance answered for <paramref name="postId" /> into <paramref name="target" />, or
    ///     <see langword="null" /> where it has not been asked this session.
    /// </summary>
    public Translation? Known(string instance, string postId, string? target) =>
        _known.GetValueOrDefault((instance.ToLowerInvariant(), postId, target));

    /// <summary>Holds <paramref name="translation" /> for the rest of the session.</summary>
    public void Keep(string instance, string postId, string? target, Translation translation) =>
        _known[(instance.ToLowerInvariant(), postId, target)] = translation;

    /// <summary>
    ///     Lets go of every translation of the post <paramref name="postId" /> names on <paramref name="instance" />,
    ///     whatever language it was asked into — the post having been edited, so that none of them says what it says now.
    /// </summary>
    public void Forget(string instance, string postId)
    {
        var key = instance.ToLowerInvariant();

        foreach (var known in _known.Keys.Where(known => known.Instance == key && known.PostId == postId).ToList())
        {
            _known.Remove(known);
        }
    }
}
