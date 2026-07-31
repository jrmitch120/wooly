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
    private readonly PickedPosts _picked = new(posts);

    /// <inheritdoc />
    public override string Crumb => destination.Label.ToLowerInvariant();

    /// <inheritdoc />
    public override IReadOnlyList<KeyHint> Keys =>
        PostKeys.Around(new KeyHint("j/k", "post"), new KeyHint("tab", "destination"));

    /// <summary>Which post is picked out, as an index into what is on screen.</summary>
    public int At => _picked.At;

    /// <summary>The posts on the timeline, newest first.</summary>
    public IReadOnlyList<Post> Posts => _picked.Posts;

    /// <summary>
    ///     Something the shell has to say about this timeline rather than about a post on it — that it is empty, or
    ///     that a rate limit cut it short.
    /// </summary>
    public string? Notice { get; } = notice;

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
        if (Notice is not { } notice)
        {
            return _picked.Lines(width, now);
        }

        return [Line.Of(TextWrap.Clip(notice, width), Role.Muted), Line.Blank, .. _picked.Lines(width, now)];
    }
}
