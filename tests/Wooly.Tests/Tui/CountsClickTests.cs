using System.Drawing;
using Terminal.Gui.Input;
using Wooly.Core.Errors;
using Wooly.Core.Posts;
using Wooly.Tests.Fakes;
using Wooly.Tui.Screens;
using Wooly.Tui.Shell;
using Wooly.Tui.Theme;
using static Wooly.Tests.Tui.ContentClicks;

namespace Wooly.Tests.Tui;

/// <summary>
///     The boost, favorite and reply of a post's <b>Counts</b> are controls (#593, #594): a click on one picks the post
///     and is the key for it, <c>b</c>, <c>f</c> or <c>r</c>, carried out on that post through the path the key takes.
///     Sent at cells of a headless terminal, as the content click tests are, and read off what the fake post engagement
///     was asked and what compose screen was put in front.
/// </summary>
public class CountsClickTests
{
    /// <summary>
    ///     A click on <c>↺</c> or <c>☆</c> on a post that is not picked picks it and asks the instance to put the mark
    ///     on it — that post, not the one picked before.
    /// </summary>
    [Theory]
    [InlineData("↺", PostMark.Boost)]
    [InlineData("☆", PostMark.Favorite)]
    public async Task ClickingACountOnAPostNotPickedMarksIt(string glyph, PostMark mark)
    {
        var built = Two();

        using var drawn = await DrawnShell.Of(80, Tall, Themes.Plain, built);

        var counts = CountsOf(drawn, "Two sheep");

        drawn.Click(ColumnOf(drawn, counts, glyph), counts);
        drawn.Settle();

        Assert.Equal("220", drawn.Shell.Screen.Picked?.Id);
        Assert.Equal(
            new FakePostEngagement.Marked("personal", "220", mark, Wanted: true),
            Assert.Single(built.Engagement.Marks));
    }

    /// <summary>A click on <c>⥀</c> or <c>★</c>, the reader's own mark, asks for it to be taken off, as the key does.</summary>
    [Theory]
    [InlineData("⥀", PostMark.Boost)]
    [InlineData("★", PostMark.Favorite)]
    public async Task ClickingTheReadersOwnMarkTakesItOff(string glyph, PostMark mark)
    {
        var built = new AShell
        {
            Timelines = FakeTimelineReader.Holding(
                APost.With(id: "110"),
                APost.With(id: "220", content: "Two sheep", marks: APost.Marked(boosted: true, favorited: true))),
        };

        using var drawn = await DrawnShell.Of(80, Tall, Themes.Plain, built);

        var counts = CountsOf(drawn, "Two sheep");

        drawn.Click(ColumnOf(drawn, counts, glyph), counts);
        drawn.Settle();

        Assert.Equal(
            new FakePostEngagement.Marked("personal", "220", mark, Wanted: false),
            Assert.Single(built.Engagement.Marks));
    }

    /// <summary>
    ///     The whole run is the target, not the glyph alone: a click on the number marks too, and once the instance
    ///     answers the counts are drawn again with the reader's mark among them.
    /// </summary>
    [Fact]
    public async Task ClickingTheNumberMarksAndTheCountsRedrawOnceAnswered()
    {
        var built = Two();
        built.Engagement = FakePostEngagement.Answering(
            APost.With(id: "220", content: "Two sheep", marks: APost.Marked(boosted: true)) with { Boosts = 4 });

        using var drawn = await DrawnShell.Of(80, Tall, Themes.Plain, built);

        var counts = CountsOf(drawn, "Two sheep");

        drawn.Click(ColumnOf(drawn, counts, "↺ 3") + 2, counts);
        drawn.Settle();

        Assert.Equal(PostMark.Boost, Assert.Single(built.Engagement.Marks).Mark);
        Assert.Contains("⥀ 4", drawn.Rows()[CountsOf(drawn, "Two sheep")], StringComparison.Ordinal);
    }

    /// <summary>On a boost the click acts on the post boosted, which is what the counts drawn are of, as the key does.</summary>
    [Theory]
    [InlineData("↺", PostMark.Boost)]
    [InlineData("☆", PostMark.Favorite)]
    public async Task OnABoostTheClickMarksThePostBoosted(string glyph, PostMark mark)
    {
        var built = new AShell
        {
            Timelines = FakeTimelineReader.Holding(
                APost.With(id: "110"),
                APost.With(
                    id: "220",
                    account: "ben@hachyderm.io",
                    boosted: APost.With(id: "330", content: "Two sheep"))),
        };

        using var drawn = await DrawnShell.Of(80, Tall, Themes.Plain, built);

        var counts = CountsOf(drawn, "Two sheep");

        drawn.Click(ColumnOf(drawn, counts, glyph), counts);
        drawn.Settle();

        Assert.Equal("220", drawn.Shell.Screen.Picked?.Id);
        Assert.Equal(
            new FakePostEngagement.Marked("personal", "330", mark, Wanted: true),
            Assert.Single(built.Engagement.Marks));
    }

    /// <summary>
    ///     The post screen's subject post spells its counts out, and the word is part of the run: a click on
    ///     <c>boosts</c> or <c>favorites</c> picks the subject from the parent picked above it and marks it.
    /// </summary>
    [Theory]
    [InlineData("boosts", PostMark.Boost)]
    [InlineData("favorites", PostMark.Favorite)]
    public async Task OnThePostScreenTheSpelledOutWordMarksTheSubject(string word, PostMark mark)
    {
        var built = new AShell
        {
            Timelines = FakeTimelineReader.Holding(APost.With(id: "110", content: "The reply")),
            Engagement = FakePostEngagement.Threaded(
                APost.With(id: "110", content: "The reply"),
                [APost.With(id: "100", content: "The question")]),
        };

        using var drawn = await DrawnShell.Of(80, Tall, Themes.Plain, built);

        drawn.Press(Key.Enter);
        drawn.Settle();
        drawn.Click(OverContent, RowOf(drawn, "The question"));

        Assert.Equal("100", drawn.Shell.Screen.Picked?.Id);

        var counts = RowOf(drawn, word);

        drawn.Click(ColumnOf(drawn, counts, word) + 1, counts);
        drawn.Settle();

        Assert.IsType<PostScreen>(drawn.Shell.Screen);
        Assert.Equal("110", drawn.Shell.Screen.Picked?.Id);
        Assert.Equal(
            new FakePostEngagement.Marked("personal", "110", mark, Wanted: true),
            Assert.Single(built.Engagement.Marks));
    }

    /// <summary>
    ///     Every screen drawing posts that takes no clicks of its own answers the counts alike, since the answer is
    ///     the base screen's: a feed, a thread's reply, a search's post and an account's post — the last thing on each,
    ///     clicked from the first.
    /// </summary>
    [Theory]
    [InlineData("feed")]
    [InlineData("post")]
    [InlineData("search")]
    [InlineData("account")]
    public async Task EveryScreenDrawingPostsAnswersTheCounts(string screen)
    {
        using var drawn = await On(screen);

        var last = Enumerable.Range(0, Tall)
            .Select(row => drawn.Content.ItemAt(new Point(OverContent, row)))
            .Max()!.Value;
        var counts = RowOf(drawn, "↩", after: ContentRowOf(drawn, last));
        var picked = drawn.Shell.Screen.Picked;

        drawn.Click(ColumnOf(drawn, counts, "☆"), counts);
        drawn.Settle();

        var marked = Assert.Single(drawn.Built.Engagement.Marks);

        Assert.NotSame(picked, drawn.Shell.Screen.Picked);
        Assert.Equal(drawn.Shell.Screen.Picked?.Id, marked.PostId);
        Assert.Equal(PostMark.Favorite, marked.Mark);
    }

    /// <summary>
    ///     The bookmark's slot, <c>pinned</c>, and the gaps between the runs are no controls: a click on any of them
    ///     picks the post and marks nothing.
    /// </summary>
    [Theory]
    [InlineData("bookmark")]
    [InlineData("pinned")]
    [InlineData("gap")]
    public async Task ClickingTheRestOfTheCountsOnlyPicks(string where)
    {
        var built = new AShell
        {
            Timelines = FakeTimelineReader.Holding(
                APost.With(id: "110"),
                APost.With(
                    id: "220",
                    content: "Two sheep",
                    isMine: true,
                    marks: APost.Marked(bookmarked: true, pinned: true))),
        };

        using var drawn = await DrawnShell.Of(80, Tall, Themes.Plain, built);

        var counts = CountsOf(drawn, "Two sheep");
        var column = where switch
        {
            "bookmark" => ColumnOf(drawn, counts, "⚑"),
            "pinned" => ColumnOf(drawn, counts, "pinned") + 2,
            _ => ColumnOf(drawn, counts, "☆") - 2,
        };

        drawn.Click(column, counts);
        drawn.Settle();

        Assert.Equal("220", drawn.Shell.Screen.Picked?.Id);
        Assert.Empty(built.Engagement.Marks);
    }

    /// <summary>
    ///     The instance has the last word, as for the key (ADR-0009): a mark it turns down is said on the status row in
    ///     its own words, nothing having been refused ahead of time.
    /// </summary>
    [Fact]
    public async Task AMarkTheInstanceRefusesIsSaidInItsWords()
    {
        var built = Two();
        built.Engagement = FakePostEngagement.Refusing(new Refusal("This action is not allowed"));

        using var drawn = await DrawnShell.Of(80, Tall, Themes.Plain, built);

        var counts = CountsOf(drawn, "Two sheep");

        drawn.Click(ColumnOf(drawn, counts, "↺"), counts);
        drawn.Settle();

        Assert.Single(built.Engagement.Marks);
        Assert.Equal("This action is not allowed", drawn.Shell.Notice);
        Assert.True(drawn.Shell.NoticeIsError);
    }

    /// <summary>
    ///     An open confirmation wins: a click on the counts declines it, as any click does, and marks nothing under it.
    /// </summary>
    [Fact]
    public async Task AClickOnTheCountsDeclinesAnOpenConfirmationAndMarksNothing()
    {
        var answers = Enumerable.Range(1, 3).Select(at => APost.AnAnswer($"Answer {at}", at)).ToList();

        var built = new AShell
        {
            Timelines = FakeTimelineReader.Holding(
                APost.With(id: "110", poll: APost.APoll(options: answers)),
                APost.With(id: "220", content: "Two sheep")),
        };

        using var drawn = await DrawnShell.Of(80, Tall, Themes.Plain, built);

        drawn.Press(new Key('2'));
        drawn.Press(Key.V);

        Assert.NotNull(drawn.Shell.Asking);

        var counts = CountsOf(drawn, "Two sheep");

        drawn.Click(ColumnOf(drawn, counts, "↺"), counts);
        drawn.Settle();

        Assert.Null(drawn.Shell.Asking);
        Assert.Equal("110", drawn.Shell.Screen.Picked?.Id);
        Assert.Empty(built.Engagement.Marks);
        Assert.Empty(built.Engagement.Votes);
    }

    /// <summary>
    ///     A double click on a count marks once, rather than putting the mark on and taking it off again, and opens
    ///     nothing: its second click is spent with its first.
    /// </summary>
    [Theory]
    [InlineData("↺", PostMark.Boost)]
    [InlineData("☆", PostMark.Favorite)]
    public async Task ADoubleClickOnACountMarksOnce(string glyph, PostMark mark)
    {
        var built = Two();

        using var drawn = await DrawnShell.Of(80, Tall, Themes.Plain, built);

        var feed = drawn.Shell.Screen;
        var counts = CountsOf(drawn, "Two sheep");

        drawn.DoubleClick(ColumnOf(drawn, counts, glyph), counts);
        drawn.Settle();

        Assert.Same(feed, drawn.Shell.Screen);
        Assert.Equal("220", drawn.Shell.Screen.Picked?.Id);
        Assert.Equal(
            new FakePostEngagement.Marked("personal", "220", mark, Wanted: true),
            Assert.Single(built.Engagement.Marks));
    }

    /// <summary>
    ///     Where <c>b</c> and <c>f</c> would not be carried out on the post clicked, the click only picks, as a click
    ///     anywhere else on the row does — the remark on the status row going with it — and says nothing: the
    ///     conversations list, whose rows are conversations, draws each one's last post with its counts, and has no post
    ///     for the keys to act on.
    /// </summary>
    [Fact]
    public async Task WhereTheKeyIsNotCarriedOutTheClickOnlyPicks()
    {
        using var drawn = await On("messages");

        var messages = Assert.IsType<DirectMessagesScreen>(drawn.Shell.Screen);
        var counts = RowOf(drawn, "↩", after: ContentRowOf(drawn, 1));

        drawn.Shell.StatusRow.Say("A remark.", isError: false);
        drawn.Click(ColumnOf(drawn, counts, "↺"), counts);
        drawn.Settle();

        Assert.Same(messages, drawn.Shell.Screen);
        Assert.Equal("8", messages.PickedConversation?.Id);
        Assert.Null(drawn.Shell.Notice);
        Assert.Empty(drawn.Built.Engagement.Marks);
    }

    /// <summary>
    ///     A click on the counts of a post whose top has been scrolled off the page marks it and leaves the page where it
    ///     is, the mark's answer drawn included: brought into view as <c>k</c> would, the post's byline would go to the
    ///     top of the page and the counts clicked would move out from under the pointer.
    /// </summary>
    [Fact]
    public async Task TheCountsClickedStayWhereTheyAre()
    {
        var built = new AShell
        {
            Timelines = FakeTimelineReader.Holding(
                APost.With(id: "110"),
                APost.With(id: "220", content: "Two sheep") with { Boosts = 7 },
                APost.With(id: "330", content: "Three sheep")),
        };

        using var drawn = await DrawnShell.Of(80, 24, Themes.Plain, built);

        while (drawn.Rows().Any(row => row[RailLines.Width..].Contains("Two sheep", StringComparison.Ordinal)))
        {
            drawn.Wheel(OverContent, 10);
        }

        var top = drawn.Content.Top;
        var counts = RowOf(drawn, "↺ 7");

        drawn.Click(ColumnOf(drawn, counts, "↺ 7"), counts);
        drawn.Settle();

        Assert.Equal("220", drawn.Shell.Screen.Picked?.Id);
        Assert.Equal("220", Assert.Single(built.Engagement.Marks).PostId);
        Assert.Equal(top, drawn.Content.Top);
        Assert.Equal(counts, RowOf(drawn, "↺ 7"));
    }

    /// <summary>
    ///     A click on <c>↩</c> on a post that is not picked picks it and puts up the compose screen <c>r</c> would, a
    ///     reply to that post addressed to its author (#594).
    /// </summary>
    [Fact]
    public async Task ClickingTheReplyCountOnAPostNotPickedRepliesToIt()
    {
        var built = new AShell
        {
            Timelines = FakeTimelineReader.Holding(
                APost.With(id: "110"),
                APost.With(id: "220", account: "ben@hachyderm.io", content: "Two sheep")),
        };

        using var drawn = await DrawnShell.Of(80, Tall, Themes.Plain, built);

        var feed = drawn.Shell.Screen;
        var counts = CountsOf(drawn, "Two sheep");

        drawn.Click(ColumnOf(drawn, counts, "↩"), counts);
        drawn.Settle();

        var compose = Assert.IsType<ComposeScreen>(drawn.Shell.Screen);

        Assert.Equal("220", feed.Picked?.Id);
        Assert.Equal(ComposeFor.Reply, compose.Purpose);
        Assert.Equal("220", compose.About?.Id);
        Assert.Equal("@ben@hachyderm.io ", compose.Text);
        Assert.Empty(built.Engagement.Marks);
    }

    /// <summary>
    ///     A double click on <c>↩</c> opens one compose screen, and its second click is spent with its first: the first
    ///     put the reply in front, under the pointer, and the second would otherwise land on whatever of it is drawn
    ///     there — a choice on To, or the body, where it put the caret in the middle of the author's handle.
    /// </summary>
    [Fact]
    public async Task ADoubleClickOnTheReplyCountOpensOneComposeAndClicksNothingOnIt()
    {
        var built = new AShell
        {
            Timelines = FakeTimelineReader.Holding(
                APost.With(id: "110"),
                APost.With(id: "220", account: "ben@hachyderm.io", content: "Two sheep")),
        };

        using var drawn = await DrawnShell.Of(80, Tall, Themes.Plain, built);

        var feed = drawn.Shell.Screen;
        var counts = CountsOf(drawn, "Two sheep");

        drawn.DoubleClick(ColumnOf(drawn, counts, "↩"), counts);
        drawn.Settle();

        var compose = Assert.IsType<ComposeScreen>(drawn.Shell.Screen);

        drawn.Press(new Key('x'));

        Assert.Equal([feed.Crumb, compose.Crumb], drawn.Shell.Crumbs);
        Assert.Equal("220", compose.About?.Id);
        Assert.Equal("@ben@hachyderm.io x", compose.Text);
    }

    /// <summary>
    ///     A reply by click goes no wider than one by key: in a conversation the compose screen opens on <c>direct</c>,
    ///     with the warning of the message answered, as <c>r</c> opens it.
    /// </summary>
    [Fact]
    public async Task InAConversationTheReplyKeepsItsAudienceAndWarning()
    {
        var built = new AShell
        {
            Messages = FakeDirectMessages.Threading(AConversation.Thread(
                AConversation.With(id: "7"),
                AConversation.DirectPost(id: "110", content: "Are you about?"),
                AConversation.DirectPost(id: "111", content: "All week") with { ContentWarning = "plans" })),
        };

        using var drawn = await DrawnShell.Of(80, Tall, Themes.Plain, built);

        drawn.Shell.Rail.GoTo(drawn.Shell.Rail.Of(DestinationKind.Messages));
        built.Host.Drain();
        drawn.Press(Key.Enter);
        built.Host.Drain();
        drawn.Redraw();

        var thread = Assert.IsType<ConversationScreen>(drawn.Shell.Screen);
        var counts = CountsOf(drawn, "plans");

        drawn.Click(ColumnOf(drawn, counts, "↩"), counts);
        drawn.Settle();

        var compose = Assert.IsType<ComposeScreen>(drawn.Shell.Screen);

        Assert.Equal("111", thread.Picked?.Id);
        Assert.Equal("111", compose.About?.Id);
        Assert.Equal(PostVisibility.Direct, compose.Visibility);
        Assert.Equal("plans", compose.Warning);
    }

    /// <summary>On a boost the reply answers the post boosted, whose counts are the ones drawn, as <c>r</c> does.</summary>
    [Fact]
    public async Task OnABoostTheReplyAnswersThePostBoosted()
    {
        var built = new AShell
        {
            Timelines = FakeTimelineReader.Holding(
                APost.With(id: "110"),
                APost.With(
                    id: "220",
                    account: "ben@hachyderm.io",
                    boosted: APost.With(id: "330", account: "ann@hachyderm.io", content: "Two sheep"))),
        };

        using var drawn = await DrawnShell.Of(80, Tall, Themes.Plain, built);

        var counts = CountsOf(drawn, "Two sheep");

        drawn.Click(ColumnOf(drawn, counts, "↩"), counts);
        drawn.Settle();

        var compose = Assert.IsType<ComposeScreen>(drawn.Shell.Screen);

        Assert.Equal("330", compose.About?.Id);
        Assert.Equal("@ann@hachyderm.io ", compose.Text);
    }

    /// <summary>
    ///     The subject post's spelled-out <c>1 replies</c> is the same control, word and all: a click on <c>replies</c>
    ///     picks the subject from the parent picked above it and replies to it.
    /// </summary>
    [Fact]
    public async Task OnThePostScreenTheSpelledOutRepliesRepliesToTheSubject()
    {
        var built = new AShell
        {
            Timelines = FakeTimelineReader.Holding(APost.With(id: "110", content: "The reply") with { Replies = 1 }),
            Engagement = FakePostEngagement.Threaded(
                APost.With(id: "110", content: "The reply") with { Replies = 1 },
                [APost.With(id: "100", content: "The question")]),
        };

        using var drawn = await DrawnShell.Of(80, Tall, Themes.Plain, built);

        drawn.Press(Key.Enter);
        drawn.Settle();
        drawn.Click(OverContent, RowOf(drawn, "The question"));

        var post = Assert.IsType<PostScreen>(drawn.Shell.Screen);
        var counts = RowOf(drawn, "1 replies");

        drawn.Click(ColumnOf(drawn, counts, "1 replies") + 4, counts);
        drawn.Settle();

        var compose = Assert.IsType<ComposeScreen>(drawn.Shell.Screen);

        Assert.Equal("110", post.Picked?.Id);
        Assert.Equal("110", compose.About?.Id);
    }

    /// <summary>
    ///     Where <c>r</c> would not be carried out on the post clicked, a click on <c>↩</c> only picks: the conversations
    ///     list, whose rows are conversations, has no post for it to answer.
    /// </summary>
    [Fact]
    public async Task WhereReplyIsNotCarriedOutTheClickOnTheReplyCountOnlyPicks()
    {
        using var drawn = await On("messages");

        var messages = Assert.IsType<DirectMessagesScreen>(drawn.Shell.Screen);
        var counts = RowOf(drawn, "↩", after: ContentRowOf(drawn, 1));

        drawn.Click(ColumnOf(drawn, counts, "↩"), counts);
        drawn.Settle();

        Assert.Same(messages, drawn.Shell.Screen);
        Assert.Equal("8", messages.PickedConversation?.Id);
    }

    /// <summary>Two posts on Home, the second the one clicked.</summary>
    private static AShell Two() => new()
    {
        Timelines = FakeTimelineReader.Holding(APost.With(id: "110"), APost.With(id: "220", content: "Two sheep")),
    };

    /// <summary>The row the <b>Counts</b> of the post reading <paramref name="text" /> are drawn on.</summary>
    private static int CountsOf(DrawnShell drawn, string text) => RowOf(drawn, "↩", after: RowOf(drawn, text));

    /// <summary>The column <paramref name="text" /> starts at on <paramref name="row" />.</summary>
    private static int ColumnOf(DrawnShell drawn, int row, string text)
    {
        var column = drawn.Rows()[row].IndexOf(text, StringComparison.Ordinal);

        Assert.True(column >= 0, $"Row {row} does not read {text}: {drawn.Rows()[row]}");

        return column;
    }

    /// <summary>An instance's refusal, in its own words, of the kind a front end says rather than treats as a defect.</summary>
    private sealed class Refusal(string words) : WoolyException(words);
}
