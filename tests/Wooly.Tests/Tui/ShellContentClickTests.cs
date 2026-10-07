using System.Drawing;
using Terminal.Gui.Input;
using Wooly.Core.Posts;
using Wooly.Tests.Fakes;
using Wooly.Tui.Media;
using Wooly.Tui.Screens;
using Wooly.Tui.Shell;
using Wooly.Tui.Theme;
using static Wooly.Tests.Tui.ContentClicks;

namespace Wooly.Tests.Tui;

/// <summary>
///     A click in the content, sent at a cell of a headless terminal: the thing the row under the pointer is part of is
///     picked, through the same pick the keys make, and a row that is part of nothing ignores it (#290).
/// </summary>
public class ShellContentClickTests
{
    /// <summary>
    ///     Any row of a post picks it — its byline, its text, and a picture on it, where the click lands on the picture's
    ///     own box — and the page stays where it was, so the post does not move out from under the pointer.
    /// </summary>
    [Theory]
    [InlineData("byline")]
    [InlineData("text")]
    [InlineData("picture")]
    public async Task ClickingAnyRowOfAPostPicksIt(string row)
    {
        var pictures = new FakePictures().Holding("m1", 800, 600);

        var built = new AShell
        {
            Timelines = FakeTimelineReader.Holding(
                APost.With(id: "110"),
                APost.With(id: "220", account: "ben@hachyderm.io", content: "Two sheep", media: [APost.APicture("m1")])),
        };

        using var drawn = await DrawnShell.Of(80, Tall, Themes.Plain, built, pictures: pictures, drawsPictures: true);

        var top = drawn.Content.Top;
        var at = row switch
        {
            "byline" => new Point(OverContent, RowOf(drawn, "ben@hachyderm.io")),
            "text" => new Point(OverContent, RowOf(drawn, "Two sheep")),
            _ => Middle(Assert.Single(drawn.Content.SubViews.OfType<PictureView>(), view => view.Visible)),
        };

        drawn.Click(at.X, at.Y);

        Assert.Equal("220", drawn.Shell.Screen.Picked?.Id);
        Assert.Equal(top, drawn.Content.Top);
    }

    /// <summary>
    ///     A click on a post taller than the terminal picks it and leaves the page alone: brought into view as <c>k</c>
    ///     would, the post's byline would go to the top of the page and the row clicked would move out from under the
    ///     pointer.
    /// </summary>
    [Fact]
    public async Task ClickingATallPostLeavesThePageWhereItIs()
    {
        var pictures = new FakePictures()
            .Holding("m1", 800, 600)
            .Holding("m2", 800, 600)
            .Holding("m3", 800, 600);

        var built = new AShell
        {
            Timelines = FakeTimelineReader.Holding(
                APost.With(id: "110"),
                APost.With(
                    id: "220",
                    content: "Three sheep",
                    media: [APost.APicture("m1"), APost.APicture("m2"), APost.APicture("m3", "The last sheep")])),
        };

        using var drawn = await DrawnShell.Of(80, 24, Themes.Plain, built, pictures: pictures, raster: ARaster.Kitty());

        var row = RowOf(drawn, "Three sheep");

        drawn.Click(OverContent, row);

        Assert.Equal("220", drawn.Shell.Screen.Picked?.Id);
        Assert.Equal(0, drawn.Content.Top);
        Assert.Contains("Three sheep", drawn.Rows()[row]);
    }

    /// <summary>
    ///     A click between two frames is answered from the rows as the last frame drew them, under the Raster that frame
    ///     worked out — not under one the terminal has answered since, which would lay a picture's box where the page
    ///     still shows a link and put the post under the pointer somewhere else (#357).
    /// </summary>
    [Fact]
    public async Task AClickBetweenFramesIsAnsweredFromTheRowsAsDrawn()
    {
        var pictures = new FakePictures().Holding("m1", 800, 600);

        var built = new AShell
        {
            Timelines = FakeTimelineReader.Holding(
                APost.With(id: "110", content: "One sheep", media: [APost.APicture("m1")]),
                APost.With(id: "220", content: "Two sheep")),
        };

        var answered = Raster.None;

        using var drawn = await DrawnShell.Of(80, Tall, Themes.Plain, built, pictures: pictures, answers: () => answered);

        var row = RowOf(drawn, "Two sheep");

        // The terminal answers that it draws pictures after the frame is drawn, and before the next one.
        answered = ARaster.Kitty();

        Assert.Equal(1, drawn.Content.ItemAt(new Point(OverContent, row)));
    }

    /// <summary>
    ///     A row that is part of nothing — the rule and blank between two posts, a section heading, and the space under
    ///     the last thing — changes nothing at all.
    /// </summary>
    [Theory]
    [InlineData("rule")]
    [InlineData("blank")]
    [InlineData("below")]
    [InlineData("heading")]
    public async Task AClickOnARowOfNothingChangesNothing(string row)
    {
        var searched = row is "heading" or "blank";

        using var drawn = searched ? await Searched() : await DrawnShell.Of(80, Tall, Themes.Plain, Two());

        if (searched)
        {
            // Off the first run, so a heading taken for the thing under it would show.
            drawn.Press(Key.K);
        }

        var at = row switch
        {
            "rule" => RowOf(drawn, "──────", after: RowOf(drawn, "Hello world")),
            "below" => Tall - 3,
            "heading" => RowOf(drawn, "2 hashtags"),
            // The blank between a heading and the first thing under it.
            _ => RowOf(drawn, "2 hashtags") + 1,
        };

        var rows = drawn.Rows();
        var picked = drawn.Shell.Screen.Picked;

        Assert.Null(drawn.Content.ItemAt(new Point(OverContent, at)));

        drawn.Click(OverContent, at);

        Assert.Equal(rows, drawn.Rows());
        Assert.Same(picked, drawn.Shell.Screen.Picked);
    }

    /// <summary>A click that picks takes the remark off the status row, as walking off with <c>j</c> does.</summary>
    [Fact]
    public async Task ClickingClearsTheRemark()
    {
        var built = new AShell
        {
            Timelines = FakeTimelineReader.Holding(
                APost.With(id: "110", content: "Hello", media: [APost.Attached(MediaKind.Video, id: "m1")]),
                APost.With(id: "220", content: "Two sheep")),
            Browser = FakeWebBrowser.WithNothingToOpen(),
        };

        using var drawn = await DrawnShell.Of(80, Tall, Themes.Plain, built);

        drawn.Shell.WalkReference(1);
        await drawn.Shell.OpenReference();
        built.Host.Drain();

        Assert.NotNull(drawn.Shell.Notice);

        drawn.Click(OverContent, RowOf(drawn, "Two sheep"));

        Assert.Null(drawn.Shell.Notice);
        Assert.Equal("220", drawn.Shell.Screen.Picked?.Id);
    }

    /// <summary>
    ///     A click on the post a reference has been walked to inside drops the reference, so that the <c>⏎</c> after it
    ///     opens the post rather than the hashtag.
    /// </summary>
    [Fact]
    public async Task ClickingAPostDropsTheReferenceWalkedInsideIt()
    {
        var built = new AShell
        {
            Timelines = FakeTimelineReader.Holding(
                APost.With(id: "110", content: "<p>Shearing day #sheep</p>"),
                APost.With(id: "220")),
        };

        using var drawn = await DrawnShell.Of(80, Tall, Themes.Plain, built);

        drawn.Press(Key.CursorRight);

        Assert.NotNull(drawn.Shell.Screen.Reference);

        drawn.Click(OverContent, RowOf(drawn, "Shearing day"));

        Assert.Null(drawn.Shell.Screen.Reference);
        Assert.Equal("110", drawn.Shell.Screen.Picked?.Id);

        drawn.Press(Key.Enter);
        built.Host.Drain();

        var post = Assert.IsType<PostScreen>(drawn.Shell.Screen);

        Assert.Equal("110", post.Picked?.Id);
    }

    /// <summary>
    ///     Every listing screen picks off a click as it does off the keys: a click on the first thing, from the second,
    ///     leaves the shell drawn exactly as <c>Home</c> does — the account screen's header block (after which the post
    ///     keys go quiet) and the first result of a search, in its section, among them.
    /// </summary>
    [Theory]
    [InlineData("feed")]
    [InlineData("post")]
    [InlineData("search")]
    [InlineData("notifications")]
    [InlineData("messages")]
    [InlineData("requests")]
    [InlineData("discover")]
    [InlineData("follows")]
    [InlineData("account")]
    [InlineData("profiles")]
    public async Task EveryListingScreenPicksOffAClickAsOffTheKeys(string screen)
    {
        using var drawn = await On(screen);

        drawn.Press(Key.K);

        var before = drawn.Rows();

        drawn.Click(OverContent, ContentRowOf(drawn, 0));

        var clicked = drawn.Rows();

        Assert.NotEqual(before, clicked);

        drawn.Press(Key.K);
        drawn.Press(Key.Home);

        Assert.Equal(drawn.Rows(), clicked);

        if (screen == "account")
        {
            Assert.Null(drawn.Shell.Screen.Picked);
        }
    }

    /// <summary>
    ///     A result further down a search picks in its own section, as walking to it with the keys does: the second
    ///     hashtag, after two accounts and the first hashtag.
    /// </summary>
    [Fact]
    public async Task ASearchResultDownTheListPicksInItsSection()
    {
        using var drawn = await Searched();

        drawn.Click(OverContent, RowOf(drawn, "#wool"));

        var clicked = drawn.Rows();

        drawn.Press(Key.Home);

        for (var step = 0; step < 3; step++)
        {
            drawn.Press(Key.K);
        }

        Assert.Equal(drawn.Rows(), clicked);
    }

    /// <summary>
    ///     The post screen picks the post a reply answers, above the one opened, as it picks anything else on it.
    /// </summary>
    [Fact]
    public async Task ThePostScreenPicksAnAncestorOffAClick()
    {
        var built = new AShell
        {
            Timelines = FakeTimelineReader.Holding(APost.With(id: "110", content: "The reply")),
            Engagement = FakePostEngagement.Threaded(
                APost.With(id: "110", content: "The reply"),
                [APost.With(id: "100", content: "The question")],
                APost.With(id: "120", content: "An answer")),
        };

        using var drawn = await DrawnShell.Of(80, Tall, Themes.Plain, built);

        drawn.Press(Key.Enter);
        built.Host.Drain();
        drawn.Redraw();

        Assert.IsType<PostScreen>(drawn.Shell.Screen);

        drawn.Click(OverContent, RowOf(drawn, "The question"));

        Assert.Equal("100", drawn.Shell.Screen.Picked?.Id);
    }

    /// <summary>An open confirmation wins: a click on a post declines it, as any key but the agreeing one does, and picks nothing.</summary>
    [Fact]
    public async Task AClickDeclinesAnOpenConfirmationAndPicksNothing()
    {
        var answers = Enumerable.Range(1, 3).Select(at => APost.AnAnswer($"Answer {at}", at)).ToList();

        using var drawn = await DrawnShell.Of(
            80,
            Tall,
            Themes.Plain,
            new AShell
            {
                Timelines = FakeTimelineReader.Holding(
                    APost.With(id: "110", poll: APost.APoll(options: answers)),
                    APost.With(id: "220", content: "Two sheep")),
            });

        drawn.Press(new Key('2'));
        drawn.Press(Key.V);

        Assert.NotNull(drawn.Shell.Asking);

        drawn.Click(OverContent, RowOf(drawn, "Two sheep"));

        Assert.Null(drawn.Shell.Asking);
        Assert.Equal("110", drawn.Shell.Screen.Picked?.Id);
        Assert.Empty(drawn.Built.Engagement.Votes);
    }

    /// <summary>And an open filter prompt: a click closes it, as a key the prompt does not take would, and picks nothing.</summary>
    [Fact]
    public async Task AClickClosesAnOpenFilterPromptAndPicksNothing()
    {
        using var drawn = await On("follows");

        drawn.Press(Key.F);

        var follows = Assert.IsType<FollowsScreen>(drawn.Shell.Screen);

        Assert.True(follows.IsTyping);

        drawn.Click(OverContent, RowOf(drawn, "bea@hachyderm.io"));

        Assert.Same(follows, drawn.Shell.Screen);
        Assert.False(follows.IsTyping);
        Assert.Equal("ann@hachyderm.io", follows.PickedPerson?.Address);
    }

    /// <summary>
    ///     With nobody to act as, a click anywhere in the content is a click on the add screen, which asks nothing and
    ///     goes nowhere — no more than its keys allow.
    /// </summary>
    [Fact]
    public async Task WithNobodyActedAsAContentClickDoesNothing()
    {
        var built = new AShell { Profiles = FakeProfileRegistry.Holding(current: null) };

        using var drawn = await DrawnShell.Of(80, Tall, Themes.Plain, built, launch: true);

        var screen = drawn.Shell.Screen;
        var rows = drawn.Rows();

        for (var row = 1; row < Tall - 1; row++)
        {
            drawn.Click(OverContent, row);
        }

        built.Host.Settle();
        drawn.Redraw();

        Assert.Same(screen, drawn.Shell.Screen);
        Assert.Equal(rows, drawn.Rows());
        Assert.Equal(0, built.Requests);
    }

    /// <summary>The cell in the middle of a picture's box.</summary>
    private static Point Middle(PictureView box)
    {
        var over = box.FrameToScreen();

        return new Point(over.X + (over.Width / 2), over.Y + (over.Height / 2));
    }

    /// <summary>Two posts on Home.</summary>
    private static AShell Two() => new()
    {
        Timelines = FakeTimelineReader.Holding(APost.With(id: "110"), APost.With(id: "220", content: "Two sheep")),
    };
}
