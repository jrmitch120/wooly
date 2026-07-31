using Wooly.Core.Posts;
using Wooly.Tui.Rendering;
using Wooly.Tui.Shell;
using Wooly.Tui.Theme;

namespace Wooly.Tui.Screens;

/// <summary>
///     A timeline: the posts on it, one of them picked out, and the gutter that says which. The screen a rail
///     destination opens onto, and the one the shell starts on.
/// </summary>
public sealed class FeedScreen(Destination destination, IReadOnlyList<Post> posts, string? notice = null) : Screen
{
    private readonly List<Post> _posts = [.. posts];
    private readonly HashSet<string> _revealed = [];

    /// <inheritdoc />
    public override string Crumb => destination.Label.ToLowerInvariant();

    /// <inheritdoc />
    public override IReadOnlyList<KeyHint> Keys =>
    [
        new("j/k", "post"),
        new("⏎", "read"),
        new("a", "author"),
        new("c", "compose"),
        new("r", "reply"),
        new("b", "boost"),
        new("f", "favorite"),
        new("p", "pin"),
        new("e", "edit"),
        new("d", "delete"),
        new("x", "show warning"),
        new("tab", "destination"),
        new("?", "keys"),
    ];

    /// <summary>Which post is picked out, as an index into what is on screen.</summary>
    public int At { get; private set; }

    /// <summary>The posts on the timeline, newest first.</summary>
    public IReadOnlyList<Post> Posts => _posts;

    /// <summary>
    ///     Something the shell has to say about this timeline rather than about a post on it — that it is empty, or
    ///     that a rate limit cut it short.
    /// </summary>
    public string? Notice { get; } = notice;

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
        if (Picked is not { } picked || Warned(picked) is not { } warned)
        {
            return false;
        }

        return _revealed.Add(warned);
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
                // The timeline is holding a boost of the post that changed. Only the post inside it is replaced: who
                // boosted it, and whether this profile did, is a fact about the boost and not about the post.
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
        // One column of gutter, always taken, so that moving the selection down the feed does not shift every post
        // sideways as it goes.
        var room = Math.Max(1, width - 1);
        var lines = new List<Line>();

        if (Notice is { } notice)
        {
            lines.Add(Line.Of(TextWrap.Clip(notice, width), Role.Muted));
            lines.Add(Line.Blank);
        }

        for (var at = 0; at < _posts.Count; at++)
        {
            var post = _posts[at];
            var shown = post.Boosted ?? post;
            var picked = at == At;

            foreach (var line in PostLines.Feed(post, room, _revealed.Contains(shown.Id), now))
            {
                lines.Add(line.After(new Span(picked ? "▌" : " ", picked ? Role.Selection : Role.Body)));
            }

            lines.Add(Line.Blank);
        }

        return lines;
    }

    /// <summary>The id whose warning is being hidden or shown, which for a boost is the post inside it.</summary>
    private static string? Warned(Post post)
    {
        var shown = post.Boosted ?? post;

        return shown.ContentWarning is null ? null : shown.Id;
    }
}
