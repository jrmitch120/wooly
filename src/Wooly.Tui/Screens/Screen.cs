using Wooly.Core.Posts;
using Wooly.Tui.Rendering;

namespace Wooly.Tui.Screens;

/// <summary>
///     One place in the stack. Entering a screen pushes, <c>esc</c> pops, and the breadcrumb is the stack
///     (<c>docs/tui-shell.md</c>) — so a screen is somewhere you <em>go</em> rather than a window over what you were
///     reading.
/// </summary>
/// <remarks>
///     A screen holds its own state and says what it draws, and nothing more: it reaches no port and knows about no
///     instance. What a keypress means is the shell's, because the shell is what has the ports — which also means
///     every screen here can be drawn, moved around and asserted on with no terminal and no network.
/// </remarks>
public abstract class Screen
{
    /// <summary>What this screen is called on the breadcrumb, e.g. <c>post by @ben</c>.</summary>
    public abstract string Crumb { get; }

    /// <summary>The keys this screen answers to, for the status row and for <c>?</c>.</summary>
    public abstract IReadOnlyList<KeyHint> Keys { get; }

    /// <summary>
    ///     The post the reader has picked out, or <see langword="null" /> where this screen has no posts on it. What
    ///     <c>⏎</c>, <c>a</c> and the marks act on.
    /// </summary>
    public virtual Post? Picked => null;

    /// <summary>
    ///     Whether this screen is taking what is typed, which is only ever a search prompt taking a query. A fact
    ///     about the screen rather than a mode the window keeps, so that the keys which act on a post cannot fire
    ///     while somebody is writing the word <c>backfeed</c>.
    /// </summary>
    public virtual bool IsTyping => false;

    /// <summary>The rows to draw, at <paramref name="width" /> columns.</summary>
    /// <param name="width">How wide the content region is — 61 at an 80-column terminal.</param>
    /// <param name="now">What to measure timestamps against.</param>
    public abstract IReadOnlyList<Line> Lines(int width, DateTimeOffset now);

    /// <summary>Moves what is picked out by <paramref name="by" /> items, stopping at either end.</summary>
    public virtual void Move(int by)
    {
    }

    /// <summary>Shows what the picked post's content warning is hiding.</summary>
    /// <returns>Whether there was anything to reveal, which is what settles whether the key was used.</returns>
    public virtual bool Reveal() => false;

    /// <summary>
    ///     Puts <paramref name="post" /> in place of the copy this screen is holding, after a mark changed it. What
    ///     stops a star lighting up only once the whole timeline has been fetched again.
    /// </summary>
    public virtual void Replace(Post post)
    {
    }

    /// <summary>Takes the post <paramref name="postId" /> names off this screen, after it was deleted.</summary>
    public virtual void Remove(string postId)
    {
    }
}
