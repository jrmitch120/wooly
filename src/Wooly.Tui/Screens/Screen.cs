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
    /// <summary>
    ///     Which reference inside the picked post the walk has got to, as an index into <see cref="References" /> —
    ///     before it is checked against what is written there now, which <see cref="Reference" /> is.
    /// </summary>
    private int? _reference;

    /// <summary>What this screen is called on the breadcrumb, e.g. <c>post by @ben</c>.</summary>
    public abstract string Crumb { get; }

    /// <summary>
    ///     The keys this screen answers to, for the status row and for <c>?</c> — which while a reference is picked
    ///     out are the three that act on it, ahead of the screen's own (<c>docs/tui-shell.md</c>, #83).
    /// </summary>
    /// <remarks>
    ///     Said here rather than by each screen, because a reference is picked the same way on all of them and a
    ///     screen that forgot the swap would be a screen where <c>←</c> and <c>→</c> fire unannounced.
    /// </remarks>
    public IReadOnlyList<KeyHint> Keys => Reference is null ? OwnKeys : PostKeys.OnAReference(OwnKeys);

    /// <summary>The keys this screen alone settles, which is every key that does not act on a picked reference.</summary>
    protected abstract IReadOnlyList<KeyHint> OwnKeys { get; }

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
    /// <param name="hideDrawnCaption">
    ///     The reader's <c>hide_drawn_caption</c> preference: whether a picture's caption hides once it is actually
    ///     drawn (#71). Ignored by a screen with no posts on it.
    /// </param>
    public abstract IReadOnlyList<Line> Lines(
        int width,
        DateTimeOffset now,
        IPictures? pictures = null,
        bool hideDrawnCaption = false);

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

    /// <summary>
    ///     The post whose text <c>←</c> and <c>→</c> walk the references of — the picked one on every screen but the
    ///     conversations list, where a row is a conversation and the post drawn on it is its last message (#83).
    /// </summary>
    protected virtual Post? Referencing => Picked;

    /// <summary>
    ///     The references inside that post, in the order they were written — which is the order they are walked in,
    ///     and the order an index into them means anything in.
    /// </summary>
    /// <remarks>
    ///     None at all while the post's text is still behind a content warning: the brackets a picked reference is
    ///     drawn in would be behind it too, so a pick there is one nobody can see (<c>docs/tui-shell.md</c>).
    /// </remarks>
    public IReadOnlyList<Reference> References => Referencing is { } post && Readable(post)
        ? BodyText.References((post.Boosted ?? post).Content)
        : [];

    /// <summary>
    ///     The one the reader has walked to, or <see langword="null" /> where none is — including where the post has
    ///     since been edited out from under the walk, which is a pick on nothing rather than a pick on whatever is
    ///     written in that place now.
    /// </summary>
    public Reference? Reference
    {
        get
        {
            var references = References;

            return _reference is { } at && at < references.Count ? references[at] : null;
        }
    }

    /// <summary>
    ///     Walks the references by <paramref name="by" />: <c>→</c> enters at the first and <c>←</c> at the last, and
    ///     further motion in the same direction at either end clamps rather than wrapping, which is the convention
    ///     <see cref="Picked{T}" /> already walks a list by.
    /// </summary>
    /// <returns>
    ///     Whether there was anything to walk, which is what settles whether the key was used — a screen with no
    ///     references on it leaves <c>←</c> and <c>→</c> to whatever else wants them, the compose editor above all.
    /// </returns>
    public bool WalkReference(int by)
    {
        var references = References;

        if (references.Count == 0)
        {
            return false;
        }

        _reference = _reference is { } at && at < references.Count
            ? Math.Clamp(at + by, 0, references.Count - 1)
            : by > 0 ? 0 : references.Count - 1;

        return true;
    }

    /// <summary>Lets the picked reference go, which <c>esc</c> does before it pops and <c>j</c> and <c>k</c> do on the way past.</summary>
    /// <returns>Whether there was one, which is what settles whether <c>esc</c> was spent on it.</returns>
    public bool ClearReference()
    {
        var had = Reference is not null;

        _reference = null;

        return had;
    }

    /// <summary>
    ///     Which reference is picked out on the <paramref name="at" />th thing on this screen — none, unless it is the
    ///     thing picked out, since a reference pick lives inside the picked post and nowhere else.
    /// </summary>
    protected Reference? ReferenceOn(int at) => Walking?.At == at ? Reference : null;

    /// <summary>Moves what is picked out by <paramref name="by" /> items, stopping at either end.</summary>
    /// <remarks>
    ///     The picked reference goes with it: the reader has left the post it was inside (<c>docs/tui-shell.md</c>).
    /// </remarks>
    public void Move(int by)
    {
        ClearReference();
        Walking?.Move(by);
    }

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
    public void Pick(int at)
    {
        ClearReference();
        Walking?.Pick(at);
    }

    /// <summary>The content warnings the reader has asked past on this screen, by the id of the post each is on.</summary>
    /// <remarks>
    ///     Held here rather than six times over, because what <c>x</c> does turned out not to vary by screen at all —
    ///     it is <see cref="Picked" /> and one question. Kept out of <see cref="Picked{T}" /> for the opposite reason:
    ///     only posts carry a warning, and a list of conversations or of accounts would be holding it for nothing.
    /// </remarks>
    protected Revealed Revealed { get; } = new();

    /// <summary>Shows what the picked post's content warning is hiding.</summary>
    /// <returns>Whether there was anything to reveal, which is what settles whether the key was used.</returns>
    /// <remarks>
    ///     A screen with no posts on it picks none, so it reveals nothing without having to say so — the same reason
    ///     <see cref="Move" /> and <see cref="Pick" /> need no override on one.
    /// </remarks>
    public bool Reveal() => Picked is { } picked && Revealed.Ask(picked);

    /// <summary>Whether <paramref name="post" />'s own text is on screen rather than behind its content warning.</summary>
    private bool Readable(Post post) => (post.Boosted ?? post).ContentWarning is null || Revealed.Has(post);

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
