using Wooly.Core.Posts;
using Wooly.Tests.Fakes;
using Wooly.Tui.Screens;

namespace Wooly.Tests.Tui;

/// <summary>
///     What the shell does to a post, through the same ports the CLI's commands use (ADR-0005). Every one of these is
///     a decision — which mark to send, whether the post is the profile's own, whether a delete has been agreed to —
///     and none of them is about drawing.
/// </summary>
public class ShellActionTests
{
    private static readonly Post Mine = APost.With(id: "110", account: "jeff@mastodon.social");

    private static readonly Post Somebody = APost.With(id: "220", account: "ben@hachyderm.io");

    /// <summary>
    ///     A mark is put on or taken off depending on what the post already carries — which is the whole reason a post
    ///     carries the reader's own marks (ADR-0014).
    /// </summary>
    [Theory]
    [InlineData(PostMark.Boost)]
    [InlineData(PostMark.Favorite)]
    public async Task Mark_PutsAMarkOnAPostThatDoesNotCarryIt(PostMark mark)
    {
        var shell = new AShell { Timelines = FakeTimelineReader.Holding(Somebody) };
        var opened = await shell.Opened();

        await opened.Mark(mark);

        var marked = Assert.Single(shell.Engagement.Marks);
        Assert.Equal("220", marked.PostId);
        Assert.Equal(mark, marked.Mark);
        Assert.True(marked.Wanted);
    }

    [Theory]
    [InlineData(PostMark.Boost)]
    [InlineData(PostMark.Favorite)]
    public async Task Mark_TakesAMarkOffAPostThatAlreadyCarriesIt(PostMark mark)
    {
        var already = APost.With(
            id: "220",
            account: "ben@hachyderm.io",
            marks: APost.Marked(boosted: true, favorited: true));

        var shell = new AShell { Timelines = FakeTimelineReader.Holding(already) };
        var opened = await shell.Opened();

        await opened.Mark(mark);

        Assert.False(Assert.Single(shell.Engagement.Marks).Wanted);
    }

    /// <summary>
    ///     A star that only lit up after the whole timeline had been fetched again would make the key feel broken, so
    ///     the post the instance answered with replaces the copy the screen is holding.
    /// </summary>
    [Fact]
    public async Task Mark_DrawsThePostAsTheInstanceNowHasIt()
    {
        var favorited = APost.With(id: "220", account: "ben@hachyderm.io", marks: APost.Marked(favorited: true));

        var shell = new AShell
        {
            Timelines = FakeTimelineReader.Holding(Somebody),
            Engagement = FakePostEngagement.Answering(favorited),
        };

        var opened = await shell.Opened();

        await opened.Mark(PostMark.Favorite);

        Assert.True(opened.Screen.Picked?.Marks.Favorited);
    }

    /// <summary>Pinning is for an account's own posts, so the shell says so rather than letting the instance refuse it.</summary>
    [Fact]
    public async Task Mark_RefusesToPinSomebodyElsesPost()
    {
        var shell = new AShell { Timelines = FakeTimelineReader.Holding(Somebody) };
        var opened = await shell.Opened();

        await opened.Mark(PostMark.Pin);

        Assert.Empty(shell.Engagement.Marks);
        Assert.Contains("your own posts", opened.Notice);
        Assert.True(opened.NoticeIsError);
    }

    [Fact]
    public async Task Mark_PinsTheProfilesOwnPost()
    {
        var shell = new AShell { Timelines = FakeTimelineReader.Holding(Mine) };
        var opened = await shell.Opened();

        await opened.Mark(PostMark.Pin);

        Assert.Equal(PostMark.Pin, Assert.Single(shell.Engagement.Marks).Mark);
    }

    /// <summary>
    ///     Story 43: the one thing here whose effect nothing else undoes, so nothing is taken down until it has been
    ///     said twice.
    /// </summary>
    [Fact]
    public async Task AskToDelete_TakesNothingDownUntilItIsAgreedTo()
    {
        var shell = new AShell { Timelines = FakeTimelineReader.Holding(Mine) };
        var opened = await shell.Opened();

        opened.AskToDelete();

        Assert.NotNull(opened.Asking);
        Assert.Contains("cannot be undone", opened.Asking.Question);
        Assert.Empty(shell.Author.Deletions);

        await opened.Answer(agreed: true);

        Assert.Null(opened.Asking);
        Assert.Equal("110", Assert.Single(shell.Author.Deletions).PostId);
    }

    /// <summary>Answering no leaves the post exactly where it was, which is the point of asking.</summary>
    [Fact]
    public async Task Answer_LeavesThePostAloneWhereTheDeleteIsNotAgreedTo()
    {
        var shell = new AShell { Timelines = FakeTimelineReader.Holding(Mine) };
        var opened = await shell.Opened();

        opened.AskToDelete();
        await opened.Answer(agreed: false);

        Assert.Null(opened.Asking);
        Assert.Empty(shell.Author.Deletions);
    }

    /// <summary>Escaping out of the question is answering it, and the answer is no.</summary>
    [Fact]
    public async Task Back_AnswersAConfirmationRatherThanWalkingUpTheStack()
    {
        var shell = new AShell { Timelines = FakeTimelineReader.Holding(Mine) };
        var opened = await shell.Opened();

        await opened.Enter();
        opened.AskToDelete();

        opened.Back();

        Assert.Null(opened.Asking);
        Assert.Empty(shell.Author.Deletions);

        // Still on the post: the escape answered the question rather than leaving the screen.
        Assert.IsType<PostScreen>(opened.Screen);
    }

    [Fact]
    public async Task AskToDelete_RefusesSomebodyElsesPostWithoutAsking()
    {
        var shell = new AShell { Timelines = FakeTimelineReader.Holding(Somebody) };
        var opened = await shell.Opened();

        opened.AskToDelete();

        Assert.Null(opened.Asking);
        Assert.Contains("your own posts", opened.Notice);
    }

    /// <summary>A post that has been taken down leaves the timeline it was on.</summary>
    [Fact]
    public async Task Answer_TakesTheDeletedPostOffTheScreensHoldingIt()
    {
        var shell = new AShell { Timelines = FakeTimelineReader.Holding(Mine, Somebody) };
        var opened = await shell.Opened();

        opened.AskToDelete();
        await opened.Answer(agreed: true);

        var feed = Assert.IsType<FeedScreen>(opened.Screen);
        Assert.Equal(["220"], feed.Posts.Select(post => post.Id));
    }

    /// <summary>Compose opens an editor with nothing in it, and sending publishes what was written.</summary>
    [Fact]
    public async Task Send_PublishesWhatWasComposed()
    {
        var shell = new AShell();
        var opened = await shell.Opened();

        opened.Compose();

        var compose = Assert.IsType<ComposeScreen>(opened.Screen);
        Assert.Equal(ComposeFor.Post, compose.Purpose);

        compose.Text = "Hello from the rail";

        await opened.Send();

        var published = Assert.Single(shell.Author.Published);
        Assert.Equal("Hello from the rail", published.Draft.Text);
        Assert.Null(published.Draft.InReplyTo);

        // The editor is closed once what was in it has gone out.
        Assert.IsType<FeedScreen>(opened.Screen);
    }

    /// <summary>A reply is a draft that names what it answers, which is what the port already takes.</summary>
    [Fact]
    public async Task Send_RepliesToThePickedPost()
    {
        var shell = new AShell { Timelines = FakeTimelineReader.Holding(Somebody) };
        var opened = await shell.Opened();

        opened.Reply();

        var compose = Assert.IsType<ComposeScreen>(opened.Screen);
        compose.Text = "Answering you";

        await opened.Send();

        Assert.Equal("220", Assert.Single(shell.Author.Published).Draft.InReplyTo);
    }

    /// <summary>Editing starts from what the post already says, because an edit is a change rather than a rewrite.</summary>
    [Fact]
    public async Task Send_SavesAnEditOfTheProfilesOwnPost()
    {
        var shell = new AShell { Timelines = FakeTimelineReader.Holding(Mine) };
        var opened = await shell.Opened();

        opened.Edit();

        var compose = Assert.IsType<ComposeScreen>(opened.Screen);
        Assert.Equal("Hello world", compose.Text);

        compose.Text = "Hello world, fixed";

        await opened.Send();

        var edited = Assert.Single(shell.Author.Edits);
        Assert.Equal("110", edited.PostId);
        Assert.Equal("Hello world, fixed", edited.Edit.Text);
    }

    [Fact]
    public async Task Edit_RefusesSomebodyElsesPost()
    {
        var shell = new AShell { Timelines = FakeTimelineReader.Holding(Somebody) };
        var opened = await shell.Opened();

        opened.Edit();

        Assert.IsType<FeedScreen>(opened.Screen);
        Assert.Contains("your own posts", opened.Notice);
    }

    /// <summary>Nothing written is nothing to send, and saying so beats an instance refusing an empty post.</summary>
    [Fact]
    public async Task Send_RefusesToPublishAnEmptyPost()
    {
        var shell = new AShell();
        var opened = await shell.Opened();

        opened.Compose();

        await opened.Send();

        Assert.Empty(shell.Author.Published);
        Assert.IsType<ComposeScreen>(opened.Screen);
        Assert.True(opened.NoticeIsError);
    }

    /// <summary>A content warning is honoured until the reader asks past it, which is what x is for.</summary>
    [Fact]
    public async Task Reveal_ShowsWhatAContentWarningWasHiding()
    {
        var shell = new AShell
        {
            Timelines = FakeTimelineReader.Holding(
                APost.With(id: "220", content: "The spoiler itself", contentWarning: "spoilers")),
        };

        var opened = await shell.Opened();

        var hidden = opened.Screen.Lines(61, AShell.Now).Select(line => line.Text).ToList();
        Assert.Contains(hidden, line => line.Contains("spoilers", StringComparison.Ordinal));
        Assert.DoesNotContain(hidden, line => line.Contains("The spoiler itself", StringComparison.Ordinal));

        opened.Reveal();

        var shown = opened.Screen.Lines(61, AShell.Now).Select(line => line.Text).ToList();
        Assert.Contains(shown, line => line.Contains("The spoiler itself", StringComparison.Ordinal));
    }
}
