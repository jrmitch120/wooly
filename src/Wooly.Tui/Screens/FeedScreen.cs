using Wooly.Core.Posts;
using Wooly.Tui.Media;
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
    private readonly Picked<Post> _posts = new(posts);

    /// <inheritdoc />
    public override string Crumb => destination.Label.ToLowerInvariant();

    /// <inheritdoc />
    protected override IReadOnlyList<KeyHint> OwnKeys =>
        PostKeys.Around(new KeyHint("j/k", "post"), new KeyHint("tab", "destination"));

    /// <summary>Which post is picked out, as an index into what is on screen.</summary>
    public int At => _posts.At;

    /// <summary>The posts on the timeline, newest first.</summary>
    public IReadOnlyList<Post> Posts => _posts.All;

    /// <summary>
    ///     Something the shell has to say about this timeline rather than about a post on it — that it is empty, or
    ///     that a rate limit cut it short.
    /// </summary>
    public string? Notice { get; } = notice;

    /// <inheritdoc />
    public override Post? Picked => _posts.Out;

    /// <inheritdoc />
    protected override IPicked Walking => _posts;

    /// <inheritdoc />
    public override void Replace(Post post) => _posts.Rewrite(held => PostChange.Replaced(held, post));

    /// <inheritdoc />
    public override void Remove(string postId) => _posts.Remove(held => PostChange.Names(held, postId));

    /// <inheritdoc />
    public override IReadOnlyList<Line> Lines(
        int width,
        DateTimeOffset now,
        IPictures? pictures = null,
        bool hideDrawnCaption = false)
    {
        var rows = _posts.Rows(
            width,
            (post, at, room) => PostLines.Feed(post, room, ReadingOf(post, at), now, pictures, hideDrawnCaption));

        if (Notice is not { } notice)
        {
            return rows;
        }

        return [Line.Of(TextWrap.Clip(notice, width), Role.Muted), Line.Blank, .. rows];
    }
}
