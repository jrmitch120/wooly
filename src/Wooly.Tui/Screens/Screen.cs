using Wooly.Core.Posts;
using Wooly.Tui.Rendering;
using Wooly.Tui.Theme;

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

    /// <summary>
    ///     Which of the picked post's poll options have been toggled and not yet cast, as indices into its answers.
    /// </summary>
    /// <remarks>
    ///     One set on the screen rather than one per post, for the reason a reference pick is one index: it belongs to
    ///     the post being read, and walking off that post is what discards it. Unlike <see cref="Revealed" />, which
    ///     survives the pick moving off the post and back onto it, a toggle is a half-finished sentence — and a vote
    ///     nobody meant to cast is exactly what confirming one is for (#87). Both are the screen's and go with it, but
    ///     only one of them survives <c>j</c>.
    /// </remarks>
    private readonly HashSet<int> _chosen = [];

    /// <summary>What this screen is called on the breadcrumb, e.g. <c>post by @ben</c>.</summary>
    public abstract string Crumb { get; }

    /// <summary>
    ///     The keys this screen answers to, for the status row and for <c>?</c> — which while a reference is picked
    ///     out are the three that act on it, ahead of the screen's own (<c>docs/tui-shell.md</c>, #83), and on a post
    ///     carrying a poll are the two that vote in it (#87).
    /// </summary>
    /// <remarks>
    ///     Said here rather than by each screen, because a reference is picked the same way on all of them and a
    ///     screen that forgot the swap would be a screen where <c>←</c> and <c>→</c> fire unannounced.
    ///     <para>
    ///         A reference wins where both apply: it is the level the reader is standing on, and the poll keys are
    ///         still there when they let it go. The poll keys are announced only where there is a poll to vote in, for
    ///         the rule <see cref="PostKeys" /> states in the other direction — a key that acts on nothing here must
    ///         not be on the row.
    ///     </para>
    /// </remarks>
    public IReadOnlyList<KeyHint> Keys => Reference is not null
        ? PostKeys.OnAReference(OwnKeys)
        : Poll is { TakesAVote: true } ? PostKeys.OnAPoll(OwnKeys) : OwnKeys;

    /// <summary>The keys this screen alone settles, which is every key that does not act on a picked reference.</summary>
    protected abstract IReadOnlyList<KeyHint> OwnKeys { get; }

    /// <summary>
    ///     Asking for what is there now, on the nine screens that have anything to ask again — screen-local, and shown
    ///     on the status row only where it applies (<c>docs/tui-shell.md</c>, #84).
    /// </summary>
    /// <remarks>
    ///     Said once here rather than written out on each of them, so that the key and the words explaining it cannot
    ///     come to differ by screen.
    /// </remarks>
    public static KeyHint Refreshing { get; } = new("g", "refresh");

    /// <summary>
    ///     Whether <c>g</c> means anything here: whether this screen can be asked for a fresher copy of what it is
    ///     showing. The nine the contract names, and nothing else — a live conversation and a live search are each
    ///     their own question and are deliberately left out (#84).
    /// </summary>
    /// <remarks>
    ///     A screen saying so owes <see cref="Refreshing" /> on its status row, and one that does not owes its absence:
    ///     a key announced and then refused reads as a shell that missed the press. The two are asserted against each
    ///     other rather than derived from one another, since each is what a different reader — the person and the
    ///     shell — goes looking for.
    /// </remarks>
    public virtual bool Refreshes => false;

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
    ///     Whether this screen is taking what is typed: a search prompt taking a query, or a compose screen's content
    ///     warning while <c>ctrl-w</c> has it (#123). A fact about the screen rather than a mode the window keeps, so
    ///     that the keys which act on a post cannot fire while somebody is writing the word <c>backfeed</c>.
    /// </summary>
    /// <remarks>
    ///     Never a post's own text, which is typed into the editor widget laid over the screen rather than through the
    ///     shell — a real terminal editor with a caret and a word wrap, and the one thing on any screen the shell does
    ///     not carry the letters of.
    /// </remarks>
    public virtual bool IsTyping => false;

    /// <summary>
    ///     Puts a letter into whatever this screen is taking, where it is taking anything. Said here rather than
    ///     matched on the screen's type where the key arrives, so that a third screen that takes letters is one
    ///     override rather than another arm in two cascades.
    /// </summary>
    public virtual void Type(char letter)
    {
    }

    /// <summary>Takes the last letter back out of it.</summary>
    public virtual void Backspace()
    {
    }

    /// <summary>
    ///     <paramref name="typed" /> with its last letter taken off, or unchanged where there is none to take. A
    ///     backspace at the start of an empty field is nothing at all rather than an exception or a reach into
    ///     whatever is behind it.
    /// </summary>
    protected static string Backspaced(string typed) => typed.Length > 0 ? typed[..^1] : typed;

    /// <summary>The rows to draw, in the room and under the conditions <paramref name="drawing" /> names.</summary>
    /// <remarks>
    ///     One parameter rather than four, because none of what a screen is told here is about the screen: the room,
    ///     the moment, what the terminal can paint and what the reader asked for are the shell's to know and the
    ///     screen's to pass on. A new one of those facts is a field on <see cref="Drawing" /> rather than a signature
    ///     edit at eleven overrides and a threading change through <see cref="PostList" /> (#148).
    /// </remarks>
    /// <param name="drawing">What this screen is being drawn in and under.</param>
    public abstract IReadOnlyList<Line> Lines(Drawing drawing);

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
    ///     The references inside that post, in the order they were written, followed by its attachments' own — which
    ///     is the order they are walked in, and the order an index into them means anything in.
    /// </summary>
    /// <remarks>
    ///     Nothing that is not on screen, on either half. The text ones go while the post's text is behind a content
    ///     warning, because the brackets a picked reference is drawn in would be behind it too and a pick there is one
    ///     nobody can see (<c>docs/tui-shell.md</c>). The attachment ones go on the same reasoning as soon as the post
    ///     is warned at all: since #113 the attachments are behind the warning with the text, so <c>←</c>/<c>→</c>
    ///     would be walking to a label nobody can see and <c>⏎</c> opening a video the reader never asked for. That
    ///     replaces the exemption this remark used to make on ADR-0017's behalf, which held only while their box and
    ///     description stood outside the warning — as they did until #113 put them behind it (ADR-0016's amendment).
    ///     <para>
    ///         Both halves asked of <see cref="OnShow" /> rather than worked out here, because they are the same two
    ///         questions <see cref="PostLines" /> puts about the same post: what is walked is what was drawn, and the
    ///         two answering separately is what let a walk reach inside something the reader was never shown (#145).
    ///     </para>
    /// </remarks>
    public IReadOnlyList<Reference> References
    {
        get
        {
            if (Referencing is not { } post)
            {
                return [];
            }

            var show = Showing(post);

            return
            [
                .. show.Words ? BodyText.References(show.Shown.Content) : [],
                .. show.Media ? BeyondTheText(show.Shown) : [],
            ];
        }
    }

    /// <summary>
    ///     The references a post carries beyond its own text: its attachments' addresses in the order they were
    ///     attached, and then its link preview's (ADR-0018).
    /// </summary>
    /// <remarks>
    ///     Shown and hidden together, which is why they are one question here: the link preview stands behind a post's
    ///     warning on exactly the terms its attachments do since #113, and asking twice would be two places for that to
    ///     be answered differently.
    /// </remarks>
    private static IEnumerable<Reference> BeyondTheText(Post post)
    {
        foreach (var attached in AttachmentReferences.Of(post))
        {
            yield return attached;
        }

        if (LinkPreviewReference.Of(post) is { } link)
        {
            yield return link;
        }
    }

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
    ///     Who the picked mention names, in full — <c>maria@fosstodon.org</c> for a <c>@maria</c> — or
    ///     <see langword="null" /> where nothing is picked, where what is picked is not a mention, or where the post
    ///     names nobody by that handle (#85).
    /// </summary>
    /// <remarks>
    ///     Answered here rather than by the shell because this is where the post the walk is inside is known: a
    ///     mention is only somebody in particular because the post it was written in says so
    ///     (<see cref="PostMentions" />), and a boost's handles are the boosted post's along with its text.
    /// </remarks>
    public string? Mentioned => Reference is { Role: Role.Mention } picked && Referencing is { } post
        ? PostMentions.Named(post, picked.Text)
        : null;

    /// <summary>
    ///     The handle the picked mention is written back out as: in full where the post says who it is, and as it was
    ///     written where it does not — or <see langword="null" /> where what is picked is not a mention. What <c>c</c>
    ///     addresses a fresh compose to, since a reader who walked to a name meant to write to them either way (#85).
    /// </summary>
    /// <remarks>
    ///     Beside <see cref="Mentioned" /> rather than folded into it, because the two answer different questions and
    ///     only one of them can be acted on: an account this client could not name cannot be opened, but it can
    ///     certainly be typed.
    /// </remarks>
    public string? MentionedAs => Reference is { Role: Role.Mention } picked
        ? Mentioned ?? picked.Text.TrimStart('@')
        : null;

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
    ///     The poll on the post being read, or <see langword="null" /> where the picked post carries none or is still
    ///     holding it behind a content warning — which is what settles whether the digits and <c>v</c> mean anything
    ///     here, and whether the status row says so.
    /// </summary>
    /// <remarks>
    ///     The post inside a boost, since that is what carries the poll and what a vote is cast in: a boost of a poll
    ///     is the same poll, the same way a boost of a post is the same post to every mark.
    ///     <para>
    ///         None at all while the post's own text is behind a content warning, which is where its poll is too since
    ///         #119: a poll nobody has been shown is not a poll to announce <c>v</c> and the digits for, and it is not
    ///         one a reader can cast a vote in either. <see cref="OnShow.Words" /> rather than
    ///         <see cref="OnShow.Media" />, because a poll is words — the sensitive flag hides the media of a post and
    ///         leaves its answers on screen.
    ///     </para>
    /// </remarks>
    public PostPoll? Poll => Picked is { } picked && Showing(picked) is { Words: true } show ? show.Shown.Poll : null;

    /// <summary>
    ///     Which of that poll's options are toggled and uncast, as indices into its answers — empty where none are,
    ///     which is what a poll being read rather than voted in looks like.
    /// </summary>
    public IReadOnlySet<int> Chosen => _chosen;

    /// <summary>
    ///     Toggles the <paramref name="option" />th answer of the picked post's poll, counted from zero — what the
    ///     digits <c>1</c>-<c>9</c> and <c>0</c> address directly (<c>docs/tui-shell.md</c>, #87).
    /// </summary>
    /// <remarks>
    ///     Exclusive on a single-choice poll: picking a new answer lets the last one go, because a ballot showing two
    ///     boxes ticked on a poll that takes one would be promising something the instance will refuse.
    ///     <para>
    ///         A poll that has closed, or that this profile has already voted in, has nothing to toggle: it is a
    ///         result to read rather than a question to answer, and its own <c>✓</c> is already saying which answer
    ///         this profile gave. Whether a vote would <em>land</em> is still the instance's (ADR-0009) — this is only
    ///         about not offering a ballot over an answered poll (#87 follow-up).
    ///     </para>
    /// </remarks>
    /// <returns>
    ///     Whether there was an answer there to toggle, which is what settles whether the key was used: a digit on a
    ///     post with no poll, on a poll already answered, or past the end of a short one, does nothing at all.
    /// </returns>
    public bool Toggle(int option)
    {
        if (Poll is not { TakesAVote: true } poll || option < 0 || option >= poll.Options.Count)
        {
            return false;
        }

        if (!_chosen.Remove(option))
        {
            if (!poll.MultipleChoice)
            {
                _chosen.Clear();
            }

            _chosen.Add(option);
        }

        return true;
    }

    /// <summary>
    ///     Lets an uncast vote go, which <c>esc</c> does before it pops and <c>j</c> and <c>k</c> do on the way past —
    ///     the same rule a picked reference follows, and for the same reason.
    /// </summary>
    /// <returns>Whether there was one, which is what settles whether <c>esc</c> was spent on it.</returns>
    public bool ClearChoices()
    {
        var had = _chosen.Count > 0;

        _chosen.Clear();

        return had;
    }

    /// <summary>
    ///     Which reference is picked out on the <paramref name="at" />th thing on this screen — none, unless it is the
    ///     thing picked out, since a reference pick lives inside the picked post and nowhere else.
    /// </summary>
    /// <remarks>
    ///     Wanted on its own only by the conversations list, which does pick references inside the message it draws
    ///     but can never have revealed it — what it picks out is a conversation, so <c>x</c> has no post to ask about.
    ///     Every other screen asks for the whole of <see cref="ReadingOf" /> instead.
    /// </remarks>
    protected Reference? ReferenceOn(int at) => Walking?.At == at ? Reference : null;

    /// <summary>
    ///     What this reader has done to <paramref name="post" />, the <paramref name="at" />th thing on this screen —
    ///     which is what drawing it takes.
    /// </summary>
    /// <remarks>
    ///     The one place the parts of it are put together, so that a screen draws a post by saying which post and
    ///     where it is rather than by answering the same two questions each screen used to answer for itself — and so
    ///     that the next thing a reader can do to a post is filled in here rather than threaded through every drawing
    ///     site again (#95).
    ///     <para>
    ///         Reachable inside this assembly rather than by a screen alone, because <see cref="PostList" /> is what
    ///         asks it for the four screens holding a list of posts and is not itself a screen (#99). Wider than the
    ///         one caller wants, which is as narrow as it goes — and narrower than <see langword="protected" /> was,
    ///         since nothing outside this assembly is a screen.
    ///     </para>
    /// </remarks>
    internal Reading ReadingOf(Post post, int at) =>
        new(Revealed.Has(post), ReferenceOn(at), Walking?.At == at ? Chosen : null);

    /// <summary>Moves what is picked out by <paramref name="by" /> items, stopping at either end.</summary>
    /// <remarks>
    ///     The picked reference goes with it, and so does an uncast vote: the reader has left the post both were
    ///     inside (<c>docs/tui-shell.md</c>).
    /// </remarks>
    public void Move(int by)
    {
        ClearReference();
        ClearChoices();
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
        ClearChoices();
        Walking?.Pick(at);
    }



    /// <summary>
    ///     The row this screen's page last began on, and whether that page was still following the pick — where the
    ///     reader was standing when they drilled off it, so that walking back out puts them there again (#133).
    /// </summary>
    /// <remarks>
    ///     Plain data the screen keeps and never reads, the way it keeps <see cref="Revealed" /> without knowing what
    ///     a terminal is: what a row means is the content region's, and this is only somewhere to leave it. Its
    ///     lifetime needs no minding either — a screen is on the stack for precisely as long as there is somewhere to
    ///     walk back to it from.
    ///     <para>
    ///         Nought and following on a screen nobody has read yet, which is what makes a push, an arrival and a
    ///         refresh go on opening at the top with nothing gating them: each of the three builds a new screen, and a
    ///         new screen remembers nothing.
    ///     </para>
    /// </remarks>
    internal int Began { get; set; }

    /// <summary>
    ///     Whether that page was still following the pick, or had been walked away from it with <c>↓</c> and <c>↑</c>.
    ///     Kept beside <see cref="Began" /> because a row on its own is not where somebody was: resumed as following,
    ///     a page walked away from the pick is snapped back onto it by the very first frame.
    /// </summary>
    internal bool Followed { get; set; } = true;

    /// <summary>The posts the reader has asked past the warning on, by the id of each.</summary>
    /// <remarks>
    ///     Held here rather than six times over, because what <c>x</c> does turned out not to vary by screen at all —
    ///     it is <see cref="Picked" /> and one question. Kept out of <see cref="Picked{T}" /> for the opposite reason:
    ///     only posts carry a warning, and a list of conversations or of accounts would be holding it for nothing.
    ///     <para>
    ///         One per screen, and so a reveal belongs to the screen it was made on and lasts exactly as long as that
    ///         screen is on the stack — the same lifetime <see cref="Began" /> has, and for the same reason: both are
    ///         what this reader did to what is in front of them here. Why that rather than one shared by the stack is
    ///         argued where the rest of the contract is (<c>docs/tui-shell.md</c>, #121).
    ///     </para>
    /// </remarks>
    protected Revealed Revealed { get; } = new();

    /// <summary>Shows what the picked post is hiding — its warned text, its sensitive attachments, or both.</summary>
    /// <returns>Whether there was anything to reveal, which is what settles whether the key was used.</returns>
    /// <remarks>
    ///     A screen with no posts on it picks none, so it reveals nothing without having to say so — the same reason
    ///     <see cref="Move" /> and <see cref="Pick" /> need no override on one.
    /// </remarks>
    public bool Reveal() => Picked is { } picked && Revealed.Ask(picked);

    /// <summary>
    ///     What <paramref name="post" /> is showing this reader — its author's words, what hangs off them, and which
    ///     post inside a boost either belongs to.
    /// </summary>
    /// <remarks>
    ///     Read rather than worked out here, so that what <c>←</c>/<c>→</c> may walk to and what <c>v</c> may vote in
    ///     cannot come to differ from what <see cref="PostLines" /> put on screen (#145). The reveal is this screen's
    ///     own, which is the whole of what the question turns on: a warning asked past belongs to the screen it was
    ///     asked past on (#121).
    /// </remarks>
    private OnShow Showing(Post post) => OnShow.Of(post, Revealed.Has(post));

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
