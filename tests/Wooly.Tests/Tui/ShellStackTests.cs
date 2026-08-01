using Wooly.Core.Posts;
using Wooly.Tests.Fakes;
using Wooly.Tui.Screens;
using Wooly.Tui.Shell;

namespace Wooly.Tests.Tui;

/// <summary>
///     Drilling in and walking back out: what <c>⏎</c>, <c>a</c> and <c>esc</c> do to the stack, and what the
///     breadcrumb says while you are down there. A screen is a place you go rather than a window over what you were
///     reading (ADR-0014), so these are facts about a stack and need no terminal.
/// </summary>
public class ShellStackTests
{
    /// <summary>Enter on a feed item opens that post with what has been said in answer to it.</summary>
    [Fact]
    public async Task Enter_OpensThePickedPostWithItsReplies()
    {
        var shell = new AShell
        {
            Timelines = FakeTimelineReader.Holding(APost.With(id: "110", account: "ben@hachyderm.io")),
            Engagement = FakePostEngagement.Answered(APost.With(id: "110"), APost.With(id: "111", content: "Yes")),
        };

        var opened = await shell.Opened();

        await opened.Enter();

        var post = Assert.IsType<PostScreen>(opened.Screen);
        Assert.Equal("110", post.Post.Id);
        Assert.Equal(["111"], post.Replies.Select(reply => reply.Id));
        Assert.Equal("110", Assert.Single(shell.Engagement.RepliesRead).PostId);
    }

    /// <summary>The trail along the top is the stack, which is the whole of how somebody knows where they are.</summary>
    [Fact]
    public async Task Breadcrumb_SaysWhereInTheStackYouAre()
    {
        var shell = new AShell
        {
            Timelines = FakeTimelineReader.Holding(APost.With(id: "110", account: "ben@hachyderm.io")),
            Accounts = FakeAccountRelationships.Holding(AnAccount.With(address: "ben@hachyderm.io")),
        };

        var opened = await shell.Opened();

        Assert.Equal("home", opened.Breadcrumb);

        await opened.Enter();

        Assert.Equal("home › post by @ben@hachyderm.io", opened.Breadcrumb);

        await opened.OpenAuthor();

        Assert.Equal("home › post by @ben@hachyderm.io › @ben@hachyderm.io", opened.Breadcrumb);
    }

    /// <summary><c>a</c> opens whoever wrote it: who they are, where you stand with them, and what they have posted.</summary>
    [Fact]
    public async Task OpenAuthor_OpensTheAccountThatWroteThePickedPost()
    {
        var shell = new AShell
        {
            Timelines = FakeTimelineReader.Holding(APost.With(id: "110", account: "ben@hachyderm.io")),
            Accounts = FakeAccountRelationships.Holding(AnAccount.With(address: "ben@hachyderm.io")),
        };

        var opened = await shell.Opened();

        await opened.OpenAuthor();

        var account = Assert.IsType<AccountScreen>(opened.Screen);
        Assert.Equal("ben@hachyderm.io", account.Account.Address);
        Assert.Equal("ben@hachyderm.io", Assert.Single(shell.Accounts.Reads).Account.Text);

        // Their posts are a timeline like any other, read through the same port.
        Assert.Contains(shell.Timelines.Reads, read => read.Timeline.Account?.Text == "ben@hachyderm.io");
    }

    /// <summary>A boost is somebody passing a post on, so its author is whoever wrote the post rather than who boosted it.</summary>
    [Fact]
    public async Task OpenAuthor_OpensWhoWroteAPostRatherThanWhoBoostedIt()
    {
        var shell = new AShell
        {
            Timelines = FakeTimelineReader.Holding(APost.With(
                id: "110",
                account: "jeff@mastodon.social",
                boosted: APost.With(id: "99", account: "hazel@mastodon.art"))),
            Accounts = FakeAccountRelationships.Holding(AnAccount.With(address: "hazel@mastodon.art")),
        };

        var opened = await shell.Opened();

        await opened.OpenAuthor();

        Assert.Equal("hazel@mastodon.art", Assert.Single(shell.Accounts.Reads).Account.Text);
    }

    /// <summary><c>esc</c> walks back up one level, and it never quits.</summary>
    [Fact]
    public async Task Back_WalksUpOneLevelAndNeverOffTheEnd()
    {
        var shell = new AShell
        {
            Timelines = FakeTimelineReader.Holding(APost.With(id: "110", account: "ben@hachyderm.io")),
            Accounts = FakeAccountRelationships.Holding(AnAccount.With(address: "ben@hachyderm.io")),
        };

        var opened = await shell.Opened();

        await opened.Enter();
        await opened.OpenAuthor();

        Assert.Equal(3, opened.Depth);

        opened.Back();

        Assert.IsType<PostScreen>(opened.Screen);

        opened.Back();

        Assert.IsType<FeedScreen>(opened.Screen);

        // The bottom of the stack is a destination, and there is nothing under it to walk to.
        opened.Back();

        Assert.Equal(1, opened.Depth);
        Assert.IsType<FeedScreen>(opened.Screen);
    }

    /// <summary>
    ///     Arriving at a destination is arriving somewhere, so what was drilled into from the last one is left behind
    ///     rather than kept underneath.
    /// </summary>
    [Fact]
    public async Task Step_LeavesTheDrillBehindWhenTheRailMovesOn()
    {
        var shell = new AShell();
        var opened = await shell.Opened();

        await opened.Enter();

        Assert.Equal(2, opened.Depth);

        opened.Step(1);
        shell.Host.Settle();

        Assert.Equal(1, opened.Depth);
        Assert.Equal("local", opened.Breadcrumb);
    }

    /// <summary>
    ///     <c>?</c> answers with the keys of whatever is underneath it, which is what every screen a later ticket adds
    ///     inherits for free.
    /// </summary>
    [Fact]
    public async Task Help_ShowsTheKeysOfTheScreenItWasOpenedFrom()
    {
        var shell = new AShell();
        var opened = await shell.Opened();

        opened.Help();

        var help = Assert.IsType<HelpScreen>(opened.Screen);
        var drawn = help.Lines(61, AShell.Now).Select(line => line.Text).ToList();

        Assert.Contains(drawn, line => line.Contains("On home", StringComparison.Ordinal));
        Assert.Contains(drawn, line => line.Contains("boost", StringComparison.Ordinal));

        // The frame's keys are on it too, because they are the ones a reader has to be able to rely on.
        Assert.Contains(drawn, line => line.Contains("ctrl-q", StringComparison.Ordinal));
        Assert.Contains(drawn, line => line.Contains("never quits", StringComparison.Ordinal));

        opened.Back();

        Assert.IsType<FeedScreen>(opened.Screen);
    }

    /// <summary>
    ///     Every key the shell acts on is a key the screen said it answers to. A key that fires without being on the
    ///     status row is a key nobody can find, and the row is the only thing making a keymap that varies workable
    ///     (docs/tui-shell.md).
    /// </summary>
    [Fact]
    public async Task Keys_SayEveryKeyThatActsOnThePickedPost()
    {
        var shell = new AShell
        {
            Timelines = FakeTimelineReader.Holding(APost.With(id: "110", account: "ben@hachyderm.io")),
            Accounts = FakeAccountRelationships.Holding(AnAccount.With(address: "ben@hachyderm.io")),
        };

        var opened = await shell.Opened();
        var acting = PostKeys.OnAPost.Select(key => key.Key).ToList();

        Assert.Subset(opened.Keys.Select(key => key.Key).ToHashSet(), acting.ToHashSet());

        await opened.Enter();

        Assert.Subset(opened.Keys.Select(key => key.Key).ToHashSet(), acting.ToHashSet());

        await opened.OpenAuthor();

        Assert.Subset(opened.Keys.Select(key => key.Key).ToHashSet(), acting.ToHashSet());
    }

    /// <summary>
    ///     <c>/</c> is a frame key rather than a screen's, so it means the same thing everywhere even though what it
    ///     opens onto is #29's.
    /// </summary>
    [Fact]
    public async Task Search_GoesToTheSearchDestinationFromWhereverYouAre()
    {
        var shell = new AShell();
        var opened = await shell.Opened();

        await opened.Enter();
        opened.Search();

        Assert.Equal(DestinationKind.Search, opened.Rail.Showing.Kind);
        Assert.Equal("search", opened.Breadcrumb);
    }

    /// <summary>Asking for help twice is still one screen, not a stack of them.</summary>
    [Fact]
    public async Task Help_DoesNotStackOnItself()
    {
        var shell = new AShell();
        var opened = await shell.Opened();

        opened.Help();
        opened.Help();

        Assert.Equal(2, opened.Depth);
    }

    /// <summary>Inside a post, the post itself is what is picked out first, and j walks down into the answers.</summary>
    [Fact]
    public async Task Move_WalksFromThePostIntoItsReplies()
    {
        var shell = new AShell
        {
            Engagement = FakePostEngagement.Answered(
                APost.With(id: "110"),
                APost.With(id: "111"),
                APost.With(id: "112")),
        };

        var opened = await shell.Opened();

        await opened.Enter();

        Assert.Equal("110", opened.Screen.Picked?.Id);

        opened.Move(1);

        Assert.Equal("111", opened.Screen.Picked?.Id);

        opened.Move(5);

        // Stops at the last answer rather than wrapping: a list you walked off the end of is a list you have lost your
        // place in.
        Assert.Equal("112", opened.Screen.Picked?.Id);
    }

    /// <summary>A post nobody has answered says so, rather than showing a heading over nothing.</summary>
    [Fact]
    public async Task Enter_SaysSoWhereNobodyHasAnsweredThePost()
    {
        var shell = new AShell { Engagement = FakePostEngagement.Answering(APost.With(id: "110")) };
        var opened = await shell.Opened();

        await opened.Enter();

        var drawn = opened.Screen.Lines(61, AShell.Now).Select(line => line.Text);

        Assert.Contains(drawn, line => line.Contains("Nobody has answered this yet", StringComparison.Ordinal));
    }

    /// <summary>Pressing enter on a timeline with nothing on it does nothing at all, rather than failing.</summary>
    [Fact]
    public async Task Enter_DoesNothingWhereThereIsNoPostPickedOut()
    {
        var shell = new AShell { Timelines = FakeTimelineReader.Holding() };
        var opened = await shell.Opened();

        await opened.Enter();

        Assert.Equal(1, opened.Depth);
        Assert.Empty(shell.Engagement.RepliesRead);
    }
}
