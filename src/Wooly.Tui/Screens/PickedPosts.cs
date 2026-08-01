using Wooly.Core.Posts;
using Wooly.Tui.Media;
using Wooly.Tui.Rendering;
using Wooly.Tui.Theme;

namespace Wooly.Tui.Screens;

/// <summary>
///     A list of posts with one of them picked out: what a feed is, what an account's posts are, and what the replies
///     under a post are. Held here rather than three times over, so that moving the selection, revealing a warning and
///     replacing a post that has just been marked cannot come to mean three slightly different things on three
///     screens.
/// </summary>
public sealed class PickedPosts(IReadOnlyList<Post> posts)
{
    private readonly List<Post> _posts = [.. posts];
    private readonly Revealed _revealed = new();

    /// <summary>Which post is picked out, as an index into what is on screen.</summary>
    public int At { get; private set; }

    /// <summary>The posts, in the order they were read.</summary>
    public IReadOnlyList<Post> Posts => _posts;

    /// <summary>How many there are.</summary>
    public int Count => _posts.Count;

    /// <summary>The post picked out, or <see langword="null" /> where there are none.</summary>
    public Post? Picked => _posts.Count == 0 ? null : _posts[At];

    /// <summary>
    ///     Moves the selection by <paramref name="by" />, stopping at either end — a list you walked off the end of is
    ///     a list you have lost your place in.
    /// </summary>
    /// <remarks>
    ///     Counted in <see cref="long" /> because <c>Home</c> and <c>End</c> ask to move by the largest step there is,
    ///     and adding that to an index overflows back to the other end of the list.
    /// </remarks>
    public void Move(int by)
    {
        if (_posts.Count > 0)
        {
            At = Clamped(At, by, _posts.Count - 1);
        }
    }

    /// <summary>The same clamp, for a screen whose selection is not a plain index into a list of posts.</summary>
    public static int Clamped(int at, int by, int most) => (int)Math.Clamp((long)at + by, 0, most);

    /// <summary>Shows what the picked post's content warning is hiding.</summary>
    /// <returns>Whether there was anything to reveal, which settles whether the key was used.</returns>
    public bool Reveal() => Picked is { } picked && Reveal(picked);

    /// <summary>Shows what <paramref name="post" />'s content warning is hiding.</summary>
    public bool Reveal(Post post) => _revealed.Ask(post);

    /// <summary>Whether the reader has asked to see past <paramref name="post" />'s warning.</summary>
    public bool IsRevealed(Post post) => _revealed.Has(post);

    /// <summary>Puts <paramref name="post" /> in place of the copy this list is holding, after a mark changed it.</summary>
    public void Replace(Post post)
    {
        for (var at = 0; at < _posts.Count; at++)
        {
            if (_posts[at].Id == post.Id)
            {
                _posts[at] = post;
            }
            else if (_posts[at].Boosted?.Id == post.Id)
            {
                // The list is holding a boost of the post that changed. Only the post inside it is replaced: who
                // boosted it, and whether this profile did, is a fact about the boost and not about the post.
                _posts[at] = _posts[at] with { Boosted = post };
            }
        }
    }

    /// <summary>
    ///     Puts <paramref name="post" /> at the end of the list, which is where a message this profile has just sent
    ///     belongs in the thread it answers.
    /// </summary>
    public void Add(Post post) => _posts.Add(post);

    /// <summary>Takes the post <paramref name="postId" /> names off the list, after it was deleted.</summary>
    public void Remove(string postId)
    {
        _posts.RemoveAll(post => post.Id == postId || post.Boosted?.Id == postId);

        At = _posts.Count == 0 ? 0 : Math.Clamp(At, 0, _posts.Count - 1);
    }

    /// <summary>
    ///     The posts as rows, each behind a gutter that says whether it is the one picked out. One column of gutter is
    ///     always taken, so that moving the selection down does not shift every post sideways as it goes.
    /// </summary>
    public IReadOnlyList<Line> Lines(int width, DateTimeOffset now, IPictures? pictures = null)
    {
        var room = Math.Max(1, width - 1);
        var lines = new List<Line>();

        for (var at = 0; at < _posts.Count; at++)
        {
            var post = _posts[at];

            foreach (var line in PostLines.Feed(post, room, IsRevealed(post), now, pictures))
            {
                lines.Add(line.After(Gutter(at == At)));
            }

            lines.Add(Line.Blank);
        }

        return lines;
    }

    /// <summary>The one column that says which row is picked out, by a mark as well as by a role.</summary>
    public static Span Gutter(bool picked) => new(picked ? "▌" : " ", picked ? Role.Selection : Role.Body);
}
