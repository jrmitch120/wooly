using Terminal.Gui.Input;
using Wooly.Core.Paging;
using Wooly.Core.Posts;
using Wooly.Tests.Fakes;
using Wooly.Tui.Screens;
using Wooly.Tui.Shell;
using Wooly.Tui.Theme;
using Wooly.Tui.Views;
using static Wooly.Tests.Tui.ContentClicks;

namespace Wooly.Tests.Tui;

/// <summary>
///     A click on a crumb of the breadcrumb, sent at a cell of a headless terminal: the stack walks back to that crumb's
///     screen in one move, as it was left, and everything on the breadcrumb that is not an ancestor crumb ignores it
///     (#308).
/// </summary>
public class ShellCrumbClickTests
{
    /// <summary>A column inside the rail, clear of its left edge and its mark column.</summary>
    private const int OverRail = RailLines.Width / 2;

    /// <summary>The breadcrumb's row: the content panel's top edge.</summary>
    private const int Breadcrumb = 0;

    /// <summary>
    ///     Drilled from Home into a post with a hashtag walked to, then into the tag and on into an author off it, a click
    ///     on the post's crumb walks back to the post's screen: its page, its pick and the reference picked on it as they
    ///     were left, and nothing asked of the instance.
    /// </summary>
    [Fact]
    public async Task ClickingAnAncestorCrumbWalksBackToItAsItWasLeft()
    {
        // Wide enough that the whole trail is drawn.
        using var drawn = await DrawnShell.Of(160, Tall, Themes.Plain, Hashtagged());

        drawn.Press(Key.Enter);
        drawn.Settle();

        var post = Assert.IsType<PostScreen>(drawn.Shell.Screen);

        drawn.Press(Key.K);
        drawn.Press(Key.CursorRight);

        var picked = post.Picked?.Id;
        var reference = post.Reference;
        var top = drawn.Content.Top;

        Assert.True(reference is not null, string.Join('\n', drawn.Rows()) + post.Picked?.Id);

        drawn.Press(Key.Enter);
        drawn.Settle();
        await drawn.Shell.OpenAuthor();
        drawn.Settle();

        Assert.Equal(4, drawn.Shell.Depth);

        var requests = drawn.Built.Requests;

        drawn.Click(CrumbColumn(drawn, 1), Breadcrumb);
        drawn.Settle();

        Assert.Same(post, drawn.Shell.Screen);
        Assert.Equal(2, drawn.Shell.Depth);
        Assert.Equal(picked, post.Picked?.Id);
        Assert.Equal(reference, post.Reference);
        Assert.Equal(top, drawn.Content.Top);
        Assert.Equal(requests, drawn.Built.Requests);
    }

    /// <summary>
    ///     Clicking the first crumb is clicking the destination shown on the rail (#289), tabbing left waiting and all:
    ///     the two shells end drawn alike, with the same stack and rail, having asked the same of the instance.
    /// </summary>
    [Fact]
    public async Task ClickingTheFirstCrumbIsClickingTheDestinationShown()
    {
        using var railed = await Drilled();
        using var crumbed = await Drilled();

        foreach (var drawn in new[] { railed, crumbed })
        {
            drawn.Press(Key.Tab);
            drawn.Press(Key.Tab);
        }

        railed.Click(OverRail, Array.FindIndex(railed.Rail(), row => row.Contains("Home", StringComparison.Ordinal)));
        crumbed.Click(CrumbColumn(crumbed, 0), Breadcrumb);

        railed.Built.Host.Settle();
        railed.Redraw();
        crumbed.Built.Host.Settle();
        crumbed.Redraw();

        Assert.Equal(["Home"], crumbed.Shell.Crumbs);
        Assert.Equal(railed.Shell.Crumbs, crumbed.Shell.Crumbs);
        Assert.Equal(railed.Rows(), crumbed.Rows());
        Assert.Equal(railed.Shell.Rail.Cursor, crumbed.Shell.Rail.Cursor);
        Assert.Equal(railed.Shell.Rail.Current, crumbed.Shell.Rail.Current);
        Assert.Equal(railed.Built.Requests, crumbed.Built.Requests);
        Assert.Equal(0, crumbed.Built.Host.Waiting);
    }

    /// <summary>
    ///     On a trail too long for the room it has, which is elided from the left, each crumb still drawn walks back to
    ///     its own depth — whatever was elided in front of it — and the <c>… ›</c> lead does nothing.
    /// </summary>
    [Fact]
    public async Task OnAnElidedTrailTheCrumbsDrawnWalkBackToTheirOwnDepth()
    {
        using var probe = await Elided();

        var row = probe.Rows()[Breadcrumb];
        var drawn = Enumerable.Range(0, probe.Shell.Depth - 1)
                              .Where(at => row.IndexOf(probe.Shell.Crumbs[at], RailLines.Width, StringComparison.Ordinal) >= 0)
                              .ToList();

        Assert.Contains("… › ", row, StringComparison.Ordinal);
        Assert.NotEmpty(drawn);
        Assert.DoesNotContain(0, drawn);

        foreach (var at in drawn)
        {
            using var elided = await Elided();

            elided.Click(CrumbColumn(elided, at), Breadcrumb);
            elided.Settle();

            Assert.Equal(at + 1, elided.Shell.Depth);
        }

        var depth = probe.Shell.Depth;
        var screen = probe.Shell.Screen;

        probe.Click(row.IndexOf('…', RailLines.Width), Breadcrumb);
        probe.Settle();

        Assert.Same(screen, probe.Shell.Screen);
        Assert.Equal(depth, probe.Shell.Depth);
    }

    /// <summary>
    ///     The crumb in front, a separator, the corner and the room after the trail are no crumbs to walk back to: the
    ///     stack, the screen and what is drawn are left as they were.
    /// </summary>
    [Theory]
    [InlineData("current")]
    [InlineData("separator")]
    [InlineData("corner")]
    [InlineData("after")]
    public async Task WhatIsNoAncestorCrumbDoesNothing(string over)
    {
        using var drawn = await Drilled();

        var row = drawn.Rows()[Breadcrumb];
        var column = over switch
        {
            "current" => CrumbColumn(drawn, drawn.Shell.Depth - 1),
            "separator" => row.IndexOf('›', RailLines.Width),
            "corner" => RailLines.Width,
            _ => row.Length - 3,
        };

        var screen = drawn.Shell.Screen;
        var rows = drawn.Rows();
        var requests = drawn.Built.Requests;

        drawn.Click(column, Breadcrumb);
        drawn.Settle();

        Assert.Same(screen, drawn.Shell.Screen);
        Assert.Equal(rows, drawn.Rows());
        Assert.Equal(requests, drawn.Built.Requests);
    }

    /// <summary>The fetch mark spinning at the end of the trail is no crumb either.</summary>
    [Fact]
    public async Task TheSpinnerDoesNothing()
    {
        var held = new TaskCompletionSource<Fetch<Post>>();
        var holding = false;

        // Home's posts and the author's are read; the refresh after them is held.
        var built = Four();

        built.Timelines = FakeTimelineReader.Awaiting(_ => holding
            ? held.Task
            : Task.FromResult(Fetch<Post>.Complete([APost.With(id: "110", account: "ben@hachyderm.io")])));

        using var drawn = await DrawnShell.Of(80, Tall, Themes.Plain, built);

        await drawn.Shell.OpenAuthor();
        drawn.Settle();

        holding = true;

        var refreshing = drawn.Shell.Refresh();

        drawn.Built.Host.Settle();
        drawn.Redraw();

        Assert.True(drawn.Shell.SpinnerFrame > 0);

        var screen = drawn.Shell.Screen;
        var edge = ChromeLines.Breadcrumb(drawn.Shell.Crumbs, drawn.Shell.SpinnerFrame, drawn.Content.Frame.Width);
        var spinner = edge.Spans.ToList().FindIndex(span => span.Role == Role.Spinner);
        var mark = RailLines.Width + edge.Spans.Take(spinner + 1).Sum(span => span.Width) - 1;

        Assert.True(mark > RailLines.Width);

        drawn.Click(mark, Breadcrumb);

        Assert.Same(screen, drawn.Shell.Screen);
        Assert.Equal(2, drawn.Shell.Depth);

        held.SetResult(Fetch<Post>.Complete([APost.With(id: "111")]));
        await refreshing;
    }

    /// <summary>
    ///     Walking back drops what the screens taken off asked for, as any pop does: an answer landing after the click
    ///     lands on nothing, and the screen walked back to stays in front.
    /// </summary>
    [Fact]
    public async Task WalkingBackDropsWhatTheScreensTakenOffAskedFor()
    {
        var held = new TaskCompletionSource<Fetch<Post>>();
        var holding = false;

        // Home's posts and the author's are read; the refresh after them is held.
        var built = Four();

        built.Timelines = FakeTimelineReader.Awaiting(_ => holding
            ? held.Task
            : Task.FromResult(Fetch<Post>.Complete([APost.With(id: "110", account: "ben@hachyderm.io")])));

        using var drawn = await DrawnShell.Of(80, Tall, Themes.Plain, built);

        var home = drawn.Shell.Screen;

        await drawn.Shell.OpenAuthor();
        drawn.Settle();

        holding = true;

        var refreshing = drawn.Shell.Refresh();

        drawn.Built.Host.Drain();

        drawn.Click(CrumbColumn(drawn, 0), Breadcrumb);

        held.SetResult(Fetch<Post>.Complete([APost.With(id: "111")]));
        await refreshing;
        drawn.Settle();

        Assert.Same(home, drawn.Shell.Screen);
        Assert.Equal(["Home"], drawn.Shell.Crumbs);
    }

    /// <summary>
    ///     With a compose on the stack, a crumb whose walk back would take it off does nothing and the draft stands; a
    ///     crumb above it — the compose itself, under the keys screen opened over it — still walks back.
    /// </summary>
    [Fact]
    public async Task ACrumbWalkingBackPastACompositionDoesNothing()
    {
        using var drawn = await Drilled(author: false);

        drawn.Shell.Compose();
        drawn.Redraw();

        var compose = Assert.IsType<ComposeScreen>(drawn.Shell.Screen);
        var editor = drawn.Window.SubViews.OfType<ComposeEditor>().Single();

        // Held on the screen as well as in the editor, as it is once the editor has given it up to the keys screen.
        compose.Text = editor.Text = "A draft about sheep";
        drawn.Redraw();

        drawn.Shell.Help();
        drawn.Redraw();

        Assert.Equal(4, drawn.Shell.Depth);

        foreach (var below in new[] { 0, 1 })
        {
            drawn.Click(CrumbColumn(drawn, below), Breadcrumb);
            drawn.Settle();

            Assert.IsType<HelpScreen>(drawn.Shell.Screen);
            Assert.Equal(4, drawn.Shell.Depth);
        }

        drawn.Click(CrumbColumn(drawn, 2), Breadcrumb);
        drawn.Settle();

        Assert.Same(compose, drawn.Shell.Screen);
        Assert.Equal("A draft about sheep", editor.Text);

        drawn.Click(CrumbColumn(drawn, 0), Breadcrumb);
        drawn.Settle();

        Assert.Same(compose, drawn.Shell.Screen);
        Assert.Equal("A draft about sheep", editor.Text);
    }

    /// <summary>
    ///     A double click on a crumb is its first click's walk back and nothing more: the screen it lands on is not
    ///     <c>⏎</c>ed, so the post picked on Home is not opened again.
    /// </summary>
    [Fact]
    public async Task ADoubleClickOnACrumbWalksBackOnceAndDoesNotEnter()
    {
        using var drawn = await Drilled();

        var requests = drawn.Built.Requests;

        drawn.DoubleClick(CrumbColumn(drawn, 0), Breadcrumb);
        drawn.Settle();

        Assert.IsType<FeedScreen>(drawn.Shell.Screen);
        Assert.Equal(["Home"], drawn.Shell.Crumbs);
        Assert.Equal(requests, drawn.Built.Requests);
    }

    /// <summary>
    ///     Open questions win: with a confirmation asked, a click on a crumb declines it and walks nowhere.
    /// </summary>
    [Fact]
    public async Task AClickOnACrumbDeclinesAnOpenConfirmationAndWalksNowhere()
    {
        var answers = Enumerable.Range(1, 3).Select(at => APost.AnAnswer($"Answer {at}", at)).ToList();

        using var drawn = await DrawnShell.Of(
            80,
            Tall,
            Themes.Plain,
            new AShell
            {
                Timelines = FakeTimelineReader.Holding(APost.With(id: "110", poll: APost.APoll(options: answers))),
            });

        drawn.Press(Key.Enter);
        drawn.Settle();
        drawn.Press(new Key('2'));
        drawn.Press(Key.V);

        Assert.NotNull(drawn.Shell.Asking);

        var screen = drawn.Shell.Screen;

        drawn.Click(CrumbColumn(drawn, 0), Breadcrumb);
        drawn.Settle();

        Assert.Null(drawn.Shell.Asking);
        Assert.Same(screen, drawn.Shell.Screen);
        Assert.Empty(drawn.Built.Engagement.Votes);
    }

    /// <summary>And the filter prompt: a click on a crumb closes it, with what was typed still narrowing, and walks nowhere.</summary>
    [Fact]
    public async Task AClickOnACrumbClosesTheFilterPromptAndWalksNowhere()
    {
        using var drawn = await On("follows");

        drawn.Press(Key.F);
        drawn.Press(new Key('a'));

        var follows = Assert.IsType<FollowsScreen>(drawn.Shell.Screen);

        Assert.True(follows.IsTyping);

        drawn.Click(CrumbColumn(drawn, 0), Breadcrumb);
        drawn.Settle();

        Assert.Same(follows, drawn.Shell.Screen);
        Assert.False(follows.IsTyping);
        Assert.Equal("a", follows.Filter);
    }

    /// <summary>The <c>?</c> screen's mouse line says a crumb walks back.</summary>
    [Fact]
    public async Task TheHelpScreenSaysACrumbWalksBack()
    {
        using var drawn = await DrawnShell.Of(80, Tall, Themes.Plain);

        drawn.Press(new Key('?'));

        Assert.Contains(drawn.Rows(), row => row.Contains("click a crumb to walk back to it", StringComparison.Ordinal));
    }

    /// <summary>
    ///     The column the <paramref name="at" />th crumb of the stack is drawn at on the breadcrumb: its first, found
    ///     after the crumbs drawn in front of it.
    /// </summary>
    private static int CrumbColumn(DrawnShell drawn, int at)
    {
        var row = drawn.Rows()[Breadcrumb];
        var from = RailLines.Width;

        for (var crumb = 0; crumb <= at; crumb++)
        {
            var found = row.IndexOf(drawn.Shell.Crumbs[crumb], from, StringComparison.Ordinal);

            if (found < 0)
            {
                continue;
            }

            if (crumb == at)
            {
                return found;
            }

            from = found + 1;
        }

        Assert.Fail($"Crumb {at} ({drawn.Shell.Crumbs[at]}) is not drawn:\n{row}");

        return -1;
    }

    /// <summary>Four posts on Home, by people other than the reader.</summary>
    private static AShell Four() => new()
    {
        Timelines = FakeTimelineReader.Holding(
            APost.With(id: "110", account: "ben@hachyderm.io"),
            APost.With(id: "220"),
            APost.With(id: "330"),
            APost.With(id: "440")),
    };

    /// <summary>Home and a post on it with a hashtag, opened on a thread of the post with two replies under it.</summary>
    private static AShell Hashtagged() => new()
    {
        Timelines = FakeTimelineReader.Holding(
            APost.With(id: "110", content: "<p>Shearing day</p>"),
            APost.With(id: "220")),
        Engagement = FakePostEngagement.Answered(
            APost.With(id: "110", content: "<p>Shearing day</p>"),
            APost.With(id: "120", content: "<p>Same here #sheep</p>", account: "ben@hachyderm.io"),
            APost.With(id: "130", content: "A third")),
    };

    /// <summary>The shell drawn tall and drilled from Home into the picked post, and on into its author where so.</summary>
    private static async Task<DrawnShell> Drilled(bool author = true)
    {
        var drawn = await DrawnShell.Of(80, Tall, Themes.Plain, Four());

        drawn.Press(Key.Enter);
        drawn.Settle();

        if (author)
        {
            await drawn.Shell.OpenAuthor();
            drawn.Settle();
        }

        return drawn;
    }

    /// <summary>
    ///     The shell drilled deep enough that the trail is elided: into a post, its author, and who they follow.
    /// </summary>
    private static async Task<DrawnShell> Elided()
    {
        var built = new AShell
        {
            Timelines = FakeTimelineReader.Holding(APost.With(id: "110", account: "ben@hachyderm.io")),
            Accounts = FakeAccountRelationships.Holding(
                AnAccount.With(address: "ben@hachyderm.io"),
                [AnAccount.With(address: "maria@here.social")]),
        };

        var drawn = await DrawnShell.Of(80, Tall, Themes.Plain, built);

        drawn.Press(Key.Enter);
        drawn.Settle();
        await drawn.Shell.OpenAuthor();
        drawn.Settle();
        drawn.Press(Key.W);
        drawn.Settle();

        return drawn;
    }
}
