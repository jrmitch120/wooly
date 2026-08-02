using Wooly.Core.Posts;
using Wooly.Tui.Media;
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
    private readonly PickedPosts _replies = new(replies);
    private readonly PickedPosts _itself = new([post]);

    /// <inheritdoc />
    public override string Crumb => $"post by @{(Post.Boosted ?? Post).Account}";

    /// <inheritdoc />
    public override IReadOnlyList<KeyHint> Keys =>
        PostKeys.Around(new KeyHint("j/k", "post · replies"), new KeyHint("esc", "back"));

    /// <summary>Which of the post and its replies is picked out: 0 is the post, and the rest are the answers in order.</summary>
    public int At { get; private set; }

    /// <summary>The post this screen is about.</summary>
    public Post Post => _itself.Posts[0];

    /// <summary>What has been said in answer to it, oldest first.</summary>
    public IReadOnlyList<Post> Replies => _replies.Posts;

    /// <inheritdoc />
    public override Post? Picked => At == 0 ? Post : _replies.Posts[At - 1];

    /// <inheritdoc />
    public override void Move(int by) => At = PickedPosts.Clamped(At, by, _replies.Count);

    /// <inheritdoc />
    public override bool Reveal() => Picked is { } picked && Held(picked).Reveal(picked);

    /// <inheritdoc />
    public override void Replace(Post post)
    {
        _itself.Replace(post);
        _replies.Replace(post);
    }

    /// <inheritdoc />
    public override void Remove(string postId)
    {
        _replies.Remove(postId);

        At = Math.Clamp(At, 0, _replies.Count);
    }

    /// <inheritdoc />
    public override IReadOnlyList<Line> Lines(int width, DateTimeOffset now, IPictures? pictures = null)
    {
        var room = Math.Max(1, width - 1);
        var lines = new List<Line>();

        foreach (var line in PostLines.Whole(Post, room, _itself.IsRevealed(Post), now, pictures))
        {
            lines.Add(line.After(PickedPosts.Gutter(At == 0)));
        }

        lines.Add(Line.Blank);
        lines.Add(Line.Of(Heading(width), Role.Muted));
        lines.Add(Line.Blank);

        if (_replies.Count == 0)
        {
            lines.Add(Line.Of("Nobody has answered this yet.", Role.Muted)
                          .After(PickedPosts.Gutter(picked: false)));

            return lines;
        }

        // The replies draw their own gutter from their own index, which is one behind this screen's: the post itself
        // is what index zero picks out.
        for (var at = 0; at < _replies.Count; at++)
        {
            var reply = _replies.Posts[at];

            foreach (var line in PostLines.Feed(reply, room, _replies.IsRevealed(reply), now, pictures))
            {
                lines.Add(line.After(PickedPosts.Gutter(At == at + 1)));
            }

            lines.Add(Line.Blank);
        }

        return lines;
    }

    /// <summary>Which of the two lists a post on this screen belongs to, since the post itself is not one of its replies.</summary>
    private PickedPosts Held(Post post) => post.Id == Post.Id ? _itself : _replies;

    private string Heading(int width) =>
        TextWrap.Clip(_replies.Count == 0 ? "── replies ──" : $"── {_replies.Count} replies ──", width);
}
