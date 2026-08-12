using Wooly.Core.Posts;
using Wooly.Tests.Fakes;
using Wooly.Tui.Media;
using Wooly.Tui.Rendering;
using Wooly.Tui.Screens;
using Wooly.Tui.Theme;

namespace Wooly.Tests.Tui;

/// <summary>
///     A list of posts on a screen (#99): what a mark or a deletion does to it, and what its rows are drawn from.
/// </summary>
/// <remarks>
///     Held once here rather than four times over, which is the point of the module: the index and the clamp are
///     <see cref="Picked{T}" />'s and asserted in <see cref="PickedTests" />, so what is left is what makes this a list
///     of <em>posts</em> — a boost being the same post as far as a change goes, a head of a thread that a deletion
///     leaves alone, and rows that know what the reader has done to what they are drawing.
/// </remarks>
public class PostListTests
{
    /// <summary>A post with references in its text, for the row that has one of them picked out.</summary>
    private const string Said = "Thanks @maria@fosstodon.org";

    /// <summary>A change to a post reaches the copy of it a list is holding, which is what lights a star up.</summary>
    [Fact]
    public void Replace_PutsTheChangedPostInPlaceOfTheCopyBeingHeld()
    {
        var list = Holding(APost.With(id: "1"), APost.With(id: "2"));

        list.Replace(APost.With(id: "2", content: "Edited"));

        Assert.Equal(["Hello world", "Edited"], list.All.Select(post => post.Content));
    }

    /// <summary>
    ///     A boost is the same post as far as a mark goes, so the change lands inside it — and the boost itself, which
    ///     says who boosted it, is still what the row is drawn from.
    /// </summary>
    [Fact]
    public void Replace_ReachesThePostInsideABoost()
    {
        var list = Holding(APost.With(id: "9", account: "ben@hachyderm.io", boosted: APost.With(id: "1")));

        list.Replace(APost.With(id: "1", content: "Edited"));

        Assert.Equal("Edited", Assert.Single(list.All).Boosted?.Content);
        Assert.Equal("ben@hachyderm.io", Assert.Single(list.All).Account);
    }

    /// <summary>A deleted post goes, whether the list is holding it or a boost of it.</summary>
    [Theory]
    [InlineData("2")]
    [InlineData("1")]
    public void Remove_TakesOffThePostTheIdNamesAndAnyBoostOfIt(string deleted)
    {
        var list = Holding(APost.With(id: "2"), APost.With(id: "9", boosted: APost.With(id: "1")));

        list.Remove(deleted);

        Assert.Equal(deleted == "2" ? ["9"] : ["2"], list.All.Select(post => post.Id));
    }

    /// <summary>
    ///     What a post screen asks for: the post the screen is about stays even when it is the one deleted, because a
    ///     thread with no head to it is a screen about nothing and the shell walks out of it instead.
    /// </summary>
    [Fact]
    public void Remove_LeavesThePostItIsToldToKeep()
    {
        var head = APost.With(id: "1");
        var list = Holding(head, APost.With(id: "2"));

        list.Remove("1", head);
        list.Remove("2", head);

        Assert.Equal(["1"], list.All.Select(post => post.Id));
    }

    /// <summary>Every post on the list is drawn as a feed row, named and marked the way the walk numbers them.</summary>
    [Fact]
    public void Rows_DrawEveryPostAsAFeedRow()
    {
        var screen = new AListOfPosts([APost.With(id: "1", content: "First"), APost.With(id: "2", content: "Second")]);

        screen.Pick(1);

        var lines = screen.Lines(61, AShell.Now);

        Assert.Contains(lines, line => line.Text.Contains("First"));
        Assert.Contains(lines, line => line.Text.Contains("Second"));
        Assert.Equal([0, 1], lines.Select(line => line.Item).OfType<int>().Distinct().Order());
        Assert.Equal(
            lines.Where(line => line.Item == 1).ToList(),
            lines.Where(line => line.Has(Role.Selection)).ToList());
    }

    /// <summary>
    ///     What the reader has done to a post is folded in here rather than by whoever asks for the rows — which is
    ///     what the list is built from a screen for.
    /// </summary>
    [Fact]
    public void Rows_DrawWhatTheReaderHasAskedPastTheWarningOf()
    {
        var screen = new AListOfPosts([APost.With(id: "1", contentWarning: "spoilers", content: "The sheep did it")]);

        Assert.DoesNotContain(screen.Lines(61, AShell.Now), line => line.Text.Contains("The sheep did it"));

        screen.Reveal();

        Assert.Contains(screen.Lines(61, AShell.Now), line => line.Text.Contains("The sheep did it"));
    }

    /// <summary>
    ///     A picked reference is drawn inside the picked post's row and nowhere else, which is the half of a reading
    ///     that varies by where in the list a post is.
    /// </summary>
    [Fact]
    public void Rows_PickOutAReferenceOnlyOnThePostBeingRead()
    {
        var screen = new AListOfPosts([APost.With(id: "1", content: Said), APost.With(id: "2", content: Said)]);

        screen.WalkReference(1);

        var picked = screen.Lines(61, AShell.Now).Where(line => line.Text.Contains('‹')).ToList();

        Assert.All(picked, line => Assert.Equal(0, line.Item));
        Assert.NotEmpty(picked);
    }

    /// <summary>
    ///     One post's rows on their own, for the screen that splices a heading between the post it is about and the
    ///     answers to it — drawn the same way the whole list would have drawn them.
    /// </summary>
    [Fact]
    public void RowsOf_DrawOnePostTheWayTheListWould()
    {
        var screen = new AListOfPosts([APost.With(id: "1", content: "First"), APost.With(id: "2", content: "Second")]);

        var rows = screen.Posts.RowsOf(1, 61, AShell.Now);

        Assert.Equal(
            screen.Lines(61, AShell.Now).Where(line => line.Item == 1).Select(line => line.Text),
            rows.Select(line => line.Text));
    }

    /// <summary>
    ///     A screen may draw a post its own way — the post screen draws the one it is about whole — and the row is
    ///     still stamped and numbered by the list.
    /// </summary>
    [Fact]
    public void RowsOf_TakeTheScreensOwnDrawing()
    {
        var screen = new AListOfPosts([APost.With(id: "1"), APost.With(id: "2")]);

        var rows = screen.Posts.RowsOf(1, 61, (post, at, room) => [Line.Of($"{post.Id} at {at} in {room}", Role.Body)]);

        Assert.Equal(1, Assert.Single(rows).Item);
        Assert.Contains("2 at 1 in 60", rows[0].Text);
    }

    /// <summary>A list on a screen of its own, so that what the four screens share can be asserted without them.</summary>
    private static PostList Holding(params Post[] posts) => new AListOfPosts(posts).Posts;

    /// <summary>
    ///     A screen that is nothing but a list of posts: the four that hold one differ in what they put around it, and
    ///     none of that is what this asks about.
    /// </summary>
    private sealed class AListOfPosts : Screen
    {
        public AListOfPosts(IReadOnlyList<Post> posts) => Posts = new PostList(this, posts);

        public PostList Posts { get; }

        public override string Crumb => "posts";

        public override Post? Picked => Posts.Out;

        protected override IReadOnlyList<KeyHint> OwnKeys => [];

        protected override IPicked Walking => Posts;

        public override IReadOnlyList<Line> Lines(
            int width,
            DateTimeOffset now,
            IPictures? pictures = null,
            bool hideDrawnCaption = false) => Posts.Rows(width, now, pictures, hideDrawnCaption);
    }
}
