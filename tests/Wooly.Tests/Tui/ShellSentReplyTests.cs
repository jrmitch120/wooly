using Terminal.Gui.Input;
using Wooly.Core.Posts;
using Wooly.Tests.Fakes;
using Wooly.Tui.Screens;
using Wooly.Tui.Shell;
using Wooly.Tui.Theme;
using Wooly.Tui.Views;

namespace Wooly.Tests.Tui;

/// <summary>
///     A reply sent from a post screen appearing in its thread as soon as the instance takes it, where the next read
///     would put it, on every post screen on the stack that holds what it answers — and picked only on the screen it
///     was written from (#482), lifted ahead of everyone else's where it answers your own post (#483). Where in a
///     thread it goes is the thread's own (<see cref="Core.PostThreadTakeInTests" />); these are about which screens
///     take it in and what is picked once they have.
/// </summary>
public class ShellSentReplyTests
{
    /// <summary>A post by somebody else answering <paramref name="answered" />.</summary>
    private static Post Answering(string id, string answered) =>
        APost.With(
            id: id,
            account: "maria@fosstodon.org",
            inReplyTo: new PostReplyTarget { PostId = answered, Handle = "jeff@mastodon.social" });

    /// <summary>The reply as the instance published it, answering <paramref name="answered" />.</summary>
    private static Post Sent(string answered) =>
        APost.With(id: "130", content: "Quite", inReplyTo: new PostReplyTarget { PostId = answered });

    /// <summary>Presses <c>r</c> on what is picked, writes something and sends it, and lets the answer land.</summary>
    private static async Task Reply(Shell opened, AShell shell)
    {
        opened.Press(ShellKey.R);
        ((ComposeScreen)opened.Screen).Text += "Quite";

        await opened.Drafting.Send();
        shell.Host.Drain();
    }

    /// <summary>
    ///     A reply to the post the screen is about is the newest answer to it, so it lands after every reply already
    ///     there — and is what is picked, since it is what the reader just said.
    /// </summary>
    [Fact]
    public async Task Send_PutsAReplyToThePostAfterItsRepliesAndPicksIt()
    {
        var shell = new AShell
        {
            Engagement = FakePostEngagement.Answered(
                APost.With(id: "110"),
                Answering("111", "110"),
                Answering("112", "111"),
                Answering("120", "110")),
            Author = FakePostAuthor.Answering(Sent("110")),
        };

        var opened = await shell.Opened();

        await opened.Enter();
        shell.Host.Drain();

        await Reply(opened, shell);

        var post = Assert.IsType<PostScreen>(opened.Screen);

        Assert.Equal(["111", "112", "120", "130"], post.Replies.Select(reply => reply.Id));
        Assert.Equal("130", post.Picked?.Id);
        Assert.Equal("Sent.", opened.Notice);
    }

    /// <summary>
    ///     A reply to one of the replies lands in that reply's part of the thread, after its own answers and ahead of
    ///     the reply after it.
    /// </summary>
    [Fact]
    public async Task Send_PutsAReplyToAReplyAfterThatReplysAnswers()
    {
        var shell = new AShell
        {
            Engagement = FakePostEngagement.Answered(
                APost.With(id: "110"),
                Answering("111", "110"),
                Answering("112", "111"),
                Answering("120", "110")),
            Author = FakePostAuthor.Answering(Sent("111")),
        };

        var opened = await shell.Opened();

        await opened.Enter();
        shell.Host.Drain();

        opened.Move(1);

        Assert.Equal("111", opened.Screen.Picked?.Id);

        await Reply(opened, shell);

        var post = Assert.IsType<PostScreen>(opened.Screen);

        Assert.Equal(["111", "112", "130", "120"], post.Replies.Select(reply => reply.Id));
        Assert.Equal("130", post.Picked?.Id);
    }

    /// <summary>
    ///     A reply to your own post is one the instance lifts ahead of everyone else's answers, so it lands first
    ///     rather than last, where the next <c>g</c> puts it, and is picked there (#483).
    /// </summary>
    [Fact]
    public async Task Send_LiftsAReplyToYourOwnPostAheadOfEveryoneElsesReplies()
    {
        var shell = new AShell
        {
            Engagement = FakePostEngagement.Answered(
                APost.With(id: "110"),
                Answering("111", "110"),
                Answering("112", "111"),
                Answering("120", "110")),
            Author = FakePostAuthor.Answering(APost.With(
                id: "130",
                content: "Quite",
                inReplyTo: new PostReplyTarget { PostId = "110", Handle = "jeff@mastodon.social" })),
        };

        var opened = await shell.Opened();

        await opened.Enter();
        shell.Host.Drain();

        await Reply(opened, shell);

        var post = Assert.IsType<PostScreen>(opened.Screen);

        Assert.Equal(["130", "111", "112", "120"], post.Replies.Select(reply => reply.Id));
        Assert.Equal("130", post.Picked?.Id);
    }

    /// <summary>
    ///     A reply to something above the post is in a thread around something else, so the screen stays the thread
    ///     around its own post — what <c>g</c> would show — and only the status row says the reply went out.
    /// </summary>
    [Fact]
    public async Task Send_LeavesAReplyToAnAncestorOffThePostScreen()
    {
        var shell = new AShell
        {
            Engagement = FakePostEngagement.Threaded(
                APost.With(id: "110"),
                [APost.With(id: "100", account: "ben@hachyderm.io")],
                Answering("111", "110")),
            Author = FakePostAuthor.Answering(Sent("100")),
        };

        var opened = await shell.Opened();

        await opened.Enter();
        shell.Host.Drain();

        opened.Move(-1);

        Assert.Equal("100", opened.Screen.Picked?.Id);

        await Reply(opened, shell);

        var post = Assert.IsType<PostScreen>(opened.Screen);

        Assert.Equal(["100"], post.Ancestors.Select(ancestor => ancestor.Id));
        Assert.Equal(["111"], post.Replies.Select(reply => reply.Id));
        Assert.Equal("100", post.Picked?.Id);
        Assert.Equal("Sent.", opened.Notice);
    }

    /// <summary>
    ///     A post screen further down the stack that holds what the reply answers takes it in at the same place, so
    ///     walking back out shows it — with its pick left on the reply the reader walked in from rather than moved to
    ///     what they said.
    /// </summary>
    [Fact]
    public async Task Send_PutsTheReplyOnAPostScreenFurtherDownWithoutMovingItsPick()
    {
        var shell = new AShell
        {
            Engagement = FakePostEngagement.Threading(around => around switch
            {
                "111" => new PostThread([APost.With(id: "110")], [Answering("112", "111")]),
                _ => new PostThread([], [Answering("111", "110"), Answering("112", "111"), Answering("120", "110")]),
            }),
            Author = FakePostAuthor.Answering(Sent("111")),
        };

        var opened = await shell.Opened();

        await opened.Enter();
        shell.Host.Drain();

        opened.Move(1);
        await opened.Enter();
        shell.Host.Drain();

        Assert.Equal("111", Assert.IsType<PostScreen>(opened.Screen).Post.Id);

        await Reply(opened, shell);

        var front = Assert.IsType<PostScreen>(opened.Screen);

        Assert.Equal(["112", "130"], front.Replies.Select(reply => reply.Id));
        Assert.Equal("130", front.Picked?.Id);

        opened.Back();

        var beneath = Assert.IsType<PostScreen>(opened.Screen);

        Assert.Equal("110", beneath.Post.Id);
        Assert.Equal(["111", "112", "130", "120"], beneath.Replies.Select(reply => reply.Id));
        Assert.Equal("111", beneath.Picked?.Id);
    }

    /// <summary>
    ///     A reader who walked away before the send landed — out of the compose and out of the post screen it was
    ///     written from — still finds the reply on the post screen still on the stack that holds what it answers, with
    ///     its pick where they left it, since it is not the screen the reply was written from.
    /// </summary>
    [Fact]
    public async Task Send_PutsTheReplyOnAPostScreenStillOnTheStackWhereTheReaderWalkedAwayBeforeItLanded()
    {
        var shell = new AShell
        {
            Engagement = FakePostEngagement.Threading(around => around switch
            {
                "111" => new PostThread([APost.With(id: "110")], [Answering("112", "111")]),
                _ => new PostThread([], [Answering("111", "110"), Answering("112", "111"), Answering("120", "110")]),
            }),
            Author = FakePostAuthor.Answering(Sent("111")),
        };

        shell.Author.HoldingPublishes = true;

        var opened = await shell.Opened();

        await opened.Enter();
        shell.Host.Drain();

        opened.Move(1);
        await opened.Enter();
        shell.Host.Drain();

        opened.Press(ShellKey.R);
        ((ComposeScreen)opened.Screen).Text += "Quite";

        var sending = opened.Drafting.Send();

        while (opened.Screen is ComposeScreen || opened.Asking is not null)
        {
            if (opened.Asking is not null)
            {
                await opened.Answer(agreed: true);
            }
            else
            {
                opened.Back();
            }
        }

        opened.Back();

        var beneath = Assert.IsType<PostScreen>(opened.Screen);

        Assert.Equal("110", beneath.Post.Id);

        shell.Author.HeldPublishes.Single().Land();
        await sending;
        shell.Host.Drain();

        Assert.Same(beneath, opened.Screen);
        Assert.Equal(["111", "112", "130", "120"], beneath.Replies.Select(reply => reply.Id));
        Assert.Equal("111", beneath.Picked?.Id);
        Assert.Equal("Sent.", opened.Notice);
    }

    /// <summary>
    ///     An edit of the reply afterwards puts the edited post in place of it on every screen that took it in, and a
    ///     delete takes it off every one of them, as they do for any other post (#482).
    /// </summary>
    [Fact]
    public async Task Edit_PutsTheEditedReplyInPlaceOnEveryScreenThatTookItIn()
    {
        var shell = new AShell
        {
            Engagement = FakePostEngagement.Threading(around => around switch
            {
                "111" => new PostThread([APost.With(id: "110")], [Answering("112", "111")]),
                _ => new PostThread([], [Answering("111", "110"), Answering("112", "111"), Answering("120", "110")]),
            }),
            Author = FakePostAuthor.Answering(Sent("111")),
        };

        var opened = await shell.Opened();

        await opened.Enter();
        shell.Host.Drain();

        opened.Move(1);
        await opened.Enter();
        shell.Host.Drain();

        await Reply(opened, shell);

        Assert.Equal("130", opened.Screen.Picked?.Id);

        shell.Author.Edited = Sent("111") with { Content = "Quite so" };

        opened.Press(ShellKey.E);
        ((ComposeScreen)opened.Screen).Text += " so";

        await opened.Drafting.Send();
        shell.Host.Drain();

        var front = Assert.IsType<PostScreen>(opened.Screen);

        Assert.Equal(["112", "130"], front.Replies.Select(reply => reply.Id));
        Assert.Equal("Quite so", front.Replies[1].Content);

        opened.Back();

        var beneath = Assert.IsType<PostScreen>(opened.Screen);

        Assert.Equal(["111", "112", "130", "120"], beneath.Replies.Select(reply => reply.Id));
        Assert.Equal("Quite so", beneath.Replies[2].Content);
    }

    /// <inheritdoc cref="Edit_PutsTheEditedReplyInPlaceOnEveryScreenThatTookItIn" />
    [Fact]
    public async Task Delete_TakesTheReplyOffEveryScreenThatTookItIn()
    {
        var shell = new AShell
        {
            Engagement = FakePostEngagement.Threading(around => around switch
            {
                "111" => new PostThread([APost.With(id: "110")], [Answering("112", "111")]),
                _ => new PostThread([], [Answering("111", "110"), Answering("112", "111"), Answering("120", "110")]),
            }),
            Author = FakePostAuthor.Answering(Sent("111")),
        };

        var opened = await shell.Opened();

        await opened.Enter();
        shell.Host.Drain();

        opened.Move(1);
        await opened.Enter();
        shell.Host.Drain();

        await Reply(opened, shell);

        Assert.Equal("130", opened.Screen.Picked?.Id);

        opened.AskToDelete();
        await opened.Answer(agreed: true);
        shell.Host.Drain();

        var front = Assert.IsType<PostScreen>(opened.Screen);

        Assert.Equal(["112"], front.Replies.Select(reply => reply.Id));

        opened.Back();

        var beneath = Assert.IsType<PostScreen>(opened.Screen);

        Assert.Equal(["111", "112", "120"], beneath.Replies.Select(reply => reply.Id));
    }

    /// <summary>
    ///     A feed is left as it was by a reply to something on it: a refresh is how a feed shows what has arrived, and
    ///     a reply put in where the reader is reading would be a post the next read puts somewhere else.
    /// </summary>
    [Fact]
    public async Task Send_LeavesAFeedAsItWas()
    {
        var shell = new AShell
        {
            Timelines = FakeTimelineReader.Holding(APost.With(id: "110"), APost.With(id: "100")),
            Author = FakePostAuthor.Answering(Sent("110")),
        };

        var opened = await shell.Opened();

        await Reply(opened, shell);

        var feed = Assert.IsType<FeedScreen>(opened.Screen);

        Assert.Equal(["110", "100"], feed.Posts.Select(post => post.Id));
        Assert.Equal("110", feed.Picked?.Id);
    }

    /// <summary>
    ///     The page follows the pick to the reply, so one landing well below the rows on show is brought into view —
    ///     even where the reader had walked the page away from the pick before replying. The screen beneath, walked
    ///     back to, is where it was left.
    /// </summary>
    /// <remarks>
    ///     Real frames, because the row a page begins on is settled inside the draw (<see cref="ShellResumeTests" />).
    /// </remarks>
    [Fact]
    public async Task Send_BringsTheReplyIntoViewOnlyOnTheScreenItWasWrittenFrom()
    {
        var replies = Enumerable.Range(1, 6).Select(at => Answering($"11{at}", "111")).ToArray();

        var shell = new AShell
        {
            Engagement = FakePostEngagement.Threading(around => around switch
            {
                "111" => new PostThread([APost.With(id: "110")], replies),
                _ => new PostThread([], [Answering("111", "110"), .. replies, Answering("120", "110")]),
            }),
            Author = FakePostAuthor.Answering(Sent("111")),
        };

        var opened = await shell.Opened();

        using var window = new ShellWindow(opened, Themes.Plain, shell.Clock, () => { }, new FakePictures())
        {
            Width = 80,
            Height = 20,
        };

        window.Layout();

        var content = window.SubViews.OfType<PaintedView>().Single(view => view.Id == ShellWindow.ContentId);

        window.Draw();

        window.NewKeyDownEvent(Key.Enter);
        shell.Host.Drain();
        window.Draw();

        window.NewKeyDownEvent(Key.K);
        window.Draw();

        Assert.Equal("111", opened.Screen.Picked?.Id);

        var beneath = content.Top;

        window.NewKeyDownEvent(Key.Enter);
        shell.Host.Drain();
        window.Draw();

        Assert.Equal("111", Assert.IsType<PostScreen>(opened.Screen).Post.Id);

        // Walked a row away from the pick, which leaves it on the page but the page no longer following it.
        window.NewKeyDownEvent(Key.CursorDown);
        window.Draw();

        var walked = content.Top;

        Assert.Null(content.Reclaimable);

        window.NewKeyDownEvent(Key.R);
        window.Draw();

        ((ComposeScreen)opened.Screen).Text += "Quite";

        await opened.Drafting.Send();
        shell.Host.Drain();
        window.Draw();

        Assert.Equal("130", opened.Screen.Picked?.Id);
        Assert.Null(content.Reclaimable);
        Assert.True(content.Top > walked);

        window.NewKeyDownEvent(Key.Esc);
        window.Draw();

        Assert.Equal("111", opened.Screen.Picked?.Id);
        Assert.Equal(beneath, content.Top);
    }
}
