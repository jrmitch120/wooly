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
public sealed class FeedScreen : Screen
{
    private readonly Destination _destination;

    private readonly PostList _posts;

    /// <param name="destination">Which timeline this is, which is what the breadcrumb says.</param>
    /// <param name="posts">The posts on it, newest first.</param>
    /// <param name="notice">Something the shell has to say about the timeline rather than about a post on it.</param>
    /// <param name="refreshes">
    ///     Whether this feed is a destination the rail arrived at, which is what settles whether <c>g</c> means
    ///     anything on it: a hashtag walked to from a search or a reference is the same screen over a destination the
    ///     rail keeps no place for, and there is nothing there for a refresh to evict and ask again (#84). The two are
    ///     told apart by how the screen was reached rather than by what is in it, because a tag the reader named and a
    ///     tag they walked to are the same destination by value.
    /// </param>
    public FeedScreen(
        Destination destination,
        IReadOnlyList<Post> posts,
        string? notice = null,
        bool refreshes = false)
    {
        _destination = destination;
        _posts = new PostList(this, posts);
        Notice = notice;
        Refreshes = refreshes;
    }

    /// <inheritdoc />
    public override string Crumb => _destination.Label.ToLowerInvariant();

    /// <inheritdoc />
    public override bool Refreshes { get; }

    /// <inheritdoc />
    protected override IReadOnlyList<KeyHint> OwnKeys =>
        PostKeys.Around(
            new KeyHint("j/k", "post"),
            Refreshes ? [Screen.Refreshing] : [],
            new KeyHint("tab", "destination"));

    /// <summary>The posts on the timeline, newest first.</summary>
    public IReadOnlyList<Post> Posts => _posts.All;

    /// <summary>
    ///     Something the shell has to say about this timeline rather than about a post on it — that it is empty, or
    ///     that a rate limit cut it short.
    /// </summary>
    public string? Notice { get; }

    /// <inheritdoc />
    public override Post? Picked => _posts.Out;

    /// <inheritdoc />
    protected override IPicked Walking => _posts;

    /// <inheritdoc />
    public override void Replace(Post post) => _posts.Replace(post);

    /// <inheritdoc />
    public override void Remove(string postId) => _posts.Remove(postId);

    /// <inheritdoc />
    public override IReadOnlyList<Line> Lines(
        int width,
        DateTimeOffset now,
        IPictures? pictures = null,
        bool hideDrawnCaption = false)
    {
        var rows = _posts.Rows(width, now, pictures, hideDrawnCaption);

        if (Notice is not { } notice)
        {
            return rows;
        }

        return [Line.Of(TextWrap.Clip(notice, width), Role.Muted), Line.Blank, .. rows];
    }
}
