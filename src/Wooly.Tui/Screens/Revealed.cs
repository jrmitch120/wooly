using Wooly.Core.Posts;

namespace Wooly.Tui.Screens;

/// <summary>
///     The posts a reader has asked past the warning on, by the id of each. What <c>x</c> writes to and what drawing a
///     post reads.
/// </summary>
/// <remarks>
///     Its own small thing rather than a set inside one screen, because three screens honour a warning — a feed, an
///     inbox, a page of search results — and a warning asked past on one of them is a decision about that post, not
///     about the list it happened to be read in. A boost is asked about by the post inside it, since that is what the
///     warning belongs to.
/// </remarks>
public sealed class Revealed
{
    private readonly HashSet<string> _asked = [];

    /// <summary>Asks to see what <paramref name="post" /> is hiding — its warned text, its attachments, or both.</summary>
    /// <remarks>
    ///     <see cref="Post.IsWarned" /> rather than a warning to print, since #113: a post marked sensitive with
    ///     nothing written over it hides its attachments and nothing else, and a key that refused there would leave the
    ///     commonest sensitive post on Mastodon with nothing to press.
    /// </remarks>
    /// <returns>Whether there was anything to reveal, which is what settles whether the key was used.</returns>
    public bool Ask(Post post)
    {
        var shown = post.Boosted ?? post;

        return shown.IsWarned && _asked.Add(shown.Id);
    }

    /// <summary>Whether the reader has asked to see past <paramref name="post" />'s warning.</summary>
    public bool Has(Post post) => _asked.Contains((post.Boosted ?? post).Id);
}
