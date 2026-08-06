using Wooly.Core.Posts;
using Wooly.Tui.Media;
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
    ///     The post <c>⏎</c> opens, which is the picked one everywhere but the post screen: the post that screen is
    ///     about is already on it, so opening it again would push a second copy of the screen the reader is standing on
    ///     (#48).
    /// </summary>
    /// <remarks>
    ///     Told apart from <see cref="Picked" /> rather than folded into it, because every other key — boost,
    ///     favorite, reply, the author — still means the post being read. It is only drilling that has nowhere to go.
    /// </remarks>
    public virtual Post? Opens => Picked;

    /// <summary>
    ///     Whether this screen is taking what is typed, which is only ever a search prompt taking a query. A fact
    ///     about the screen rather than a mode the window keeps, so that the keys which act on a post cannot fire
    ///     while somebody is writing the word <c>backfeed</c>.
    /// </summary>
    public virtual bool IsTyping => false;

    /// <summary>The rows to draw, at <paramref name="width" /> columns.</summary>
    /// <param name="width">How wide the content region is — 61 at an 80-column terminal.</param>
    /// <param name="now">What to measure timestamps against.</param>
    /// <param name="pictures">
    ///     What this terminal can draw and which attachments' pixels have arrived, or <see langword="null" /> for a
    ///     screen being laid out with no terminal in the room — which is every test, and which reads as every
    ///     attachment being linked rather than drawn.
    ///     <para>
    ///         A screen needs this while it is working out its rows rather than while they are being painted, because
    ///         it changes what the rows are: a picture's own proportions settle how many rows its box takes, and an
    ///         attachment on a terminal that draws nothing becomes a link and a description instead (ADR-0016).
    ///     </para>
    /// </param>
    public abstract IReadOnlyList<Line> Lines(int width, DateTimeOffset now, IPictures? pictures = null);

    /// <summary>
    ///     The things on this screen with one of them picked out, or <see langword="null" /> where there is nothing on
    ///     it to walk — the compose editor, the keymap, a notice.
    /// </summary>
    /// <remarks>
    ///     The one member a screen exposes for the sake of <see cref="Move" /> and <see cref="Pick" />, which is what
    ///     lets those two be the same two lines everywhere rather than an override apiece that could number its rows
    ///     one way and its picks another (#51).
    /// </remarks>
    protected virtual IPicked? Walking => null;

    /// <summary>Moves what is picked out by <paramref name="by" /> items, stopping at either end.</summary>
    public void Move(int by) => Walking?.Move(by);

    /// <summary>
    ///     Picks the <paramref name="at" />th thing on this screen out, stopping at either end. What <c>j</c> does
    ///     when the arrows have scrolled what is picked off the page: a step from where the pick was left would
    ///     take the reader back to a post they can no longer see (#51).
    /// </summary>
    /// <remarks>
    ///     The ordinal is the one this screen's rows are named with (<see cref="Line.Item" />), and it is the same
    ///     ordinal by construction: the rows are stamped by whatever <see cref="Walking" /> is, from the index it is
    ///     keeping.
    /// </remarks>
    public void Pick(int at) => Walking?.Pick(at);

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
