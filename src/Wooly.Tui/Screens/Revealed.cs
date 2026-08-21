using Wooly.Core.Posts;

namespace Wooly.Tui.Screens;

/// <summary>
///     The posts a reader has asked past the warning on, by the id of each. What <c>x</c> writes to and what drawing a
///     post reads.
/// </summary>
/// <remarks>
///     Its own small thing rather than a set written out inside each screen, because six screens honour a warning — a
///     feed, a thread, an account, an inbox, a page of search results, a conversation — and what asking past one means
///     does not vary between them. A boost is asked about by the post inside it, since that is what the warning
///     belongs to.
///     <para>
///         One of these per <see cref="Screen" />, so a reveal belongs to the screen it was made on and lasts exactly
///         as long as that screen is on the stack — the same lifetime <see cref="Screen.Began" /> has for the page a
///         screen is showing (#133), and ended by the same three things: <c>esc</c>, a refresh, an arrival. Drilling
///         into a post asked past in the feed asks again, the post screen being a new screen that has been asked
///         nothing; walking back out to the feed does not, a pop handing back the very screen the reveal was made on.
///         Wanted rather than a gap — a warning is a request to be asked before being shown, and honouring it once is
///         not consent to skip the asking everywhere afterwards (#121, <c>docs/tui-shell.md</c>).
///     </para>
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
