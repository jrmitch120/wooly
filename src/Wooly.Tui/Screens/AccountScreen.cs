using Wooly.Core.Accounts;
using Wooly.Core.Posts;
using Wooly.Tui.Media;
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
public sealed class AccountScreen(Account account, IReadOnlyList<Post> posts) : Screen
{
    private readonly PickedPosts _picked = new(posts);

    /// <inheritdoc />
    public override string Crumb => $"@{Account.Address}";

    /// <inheritdoc />
    public override IReadOnlyList<KeyHint> Keys =>
        PostKeys.Around(
            new KeyHint("j/k", "post"),
            [
                new KeyHint("F", Says(Follows, "unfollow", "follow")),
                new KeyHint("M", Says(Account.Standing?.Muting, "unmute", "mute")),
                new KeyHint("B", Says(Account.Standing?.Blocking, "unblock", "block")),
            ],
            new KeyHint("esc", "back"));

    /// <summary>The account being shown, as the instance last answered about them.</summary>
    public Account Account { get; private set; } = account;

    /// <summary>Which of their posts is picked out.</summary>
    public int At => _picked.At;

    /// <summary>The posts of theirs that were read, newest first.</summary>
    public IReadOnlyList<Post> Posts => _picked.Posts;

    /// <inheritdoc />
    public override Post? Picked => _picked.Picked;

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
    public void Stands(Account account) => Account = account;

    /// <inheritdoc />
    public override void Move(int by) => _picked.Move(by);

    /// <inheritdoc />
    public override void Pick(int at) => _picked.Pick(at);

    /// <inheritdoc />
    public override bool Reveal() => _picked.Reveal();

    /// <inheritdoc />
    public override void Replace(Post post) => _picked.Replace(post);

    /// <inheritdoc />
    public override void Remove(string postId) => _picked.Remove(postId);

    /// <inheritdoc />
    public override IReadOnlyList<Line> Lines(int width, DateTimeOffset now, IPictures? pictures = null)
    {
        var lines = new List<Line>(AccountLines.Who(Account, width))
        {
            Line.Blank,
            AccountLines.Presence(Account, width),
            AccountLines.Standing(Account, width),
            Line.Blank,
            Line.Of("── their posts ──", Role.Muted),
            Line.Blank,
        };

        if (_picked.Count == 0)
        {
            lines.Add(Line.Of("Nothing to read here yet.", Role.Muted));

            return lines;
        }

        lines.AddRange(_picked.Lines(width, now, pictures));

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
