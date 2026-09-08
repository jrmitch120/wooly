using Wooly.Core.Accounts;
using Wooly.Core.Posts;
using Wooly.Core.Relationships;
using Wooly.Tui.Rendering;
using Wooly.Tui.Theme;

namespace Wooly.Tui.Screens;

/// <summary>
///     One account: who they are, where this profile stands with them, and what they have posted. What <c>a</c> opens
///     onto, from a feed item or from inside a post.
/// </summary>
/// <remarks>
///     This is what the rejected right-hand context pane held — who wrote this, where you stand with them — at full
///     width and one keystroke away instead of costing the feed 24 columns (ADR-0014). The three tie actions are
///     capitals, so that a lower-case mark key can never fire one by accident (<c>docs/tui-shell.md</c>).
/// </remarks>
public sealed class AccountScreen : Screen
{
    private readonly PostList _posts;

    // Which of the posts the fetch found pinned, so the run they are in is read off the list itself rather than kept
    // as a length beside it: a deletion takes a row off the list, and a length would go on heading a run one longer
    // than the rows under it.
    private readonly HashSet<string> _pins;

    private readonly HeaderAndPosts _walking;

    /// <param name="account">The account being shown, as the instance last answered about them.</param>
    /// <param name="posts">The posts of theirs that were read off their timeline, newest first.</param>
    /// <param name="pinned">
    ///     The posts they have pinned to the top of their profile, in the instance's own order — empty where they have
    ///     pinned none, and <see langword="null" /> where the question went unput. Disjoint from
    ///     <paramref name="posts" />, the shell having dropped the duplicate from the timeline before either reaches
    ///     here (#182): a screen deciding for itself which run a post belongs in would be a screen that could disagree
    ///     with itself.
    ///     <para>
    ///         Said rather than defaulted, unlike <paramref name="familiar" />, because the two silences are not worth
    ///         the same: an unasked familiar-followers list draws nothing, and an unasked pinned run draws a row
    ///         saying so — so a caller that left it off would have the screen telling a reader about a call it never
    ///         meant to make.
    ///     </para>
    /// </param>
    /// <param name="familiar">
    ///     The accounts the reader follows that also follow this one, or <see langword="null" /> where the instance
    ///     was never asked — which is what a screen built with no answer to that question is handed, and what the
    ///     shell hands it when a rate limit stops the last of its calls (CONTEXT.md).
    /// </param>
    public AccountScreen(
        Account account,
        IReadOnlyList<Post> posts,
        IReadOnlyList<Post>? pinned,
        IReadOnlyList<Account>? familiar = null)
    {
        Familiar = familiar;
        Asked = pinned is not null;
        _pins = [.. (pinned ?? []).Select(post => post.Id)];
        _posts = new PostList(this, [.. pinned ?? [], .. posts]);
        _walking = new HeaderAndPosts(account, _posts);
    }

    /// <inheritdoc />
    public override string Crumb => $"@{Account.Address}";

    /// <inheritdoc />
    /// <remarks>
    ///     Every key that acts on the picked post goes quiet while the header block is picked, because there is no
    ///     post picked out there for them to act on — said by <see cref="Screen.Keys" /> for every screen at once
    ///     rather than here (#193), so this list is the same whichever thing on the screen is picked. The screen's own
    ///     three are on it either way: a tie is with the person, and this is their screen wherever the reader stands.
    /// </remarks>
    protected override IReadOnlyList<KeyHint> OwnKeys =>
        PostKeys.Around(
            new KeyHint("j/k", "post"),
            [
                .. Jumping,
                new KeyHint("F", Says(Follows, "unfollow", "follow")),
                new KeyHint("M", Says(Account.Standing?.Muting, "unmute", "mute")),
                new KeyHint("B", Says(Account.Standing?.Blocking, "unblock", "block")),
                new KeyHint("w", "follows"),
                Refreshing,
            ],
            new KeyHint("esc", "back"));

    /// <inheritdoc />
    public override bool Refreshes => true;

    /// <summary>The account being shown, as the instance last answered about them.</summary>
    /// <remarks>
    ///     Held by the walk rather than beside it, because the header block is the first thing walked on this screen
    ///     and what it draws is this account: two copies would be a screen that could pick one and draw the other.
    /// </remarks>
    public Account Account => _walking.Account;

    /// <summary>
    ///     The posts of theirs that are on the screen, in the order they are drawn and walked: what they have pinned,
    ///     in the instance's own pin order, and then their timeline newest first (#182).
    /// </summary>
    /// <remarks>
    ///     One list rather than two, which is the shape the post screen already draws
    ///     <c>[...ancestors, post, ...replies]</c> in: the screen has one pick, and two lists would be a screen able
    ///     to pick a post in one of them and draw the selection in the other. Which run a post is in is a fact about
    ///     where it sits, and is asked of <see cref="Pins" />.
    /// </remarks>
    public IReadOnlyList<Post> Posts => _posts.All;

    /// <summary>
    ///     Whether the instance was asked what they have pinned. False leaves a row saying so where the run would
    ///     have been, which is not the same as the nothing an account with no pins draws — absent is not empty
    ///     (CONTEXT.md), and the precedent is <see cref="AccountLines.Standing" />'s own.
    /// </summary>
    public bool Asked { get; }

    /// <summary>
    ///     The accounts the reader follows that also follow this one, or <see langword="null" /> where the instance
    ///     was never asked. Empty and absent draw the same nothing, and are told apart here rather than on the row:
    ///     the screen holds which of the two it was handed, and neither costs a row it cannot honestly fill.
    /// </summary>
    public IReadOnlyList<Account>? Familiar { get; }

    /// <inheritdoc />
    /// <remarks>
    ///     None while the header block is picked, which is what the screen opens on: the reader is standing on the
    ///     person rather than on anything they wrote, so the keys that act on a post have nothing to act on (#179).
    /// </remarks>
    public override Post? Picked => _walking.Picked;

    /// <inheritdoc />
    protected override IPicked Walking => _walking;

    /// <inheritdoc />
    /// <remarks>
    ///     The header block is the first reference source in this client that is not a post: the hashtags and
    ///     addresses in a bio and in a custom field's value, walked in the order they are drawn (ADR-0019).
    /// </remarks>
    protected override IReferring? Referring =>
        _walking.OnHeader ? new HeaderReferences(Account) : base.Referring;

    /// <summary>
    ///     Whether the tie <paramref name="tie" /> names is in place, which is what settles whether pressing its key
    ///     puts it on or takes it off — three ties that are each on or off, rather than six acts (ADR-0012).
    /// </summary>
    /// <remarks>
    ///     A follow this account has not answered yet counts as in place: what <c>F</c> undoes on a locked account is
    ///     the request, and offering to follow somebody you have already asked would be offering to ask twice.
    /// </remarks>
    public bool Has(AccountTie tie) => Account.Standing?.Has(tie) ?? false;

    /// <inheritdoc />
    /// <remarks>
    ///     Through the walk rather than straight at the account, so that the header block and the posts stay one
    ///     numbering — the same reason <see cref="Remove" /> goes that way.
    /// </remarks>
    public override void Stands(Account account) => _walking.Stands(account);

    /// <inheritdoc />
    public override void Replace(Post post) => _posts.Replace(post);

    /// <inheritdoc />
    /// <remarks>
    ///     Through the walk rather than straight at the list, so that the pick is brought back inside what is left by
    ///     whatever is holding it: the header block and the posts are numbered together, and a list re-clamping on its
    ///     own would leave the screen picking one thing and drawing another (#179).
    /// </remarks>
    public override void Remove(string postId) => _walking.Remove(postId);

    /// <inheritdoc />
    /// <remarks>
    ///     Two headed runs where they have pinned anything, spliced into the one list of posts: the pinned run counted,
    ///     because it is complete and unpaged so its total is a fact, and the timeline left uncounted, because counting
    ///     a page of an unbounded list would be a number about the fetch pretending to be a number about the account
    ///     (#182). Both are marked as headings rather than only drawn like them, which is what <c>[</c> and <c>]</c>
    ///     move between (<see cref="Sections" />).
    /// </remarks>
    public override IReadOnlyList<Line> Lines(Drawing drawing)
    {
        var width = drawing.Width;
        var pinned = Pins;

        var lines = new List<Line>(_walking.HeaderRows(
            (account, at, room) => AccountLines.Header(account, Familiar, drawing.In(room), ReferenceOn(at)),
            width));

        if (!Asked)
        {
            // Where the section would be, and one row rather than a heading over nothing: a heading says how many are
            // under it, and this is the case where nobody knows.
            lines.Add(Line.Of("Pinned posts not asked for.", Role.Muted));
            lines.Add(Line.Blank);
        }
        else if (pinned > 0)
        {
            lines.Add(Heading($"── {pinned} pinned ──", width));
            lines.Add(Line.Blank);
            lines.AddRange(_walking.PostRows(drawing, from: 0, howMany: pinned));
        }

        // No blank of its own above the divider: the divider is a separator, and whatever stands above it — the header
        // block's last section, or the rule after the last pinned post — has already brought one (ADR-0019).
        lines.Add(Heading("── their posts ──", width));
        lines.Add(Line.Blank);

        if (_posts.Count == pinned)
        {
            lines.Add(Line.Of("Nothing to read here yet.", Role.Muted));

            return lines;
        }

        lines.AddRange(_walking.PostRows(drawing, from: pinned, howMany: _posts.Count - pinned));

        return lines;
    }

    /// <summary>Whether a follow is in place or waiting to be let in, which <c>F</c> treats the same way.</summary>
    private bool Follows => Has(AccountTie.Follow);

    /// <summary>
    ///     How many of the posts on screen are in the pinned run — the leading ones the fetch found pinned, the shell
    ///     having handed the two runs over disjoint and in that order.
    /// </summary>
    /// <remarks>
    ///     Counted off the list every frame rather than held as a length, so that a deletion cannot leave the heading
    ///     saying more than the rows under it — the same reason the search screen counts its own runs off the results
    ///     rather than beside them (<see cref="Sections" />). A mark is not a deletion: <c>p</c> rewrites a row where
    ///     it stands, so un-pinning inside the run moves nothing and the heading goes on saying what the fetch found
    ///     until <c>g</c> re-asks.
    /// </remarks>
    private int Pins => _posts.All.TakeWhile(post => _pins.Contains(post.Id)).Count();

    /// <summary>
    ///     The key that moves between this screen's sections, said only where there are two headed runs to move
    ///     between — which most accounts do not have, having pinned nothing (#177, #182).
    /// </summary>
    /// <remarks>
    ///     Counted as the runs with something in them, which are the runs the jump can reach: a heading over an empty
    ///     timeline begins no run (<see cref="Sections" />), and the header block is a run with no heading and so no
    ///     stop of its own. Read off <see cref="Pins" />, which is what <see cref="Lines" /> draws by, so the runs the
    ///     reader can see are the runs the key is announced for with no second count kept anywhere.
    /// </remarks>
    private IReadOnlyList<KeyHint> Jumping =>
        Pins > 0 && _posts.Count > Pins ? [new KeyHint("[/]", "section")] : [];

    /// <summary>One of this screen's headings, marked as one so that <c>[</c> and <c>]</c> know the run it stands over.</summary>
    private static Line Heading(string heading, int width) =>
        Line.Of(TextWrap.Clip(heading, width), Role.Muted).Heading();

    /// <summary>
    ///     What a tie key offers: taking the tie off where it is on. A standing the instance was not asked for reads
    ///     as no tie, which is the only thing that can be offered without inventing an answer.
    /// </summary>
    private static string Says(bool? inPlace, string undo, string put) => inPlace == true ? undo : put;
}
