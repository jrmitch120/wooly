using Wooly.Core.Posts;
using Wooly.Tests.Fakes;
using Wooly.Tui.Rendering;
using Wooly.Tui.Screens;
using Wooly.Tui.Theme;

namespace Wooly.Tests.Tui;

/// <summary>
///     What the post screen says about what the post itself answers: the chain above it, drawn whole and walked the
///     same way its replies are, with the reader still standing on the post they opened (#86).
/// </summary>
/// <remarks>
///     Held against the screen rather than against the shell, because what is under test is one list with a post
///     somewhere in the middle of it — which of them is picked out at the start, which of them <c>⏎</c> refuses, and
///     which of them the screen is still about after one is taken off it.
/// </remarks>
public class PostThreadTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 29, 12, 30, 0, TimeSpan.Zero);

    /// <summary>The post the screen is about, two posts down a thread, with one answer under it.</summary>
    private static PostScreen Opened(int ancestors = 2, int replies = 1) =>
        new(
            Answering(APost.With(id: "110"), "100"),
            new PostThread(
                [.. Enumerable.Range(0, ancestors).Select(at => Above(at))],
                [.. Enumerable.Range(0, replies).Select(at => Answering(APost.With(id: $"11{at + 1}"), "110"))]));

    /// <summary>The <paramref name="at" />th post up the chain: the root answers nothing, the rest answer the one above.</summary>
    private static Post Above(int at) =>
        at == 0 ? APost.With(id: "100") : Answering(APost.With(id: $"10{at}"), $"10{at - 1}");

    /// <summary>A post that answers <paramref name="answered" />, which is what puts the <c>↳</c> row on it.</summary>
    private static Post Answering(Post post, string answered) =>
        post with { InReplyTo = new PostReplyTarget { PostId = answered, Handle = "maria@fosstodon.org" } };

    /// <summary>Which post each row belongs to, by the ordinal <see cref="Picked{T}" /> stamps on it.</summary>
    private static IReadOnlyList<int> Items(IReadOnlyList<Line> lines) =>
        [.. lines.Select(line => line.Item).OfType<int>()];

    /// <summary>Whether a row is the <c>↳</c> mark, behind the gutter column every row is drawn after.</summary>
    private static bool SaysWhatItAnswers(Line line) =>
        line.Text.TrimStart(' ', '▌').StartsWith("↳", StringComparison.Ordinal);

    /// <summary>
    ///     The chain the post answers is drawn above it, in the same list its replies are in: the root first, the post
    ///     itself in the middle, and what answered it last.
    /// </summary>
    [Fact]
    public void Lines_DrawTheAncestorChainAboveThePostAndTheRepliesBelowIt()
    {
        var screen = Opened();

        Assert.Equal(["100", "101"], screen.Ancestors.Select(ancestor => ancestor.Id));
        Assert.Equal("110", screen.Post.Id);
        Assert.Equal(["111"], screen.Replies.Select(reply => reply.Id));

        // Four posts on one screen, drawn in the order they are walked and none of them numbered twice.
        Assert.Equal([0, 1, 2, 3], Items(screen.Lines(new Drawing(61, Now))).Distinct());
    }

    /// <summary>
    ///     The whole chain, uncapped: a post five deep in a thread shows the four above it rather than the nearest
    ///     one.
    /// </summary>
    [Fact]
    public void Lines_DrawTheWholeChainRatherThanTheNearestAncestor()
    {
        var screen = Opened(ancestors: 4);

        Assert.Equal(["100", "101", "102", "103"], screen.Ancestors.Select(ancestor => ancestor.Id));
        Assert.Equal(4, screen.At);
    }

    /// <summary>
    ///     The reader lands on the post they opened, not at the top of the thread — the chain above it is context, and
    ///     a screen that opened onto somebody else's post would answer <c>b</c> and <c>f</c> about the wrong one.
    /// </summary>
    [Fact]
    public void Picked_StartsOnThePostTheScreenIsAboutRatherThanTheTopOfTheThread()
    {
        var screen = Opened();

        Assert.Equal(2, screen.At);
        Assert.Equal("110", screen.Picked?.Id);
    }

    /// <summary>An ancestor is opened by <c>⏎</c> exactly as a reply is: a whole post, on a screen of its own.</summary>
    [Fact]
    public void Opens_OpensAnAncestorTheSameWayItOpensAReply()
    {
        var screen = Opened();

        screen.Pick(0);
        Assert.Equal("100", screen.Opens?.Id);

        screen.Pick(3);
        Assert.Equal("111", screen.Opens?.Id);
    }

    /// <summary>
    ///     Only the post the screen is about opens nothing, and it is no longer the first thing on the list — drilling
    ///     into it would push a copy of this same screen (#48).
    /// </summary>
    [Fact]
    public void Opens_RefusesThePostTheScreenIsAboutRatherThanWhateverIsFirst()
    {
        var screen = Opened();

        screen.Pick(2);

        Assert.Null(screen.Opens);
    }

    /// <summary>
    ///     A heading above the post says how many stand over it, the way the one below it says how many answered.
    /// </summary>
    [Fact]
    public void Lines_HeadTheAncestorsWithHowManyStandAboveThePost()
    {
        var lines = Opened().Lines(new Drawing(61, Now)).ToList();

        var at = lines.FindIndex(line => line.Text.EndsWith("up ──", StringComparison.Ordinal));

        Assert.Equal("── 2 up ──", lines[at].Text);
        Assert.Equal(Role.Muted, lines[at].Role);

        // Between the last ancestor and the post itself, which is what "above the post" means on a list — a blank
        // either side of it, the same spacing the replies heading below already has.
        Assert.Equal(1, lines[at - 2].Item);
        Assert.Equal(2, lines[at + 2].Item);
    }

    /// <summary>A post that answers nothing gets no heading, rather than one saying nothing stands above it.</summary>
    [Fact]
    public void Lines_SayNothingAboveAPostThatAnswersNothing()
    {
        var lines = Opened(ancestors: 0).Lines(new Drawing(61, Now));

        Assert.DoesNotContain(lines, line => line.Text.EndsWith("up ──", StringComparison.Ordinal));

        // The post itself is the first thing on the screen again, which is where a thread with no head to it starts.
        Assert.Equal(0, Items(lines)[0]);
        Assert.Equal(0, Opened(ancestors: 0).At);
    }

    /// <summary>
    ///     The post's own <c>↳</c> mark comes off here and only here: what it points at is drawn whole immediately
    ///     above it, and saying both would name the same post twice over.
    /// </summary>
    [Fact]
    public void Lines_LeaveTheReplyMarkOffThePostTheScreenIsAbout()
    {
        var lines = Opened().Lines(new Drawing(61, Now));

        var mine = lines.Where(line => line.Item == 2).ToList();

        Assert.DoesNotContain(mine, line => SaysWhatItAnswers(line));
    }

    /// <summary>Every other post on the screen keeps its mark: an ancestor and a reply are posts like any other.</summary>
    [Fact]
    public void Lines_KeepTheReplyMarkOnTheAncestorsAndTheReplies()
    {
        var lines = Opened().Lines(new Drawing(61, Now));

        Assert.Contains(lines, line => line.Item == 1 && SaysWhatItAnswers(line));
        Assert.Contains(lines, line => line.Item == 3 && SaysWhatItAnswers(line));
    }

    /// <summary>
    ///     And the post itself keeps its own where nothing came back above it — what it answers has been deleted, or
    ///     the instance did not send the chain. The mark comes off because its ancestor is drawn whole immediately
    ///     above it (<c>docs/tui-shell.md</c>), and with no ancestor there the row is all the reader has.
    /// </summary>
    [Fact]
    public void Lines_KeepTheReplyMarkOnAPostWithNoAncestorsAboveIt()
    {
        var lines = Opened(ancestors: 0).Lines(new Drawing(61, Now));

        Assert.Contains(lines, line => line.Item == 0 && SaysWhatItAnswers(line));
    }

    /// <summary>
    ///     The reader opens onto their own post rather than onto the top of somebody else's thread: the offset starts
    ///     at nought and following the pick is what carries the page down to it on the first draw
    ///     (<c>docs/tui-shell.md</c>).
    /// </summary>
    [Fact]
    public void Lines_OpenOnThePostItselfEvenWhereTheChainAboveItFillsThePage()
    {
        var lines = Opened(ancestors: 4).Lines(new Drawing(61, Now));

        var at = Scroll.To(lines, 20, from: 0);

        Assert.NotEqual(0, at);
        Assert.True(Scroll.Shows(lines, 20, at), "the post the screen is about is off the page it opens on");
    }

    /// <summary>
    ///     Which post the screen is about is the post itself rather than a place in the list, so an ancestor deleted
    ///     out from under it does not hand the screen over to the root of the thread.
    /// </summary>
    [Fact]
    public void Remove_KeepsTheScreenAboutItsOwnPostWhenAnAncestorGoes()
    {
        var screen = Opened();

        screen.Remove("100");

        Assert.Equal("110", screen.Post.Id);
        Assert.Equal(["101"], screen.Ancestors.Select(ancestor => ancestor.Id));
        Assert.Equal(["111"], screen.Replies.Select(reply => reply.Id));

        screen.Pick(1);
        Assert.Null(screen.Opens);
    }

    /// <summary>
    ///     The post the screen is about stays on it even where the instance says it is gone, the same as before there
    ///     was anything above it: a thread with no head to it is a screen about nothing.
    /// </summary>
    [Fact]
    public void Remove_LeavesThePostTheScreenIsAboutWhereItIs()
    {
        var screen = Opened();

        screen.Remove("110");

        Assert.Equal("110", screen.Post.Id);
        Assert.Equal(2, screen.At);
    }

    /// <summary>The breadcrumb names the post the screen is about, not the one at the top of the thread.</summary>
    [Fact]
    public void Crumb_NamesThePostTheScreenIsAbout()
    {
        var screen = new PostScreen(
            APost.With(id: "110", account: "jeff@mastodon.social"),
            new PostThread([APost.With(id: "100", account: "maria@fosstodon.org")], []));

        Assert.Equal("post by @jeff@mastodon.social", screen.Crumb);
    }
}
