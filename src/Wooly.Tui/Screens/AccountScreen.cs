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

    private readonly HeaderAndPosts _walking;

    /// <param name="account">The account being shown, as the instance last answered about them.</param>
    /// <param name="posts">The posts of theirs that were read, newest first.</param>
    /// <param name="familiar">
    ///     The accounts the reader follows that also follow this one, or <see langword="null" /> where the instance
    ///     was never asked — which is what a screen built with no answer to that question is handed, and what the
    ///     shell hands it when a rate limit stops the last of its calls (CONTEXT.md).
    /// </param>
    public AccountScreen(Account account, IReadOnlyList<Post> posts, IReadOnlyList<Account>? familiar = null)
    {
        Familiar = familiar;
        _posts = new PostList(this, posts);
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
                new KeyHint("F", Says(Follows, "unfollow", "follow")),
                new KeyHint("M", Says(Account.Standing?.Muting, "unmute", "mute")),
                new KeyHint("B", Says(Account.Standing?.Blocking, "unblock", "block")),
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

    /// <summary>The posts of theirs that were read, newest first.</summary>
    public IReadOnlyList<Post> Posts => _posts.All;

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
    public bool Has(AccountTie tie) => tie switch
    {
        AccountTie.Follow => Follows,
        AccountTie.Block => Account.Standing?.Blocking ?? false,
        AccountTie.Mute => Account.Standing?.Muting ?? false,
        _ => false,
    };

    /// <summary>Puts the account as the instance now has it in place of the copy this screen is holding.</summary>
    /// <remarks>
    ///     What stops a follow reading as un-followed until the screen is opened again — the same reason a marked post
    ///     replaces the copy a feed is holding.
    /// </remarks>
    public void Stands(Account account) => _walking.Stands(account);

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
    public override IReadOnlyList<Line> Lines(Drawing drawing)
    {
        var lines = new List<Line>(_walking.HeaderRows(
            (account, at, room) => AccountLines.Header(account, Familiar, drawing.In(room), ReferenceOn(at)),
            drawing.Width))
        {
            // No blank of its own above the divider: the divider is a separator, and the header block's last section
            // has already brought the one that separates it from what is above (ADR-0019).
            Line.Of("── their posts ──", Role.Muted),
            Line.Blank,
        };

        if (_posts.Count == 0)
        {
            lines.Add(Line.Of("Nothing to read here yet.", Role.Muted));

            return lines;
        }

        lines.AddRange(_walking.PostRows(drawing));

        return lines;
    }

    /// <summary>Whether a follow is in place or waiting to be let in, which <c>F</c> treats the same way.</summary>
    private bool Follows => Account.Standing is { } standing && (standing.Following || standing.FollowRequested);

    /// <summary>
    ///     What a tie key offers: taking the tie off where it is on. A standing the instance was not asked for reads
    ///     as no tie, which is the only thing that can be offered without inventing an answer.
    /// </summary>
    private static string Says(bool? inPlace, string undo, string put) => inPlace == true ? undo : put;
}
