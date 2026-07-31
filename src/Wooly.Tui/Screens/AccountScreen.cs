using System.Globalization;
using Wooly.Core.Accounts;
using Wooly.Core.Posts;
using Wooly.Tui.Rendering;
using Wooly.Tui.Theme;

namespace Wooly.Tui.Screens;

/// <summary>
///     One account: who they are, where this profile stands with them, and what they have posted. What <c>a</c> opens
///     onto, from a feed item or from inside a post.
/// </summary>
/// <remarks>
///     This is what the rejected right-hand context pane held — who wrote this, where you stand with them — at full
///     width and one keystroke away instead of costing the feed 24 columns (ADR-0014). The three tie actions it lists
///     are #29's; the screen, and the standing it draws, are this ticket's.
/// </remarks>
public sealed class AccountScreen(Account account, IReadOnlyList<Post> posts) : Screen
{
    private readonly PickedPosts _picked = new(posts);

    /// <inheritdoc />
    public override string Crumb => $"@{account.Address}";

    /// <inheritdoc />
    public override IReadOnlyList<KeyHint> Keys =>
        PostKeys.Around(new KeyHint("j/k", "post"), new KeyHint("esc", "back"));

    /// <summary>The account being shown.</summary>
    public Account Account => account;

    /// <summary>Which of their posts is picked out.</summary>
    public int At => _picked.At;

    /// <summary>The posts of theirs that were read, newest first.</summary>
    public IReadOnlyList<Post> Posts => _picked.Posts;

    /// <inheritdoc />
    public override Post? Picked => _picked.Picked;

    /// <inheritdoc />
    public override void Move(int by) => _picked.Move(by);

    /// <inheritdoc />
    public override bool Reveal() => _picked.Reveal();

    /// <inheritdoc />
    public override void Replace(Post post) => _picked.Replace(post);

    /// <inheritdoc />
    public override void Remove(string postId) => _picked.Remove(postId);

    /// <inheritdoc />
    public override IReadOnlyList<Line> Lines(int width, DateTimeOffset now)
    {
        var lines = new List<Line>
        {
            Line.Of(TextWrap.Clip(account.Author, width), Role.BylineName),
            Line.Of(TextWrap.Clip($"@{account.Address}", width), Role.BylineHandle),
            Line.Blank,
            Line.Of(
                TextWrap.Clip(
                    $"{Number(account.Posts)} posts · {Number(account.Following)} following · {Number(account.Followers)} followers",
                    width),
                Role.Muted),
            Standing(width),
            Line.Blank,
            Line.Of("── their posts ──", Role.Muted),
            Line.Blank,
        };

        if (_picked.Count == 0)
        {
            lines.Add(Line.Of("Nothing to read here yet.", Role.Muted));

            return lines;
        }

        lines.AddRange(_picked.Lines(width, now));

        return lines;
    }

    private static string Number(long count) => count.ToString("N0", CultureInfo.CurrentCulture);

    /// <summary>
    ///     Where the profile stands with them, or the fact that the instance was not asked. Absent is not the same as
    ///     nothing (CONTEXT.md), and five silences would say the profile follows nobody.
    /// </summary>
    private Line Standing(int width)
    {
        if (account.Standing is not { } standing)
        {
            return Line.Of("Standing not asked for.", Role.Muted);
        }

        var said = new List<string>();

        if (standing.Following)
        {
            said.Add("you follow them");
        }
        else if (standing.FollowRequested)
        {
            said.Add("you have asked to follow them");
        }

        if (standing.FollowedBy)
        {
            said.Add("they follow you");
        }

        if (standing.Blocking)
        {
            said.Add("blocked");
        }

        if (standing.Muting)
        {
            said.Add("muted");
        }

        return said.Count == 0
            ? Line.Of("No ties either way.", Role.Muted)
            : Line.Of(TextWrap.Clip(string.Join(" · ", said), width), Role.Muted);
    }
}
