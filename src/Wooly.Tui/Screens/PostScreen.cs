using Wooly.Core.Posts;
using Wooly.Tui.Rendering;
using Wooly.Tui.Theme;

namespace Wooly.Tui.Screens;

/// <summary>
///     One post whole, with what has been said in answer to it underneath. What <c>⏎</c> on a feed item opens onto.
/// </summary>
/// <remarks>
///     The post itself is the first thing picked out, and <c>j</c> walks down into the replies — so every key that
///     acts on a post (boost, favorite, reply, delete) means the same thing here as on the feed, and a reply can be
///     answered without leaving the thread it is in.
/// </remarks>
public sealed class PostScreen(Post post, IReadOnlyList<Post> replies) : Screen
{
    private readonly List<Post> _replies = [.. replies];
    private readonly HashSet<string> _revealed = [];

    private Post _post = post;

    /// <inheritdoc />
    public override string Crumb => $"post by @{(_post.Boosted ?? _post).Account}";

    /// <inheritdoc />
    public override IReadOnlyList<KeyHint> Keys =>
    [
        new("j/k", "reply"),
        new("a", "author"),
        new("r", "reply"),
        new("b", "boost"),
        new("f", "favorite"),
        new("p", "pin"),
        new("e", "edit"),
        new("d", "delete"),
        new("x", "show warning"),
        new("esc", "back"),
        new("?", "keys"),
    ];

    /// <summary>Which of the post and its replies is picked out: 0 is the post, and the rest are the answers in order.</summary>
    public int At { get; private set; }

    /// <summary>The post this screen is about.</summary>
    public Post Post => _post;

    /// <summary>What has been said in answer to it, oldest first.</summary>
    public IReadOnlyList<Post> Replies => _replies;

    /// <inheritdoc />
    public override Post? Picked => At == 0 ? _post : _replies[At - 1];

    /// <inheritdoc />
    public override void Move(int by) => At = Math.Clamp(At + by, 0, _replies.Count);

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
        if (_post.Id == post.Id)
        {
            _post = post;
        }
        else if (_post.Boosted?.Id == post.Id)
        {
            _post = _post with { Boosted = post };
        }

        for (var at = 0; at < _replies.Count; at++)
        {
            if (_replies[at].Id == post.Id)
            {
                _replies[at] = post;
            }
        }
    }

    /// <inheritdoc />
    public override void Remove(string postId)
    {
        _replies.RemoveAll(reply => reply.Id == postId);

        At = Math.Clamp(At, 0, _replies.Count);
    }

    /// <inheritdoc />
    public override IReadOnlyList<Line> Lines(int width, DateTimeOffset now)
    {
        var room = Math.Max(1, width - 1);
        var lines = new List<Line>();

        foreach (var line in PostLines.Whole(_post, room, Revealed(_post), now))
        {
            lines.Add(line.After(Gutter(At == 0)));
        }

        lines.Add(Line.Blank);
        lines.Add(Line.Of(Heading(width), Role.Muted));
        lines.Add(Line.Blank);

        if (_replies.Count == 0)
        {
            lines.Add(Line.Of("Nobody has answered this yet.", Role.Muted).After(Gutter(picked: false)));

            return lines;
        }

        for (var at = 0; at < _replies.Count; at++)
        {
            var reply = _replies[at];

            foreach (var line in PostLines.Feed(reply, room, Revealed(reply), now))
            {
                lines.Add(line.After(Gutter(At == at + 1)));
            }

            lines.Add(Line.Blank);
        }

        return lines;
    }

    private static Span Gutter(bool picked) => new(picked ? "▌" : " ", picked ? Role.Selection : Role.Body);

    private string Heading(int width) =>
        TextWrap.Clip(_replies.Count == 0 ? "── replies ──" : $"── {_replies.Count} replies ──", width);

    private bool Revealed(Post post) => _revealed.Contains((post.Boosted ?? post).Id);
}
