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
    private readonly List<Post> _posts = [.. posts];
    private readonly HashSet<string> _revealed = [];

    /// <inheritdoc />
    public override string Crumb => $"@{account.Address}";

    /// <inheritdoc />
    public override IReadOnlyList<KeyHint> Keys =>
    [
        new("j/k", "post"),
        new("⏎", "read"),
        new("r", "reply"),
        new("b", "boost"),
        new("f", "favorite"),
        new("x", "show warning"),
        new("esc", "back"),
        new("?", "keys"),
    ];

    /// <summary>The account being shown.</summary>
    public Account Account => account;

    /// <summary>Which of their posts is picked out.</summary>
    public int At { get; private set; }

    /// <summary>The posts of theirs that were read, newest first.</summary>
    public IReadOnlyList<Post> Posts => _posts;

    /// <inheritdoc />
    public override Post? Picked => _posts.Count == 0 ? null : _posts[At];

    /// <inheritdoc />
    public override void Move(int by)
    {
        if (_posts.Count > 0)
        {
            At = Math.Clamp(At + by, 0, _posts.Count - 1);
        }
    }

    /// <inheritdoc />
    public override bool Reveal()
    {
        if (Picked is not { } picked)
        {
            return false;
        }

        var shown = picked.Boosted ?? picked;

        return shown.ContentWarning is not null && _revealed.Add(shown.Id);
    }

    /// <inheritdoc />
    public override void Replace(Post post)
    {
        for (var at = 0; at < _posts.Count; at++)
        {
            if (_posts[at].Id == post.Id)
            {
                _posts[at] = post;
            }
            else if (_posts[at].Boosted?.Id == post.Id)
            {
                _posts[at] = _posts[at] with { Boosted = post };
            }
        }
    }

    /// <inheritdoc />
    public override void Remove(string postId)
    {
        _posts.RemoveAll(post => post.Id == postId || post.Boosted?.Id == postId);

        At = _posts.Count == 0 ? 0 : Math.Clamp(At, 0, _posts.Count - 1);
    }

    /// <inheritdoc />
    public override IReadOnlyList<Line> Lines(int width, DateTimeOffset now)
    {
        var room = Math.Max(1, width - 1);

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

        if (_posts.Count == 0)
        {
            lines.Add(Line.Of("Nothing to read here yet.", Role.Muted));

            return lines;
        }

        for (var at = 0; at < _posts.Count; at++)
        {
            var post = _posts[at];
            var picked = at == At;
            var shown = post.Boosted ?? post;

            foreach (var line in PostLines.Feed(post, room, _revealed.Contains(shown.Id), now))
            {
                lines.Add(line.After(new Span(picked ? "▌" : " ", picked ? Role.Selection : Role.Body)));
            }

            lines.Add(Line.Blank);
        }

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
