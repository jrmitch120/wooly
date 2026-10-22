using Wooly.Core.Errors;
using Wooly.Core.Posts;
using Wooly.Tests.Fakes;
using Wooly.Tui.Rendering;
using Wooly.Tui.Screens;
using Wooly.Tui.Shell;

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
        shell.Host.Drain();

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
        Assert.Equal(Confirmation.CannotBeUndone, opened.Asking.Warning);
        Assert.Empty(shell.Author.Deletions);

        await opened.Answer(agreed: true);

        Assert.Null(opened.Asking);
        Assert.Equal("110", Assert.Single(shell.Author.Deletions).PostId);
    }

    /// <summary>
    ///     The question names no post id — no screen draws one, so it could be checked against nothing, and the post
    ///     it is about is the one drawn picked. So an instance whose ids are not Mastodon-shaped asks it the same way,
    ///     in the same columns (#219).
    /// </summary>
    [Theory]
    [InlineData("110")]
    [InlineData("01J9ZQ4M7K2VXB3NTRW8YH5F6D")]
    public async Task AskToDelete_AsksAboutThisPostWhateverItsIdIs(string id)
    {
        var shell = new AShell
        {
            Timelines = FakeTimelineReader.Holding(APost.With(id: id, account: "jeff@mastodon.social")),
        };
        var opened = await shell.Opened();

        opened.AskToDelete();

        Assert.Equal("Delete this post?", opened.Asking?.Ask);
        Assert.Equal(
            " Delete this post? This cannot be undone.  y delete · esc keep",
            ChromeLines.Status(opened.Keys, opened.Notice, opened.NoticeIsError, opened.Asking, 80).Text);
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
        shell.Host.Drain();
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
        shell.Host.Drain();

        var feed = Assert.IsType<FeedScreen>(opened.Screen);
        Assert.Equal(["220"], feed.Posts.Select(post => post.Id));
    }

    /// <summary>Compose opens an editor with nothing in it, and sending publishes what was written.</summary>
    [Fact]
    public async Task Send_PublishesWhatWasComposed()
    {
        var shell = new AShell();
        var opened = await shell.Opened();

        opened.Press(ShellKey.C);

        var compose = Assert.IsType<ComposeScreen>(opened.Screen);
        Assert.Equal(ComposeFor.Post, compose.Purpose);

        compose.Text = "Hello from the rail";

        await opened.Drafting.Send();
        shell.Host.Drain();

        var published = Assert.Single(shell.Author.Published);
        Assert.Equal("Hello from the rail", published.Draft.Text);
        Assert.Null(published.Draft.InReplyTo);

        // The editor is closed once what was in it has gone out.
        Assert.IsType<FeedScreen>(opened.Screen);
    }

    /// <summary>
    ///     A post the instance turns down is said, in the instance's words, over the draft — which stays where it is,
    ///     every word of it, for the writer to cut down and send again.
    /// </summary>
    [Fact]
    public async Task Send_SaysWhyTheInstanceRefusedThePostAndKeepsTheDraft()
    {
        var shell = new AShell
        {
            Author = FakePostAuthor.Refusing(
                new PostRefusedException("Text character limit of 500 exceeded")),
        };
        var opened = await shell.Opened();

        opened.Press(ShellKey.C);

        var compose = Assert.IsType<ComposeScreen>(opened.Screen);

        compose.Text = "Far too long";

        await opened.Drafting.Send();
        shell.Host.Drain();

        Assert.Contains("character limit of 500 exceeded", opened.Notice);
        Assert.True(opened.NoticeIsError);
        Assert.Same(compose, opened.Screen);
        Assert.Equal("Far too long", compose.Text);
    }

    /// <summary>A reply is a draft that names what it answers, which is what the port already takes.</summary>
    [Fact]
    public async Task Send_RepliesToThePickedPost()
    {
        var shell = new AShell { Timelines = FakeTimelineReader.Holding(Somebody) };
        var opened = await shell.Opened();

        opened.Press(ShellKey.R);

        var compose = Assert.IsType<ComposeScreen>(opened.Screen);
        compose.Text = "Answering you";

        await opened.Drafting.Send();

        Assert.Equal("220", Assert.Single(shell.Author.Published).Draft.InReplyTo);
    }

    /// <summary>
    ///     A reply that is nothing but the mention it opened with says nothing the reader wrote, so it is refused —
    ///     which now covers a public reply too, since a public reply opens with one.
    /// </summary>
    [Fact]
    public async Task Send_RefusesAPublicReplyWithNothingButTheMentionItOpenedWith()
    {
        var shell = new AShell { Timelines = FakeTimelineReader.Holding(Somebody) };
        var opened = await shell.Opened();

        opened.Press(ShellKey.R);

        await opened.Drafting.Send();
        shell.Host.Drain();

        Assert.Empty(shell.Author.Published);
        Assert.True(opened.NoticeIsError);
    }

    /// <summary>Editing starts from what the post already says, because an edit is a change rather than a rewrite.</summary>
    [Fact]
    public async Task Send_SavesAnEditOfTheProfilesOwnPost()
    {
        var shell = new AShell { Timelines = FakeTimelineReader.Holding(Mine) };
        var opened = await shell.Opened();

        opened.Press(ShellKey.E);

        var compose = Assert.IsType<ComposeScreen>(opened.Screen);
        Assert.Equal("Hello world", compose.Text);

        compose.Text = "Hello world, fixed";

        await opened.Drafting.Send();

        var edited = Assert.Single(shell.Author.Edits);
        Assert.Equal("110", edited.PostId);
        Assert.Equal("Hello world, fixed", edited.Edit.Text);
    }

    /// <summary>Nothing written is nothing to send, and saying so beats an instance refusing an empty post.</summary>
    [Fact]
    public async Task Send_RefusesToPublishAnEmptyPost()
    {
        var shell = new AShell();
        var opened = await shell.Opened();

        opened.Press(ShellKey.C);

        await opened.Drafting.Send();

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

        var hidden = opened.Screen.Lines(new Drawing(61, AShell.Now)).Select(line => line.Text).ToList();
        Assert.Contains(hidden, line => line.Contains("spoilers", StringComparison.Ordinal));
        Assert.DoesNotContain(hidden, line => line.Contains("The spoiler itself", StringComparison.Ordinal));

        opened.Reveal();

        var shown = opened.Screen.Lines(new Drawing(61, AShell.Now)).Select(line => line.Text).ToList();
        Assert.Contains(shown, line => line.Contains("The spoiler itself", StringComparison.Ordinal));
    }

    /// <summary>
    ///     A key that means nothing on this screen is left unused, so that it falls through the window to whatever
    ///     else wants it — handing it to the screen as one of its own would spend it on nothing (#232).
    /// </summary>
    [Fact]
    public async Task Press_LeavesAKeyThatMeansNothingHereUnused()
    {
        var opened = await new AShell().Opened();

        Assert.False(opened.Press(ShellKey.CtrlS));
    }

    /// <summary>
    ///     A screen-local key bound on every screen is spent wherever it is pressed, and a screen with nothing for it
    ///     does nothing with it: <c>D</c> on a feed empties no inbox and asks nothing (#232).
    /// </summary>
    [Fact]
    public async Task Press_SpendsAScreenLocalKeyOnAScreenThatDoesNothingWithIt()
    {
        var shell = new AShell();
        var opened = await shell.Opened();

        Assert.True(opened.Press(ShellKey.CapitalD));
        shell.Host.Drain();

        Assert.Null(opened.Asking);
        Assert.IsType<FeedScreen>(opened.Screen);
    }

    /// <summary>
    ///     The verbs that need a terminal are the window's, so the shell answers each of them unused rather than handing
    ///     it to the screen — and there are the sixteen <see cref="Verb" /> names, no more and no fewer (#232, #414).
    /// </summary>
    [Fact]
    public async Task Do_LeavesEveryVerbThatNeedsATerminalUnused()
    {
        var opened = await new AShell().Opened();

        var windows = Enum.GetValues<Verb>().Where(verb => verb.NeedsATerminal()).ToList();

        Assert.Equal(16, windows.Count);
        Assert.All(windows, verb => Assert.False(opened.Do(verb, answer: null)));
    }
}
