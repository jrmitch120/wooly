using Wooly.Core.Errors;
using Wooly.Core.Posts;
using Wooly.Tests.Fakes;
using Wooly.Tui.Screens;
using Wooly.Tui.Theme;

namespace Wooly.Tests.Tui;

/// <summary>
///     Voting in a poll from the shell: what the digits toggle, what discards a toggle, and what <c>v</c> sends once it
///     has been agreed to (#87). Every one of these is a decision rather than a drawing — which answers were chosen,
///     whether anything has been sent yet, what the screen holds afterwards — so none of them needs a terminal.
/// </summary>
public class ShellVoteTests
{
    private static readonly Post Polled = APost.With(
        id: "220",
        account: "ben@hachyderm.io",
        poll: APost.APoll(options: [APost.AnAnswer("Cats", 4), APost.AnAnswer("Dogs", 6)], votes: 10));

    /// <summary>A digit toggles a local, unsent selection: nothing at all leaves for the instance until <c>v</c>.</summary>
    [Fact]
    public async Task Toggle_ChoosesAnAnswerWithoutSendingAnything()
    {
        var shell = new AShell { Timelines = FakeTimelineReader.Holding(Polled) };
        var opened = await shell.Opened();

        Assert.True(opened.Toggle(1));

        Assert.Equal([1], opened.Screen.Chosen);
        Assert.Empty(shell.Engagement.Votes);
    }

    /// <summary>Pressing the same digit again takes the answer back off, which is what toggling means.</summary>
    [Fact]
    public async Task Toggle_TakesAnAnswerBackOffWhenItIsPressedAgain()
    {
        var opened = await Reading(Polled);

        opened.Toggle(1);
        opened.Toggle(1);

        Assert.Empty(opened.Screen.Chosen);
    }

    /// <summary>
    ///     A single-choice poll takes one answer, so choosing a new one lets the last go — a ballot showing two boxes
    ///     ticked would be promising something the instance refuses.
    /// </summary>
    [Fact]
    public async Task Toggle_LetsTheLastAnswerGoOnASingleChoicePoll()
    {
        var opened = await Reading(Polled);

        opened.Toggle(0);
        opened.Toggle(1);

        Assert.Equal([1], opened.Screen.Chosen);
    }

    /// <summary>A poll that says a voter may choose several holds several at once.</summary>
    [Fact]
    public async Task Toggle_HoldsSeveralAnswersOnAMultipleChoicePoll()
    {
        var opened = await Reading(APost.With(
            id: "220",
            poll: APost.APoll(
                options: [APost.AnAnswer("Cats", 4), APost.AnAnswer("Dogs", 6), APost.AnAnswer("Sheep", 0)],
                multipleChoice: true)));

        opened.Toggle(0);
        opened.Toggle(2);

        Assert.Equal([0, 2], opened.Screen.Chosen.Order());
    }

    /// <summary>
    ///     A digit on a post with no poll, or past the end of a short one, is not this shell's key: it does nothing and
    ///     says it did nothing, so the window can leave it to whatever else wants it.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Toggle_IsNotUsedWhereThereIsNoAnswerToAddress(bool hasPoll)
    {
        var opened = await Reading(hasPoll ? Polled : APost.With(id: "220"));

        Assert.False(opened.Toggle(hasPoll ? 4 : 0));
        Assert.Empty(opened.Screen.Chosen);
    }

    /// <summary>The ballot is drawn on the post being read, with every other answer boxed and empty beside it.</summary>
    [Fact]
    public async Task Toggle_DrawsTheChosenAnswerAsATickedBox()
    {
        var opened = await Reading(Polled);

        opened.Toggle(1);

        var lines = opened.Screen.Lines(61, AShell.Now).Select(line => line.Text).ToList();

        Assert.Contains(lines, line => line.Contains("[x]") && line.EndsWith("Dogs", StringComparison.Ordinal));
        Assert.Contains(lines, line => line.Contains("[ ]") && line.EndsWith("Cats", StringComparison.Ordinal));
    }

    /// <summary>
    ///     Walking off the post discards the toggle, the same rule a picked reference follows: the reader has left the
    ///     post the half-finished vote was inside.
    /// </summary>
    [Theory]
    [InlineData(1)]
    [InlineData(-1)]
    public async Task Walking_DiscardsAnUncastVote(int by)
    {
        var shell = new AShell { Timelines = FakeTimelineReader.Holding(Polled, APost.With(id: "330")) };
        var opened = await shell.Opened();

        opened.Toggle(1);
        opened.Walk(by, reclaiming: null);

        Assert.Empty(opened.Screen.Chosen);
        Assert.Empty(shell.Engagement.Votes);
    }

    /// <summary>
    ///     <c>esc</c> is up one level of whichever kind is open, and an uncast vote is a level: the first press lets it
    ///     go and the next walks out of the screen.
    /// </summary>
    [Fact]
    public async Task Back_DiscardsAnUncastVoteBeforeItPopsTheScreen()
    {
        var shell = new AShell { Timelines = FakeTimelineReader.Holding(Polled) };
        var opened = await shell.Opened();

        await opened.Enter();
        shell.Host.Drain();

        opened.Toggle(1);
        opened.Back();

        Assert.Empty(opened.Screen.Chosen);
        Assert.IsType<PostScreen>(opened.Screen);

        opened.Back();

        Assert.IsType<FeedScreen>(opened.Screen);
    }

    /// <summary>
    ///     Story 43, and this qualifies for it more than a delete does: an instance refuses a second vote outright, so
    ///     one cast by accident is not something the reader can put right.
    /// </summary>
    [Fact]
    public async Task AskToVote_CastsNothingUntilItIsAgreedTo()
    {
        var shell = new AShell { Timelines = FakeTimelineReader.Holding(Polled) };
        var opened = await shell.Opened();

        opened.Toggle(1);
        opened.AskToVote();

        Assert.NotNull(opened.Asking);
        Assert.Contains("cannot be undone", opened.Asking.Question);
        Assert.Equal("vote", opened.Asking.Going);
        Assert.Empty(shell.Engagement.Votes);

        await opened.Answer(agreed: true);

        Assert.Null(opened.Asking);

        var cast = Assert.Single(shell.Engagement.Votes);
        Assert.Equal("220", cast.PostId);
        Assert.Equal([1], cast.Choices);
    }

    /// <summary>Answering no leaves the ballot exactly as it was, so the reader can change their mind and cast again.</summary>
    [Fact]
    public async Task Answer_LeavesTheBallotStandingWhereTheVoteIsNotAgreedTo()
    {
        var shell = new AShell { Timelines = FakeTimelineReader.Holding(Polled) };
        var opened = await shell.Opened();

        opened.Toggle(1);
        opened.AskToVote();
        await opened.Answer(agreed: false);

        Assert.Empty(shell.Engagement.Votes);
        Assert.Equal([1], opened.Screen.Chosen);
    }

    /// <summary>Every answer the reader ticked goes on the one call, in the order the poll lists them.</summary>
    [Fact]
    public async Task AskToVote_CastsEveryAnswerTheBallotHolds()
    {
        var shell = new AShell
        {
            Timelines = FakeTimelineReader.Holding(APost.With(
                id: "220",
                poll: APost.APoll(
                    options: [APost.AnAnswer("Cats", 4), APost.AnAnswer("Dogs", 6), APost.AnAnswer("Sheep", 0)],
                    multipleChoice: true))),
        };

        var opened = await shell.Opened();

        opened.Toggle(2);
        opened.Toggle(0);
        opened.AskToVote();
        await opened.Answer(agreed: true);

        Assert.Equal([0, 2], Assert.Single(shell.Engagement.Votes).Choices);
    }

    /// <summary>
    ///     The vote answers with the poll as it now stands, and that answer replaces the copy the screen is holding —
    ///     no second read of the post, which is the whole reason the port hands back a post at all.
    /// </summary>
    [Fact]
    public async Task Answer_DrawsThePollAsTheInstanceNowHasIt()
    {
        var shell = new AShell { Timelines = FakeTimelineReader.Holding(Polled) };

        shell.Engagement.Voted = APost.With(
            id: "220",
            account: "ben@hachyderm.io",
            poll: APost.APoll(
                options: [APost.AnAnswer("Cats", 4), APost.AnAnswer("Dogs", 7, picked: true)],
                votes: 11,
                voted: true));

        var opened = await shell.Opened();

        opened.Toggle(1);
        opened.AskToVote();
        await opened.Answer(agreed: true);
        shell.Host.Drain();

        Assert.True(opened.Screen.Picked?.Poll?.Voted);
        Assert.Equal(11, opened.Screen.Picked?.Poll?.Votes);

        // Read back once, and only for the arrival: nothing here asked the instance for the post a second time.
        Assert.Empty(shell.Engagement.Reads);
    }

    /// <summary>The ballot goes as the vote leaves: what is on screen from here on is what the instance says it is.</summary>
    [Fact]
    public async Task Answer_LetsTheBallotGoOnceTheVoteHasBeenCast()
    {
        var shell = new AShell { Timelines = FakeTimelineReader.Holding(Polled) };
        var opened = await shell.Opened();

        opened.Toggle(1);
        opened.AskToVote();
        await opened.Answer(agreed: true);
        shell.Host.Drain();

        Assert.Empty(opened.Screen.Chosen);
        Assert.DoesNotContain(opened.Screen.Lines(61, AShell.Now), line => line.Text.Contains("[x]"));
    }

    /// <summary>
    ///     An instance refuses a second vote outright, and that refusal is a notice over what the reader was reading
    ///     rather than a client that falls over.
    /// </summary>
    [Fact]
    public async Task Answer_SaysWhatTheInstanceRefusedRatherThanFallingOver()
    {
        var shell = new AShell { Timelines = FakeTimelineReader.Holding(Polled) };

        shell.Engagement.VoteRefusal = new VoteRefusedException(
            new InvalidOperationException("You have already voted on this poll"));

        var opened = await shell.Opened();

        opened.Toggle(1);
        opened.AskToVote();
        await opened.Answer(agreed: true);
        shell.Host.Drain();

        Assert.Contains("already voted", opened.Notice);
        Assert.True(opened.NoticeIsError);
        Assert.IsType<FeedScreen>(opened.Screen);
    }

    /// <summary>
    ///     <c>v</c> is announced wherever there is a poll, so it has to answer wherever it is announced: pressed with
    ///     nothing ticked it says what to do rather than putting a question nobody can answer.
    /// </summary>
    [Fact]
    public async Task AskToVote_SaysWhatToDoFirstWhereNothingIsToggled()
    {
        var shell = new AShell { Timelines = FakeTimelineReader.Holding(Polled) };
        var opened = await shell.Opened();

        opened.AskToVote();

        Assert.Null(opened.Asking);
        Assert.Contains("Choose an answer", opened.Notice);
        Assert.False(opened.NoticeIsError);
        Assert.Empty(shell.Engagement.Votes);
    }

    /// <summary>A post with no poll on it has nothing for <c>v</c> to ask about, and the key is not announced either.</summary>
    [Fact]
    public async Task AskToVote_AsksNothingOnAPostWithNoPoll()
    {
        var shell = new AShell { Timelines = FakeTimelineReader.Holding(APost.With(id: "220")) };
        var opened = await shell.Opened();

        opened.AskToVote();

        Assert.Null(opened.Asking);
        Assert.Null(opened.Notice);
    }

    /// <summary>
    ///     A poll still behind its post's content warning is not a poll to vote in: <c>v</c> is off the row, so it asks
    ///     nothing and says nothing — the same silence a post carrying no poll at all answers with. Asked past with
    ///     <c>x</c>, the key means what it always did (#119).
    /// </summary>
    [Fact]
    public async Task AskToVote_AsksNothingAboutAPollBehindAContentWarning()
    {
        var shell = new AShell
        {
            Timelines = FakeTimelineReader.Holding(
                APost.With(id: "220", contentWarning: "spoilers", poll: APost.APoll())),
        };

        var opened = await shell.Opened();

        opened.AskToVote();

        Assert.Null(opened.Asking);
        Assert.Null(opened.Notice);
        Assert.DoesNotContain(opened.Keys, key => key.Key == "v");

        opened.Reveal();
        opened.Toggle(0);
        opened.AskToVote();

        Assert.NotNull(opened.Asking);
    }

    /// <summary>
    ///     A vote is cast in the poll on the post inside a boost, since that is what carries it — the same post every
    ///     other key acts on.
    /// </summary>
    [Fact]
    public async Task AskToVote_VotesInThePollOnThePostInsideABoost()
    {
        var shell = new AShell
        {
            Timelines = FakeTimelineReader.Holding(APost.With(id: "555", boosted: Polled)),
        };

        var opened = await shell.Opened();

        opened.Toggle(0);
        opened.AskToVote();
        await opened.Answer(agreed: true);

        Assert.Equal("220", Assert.Single(shell.Engagement.Votes).PostId);
    }

    /// <summary>
    ///     The status row says the two keys a poll answers to, and says them nowhere else: a key announced where it
    ///     does nothing reads as a shell that missed the press.
    /// </summary>
    [Fact]
    public async Task Keys_SayHowToVoteOnlyWhereThereIsAPollToVoteIn()
    {
        var shell = new AShell { Timelines = FakeTimelineReader.Holding(Polled, APost.With(id: "330")) };
        var opened = await shell.Opened();

        Assert.Contains(opened.Keys, key => key is { Key: "1-0", Does: "option" });
        Assert.Contains(opened.Keys, key => key is { Key: "v", Does: "vote" });

        // And the screen's own keys are still behind them, cut off at the right the same way they always are.
        Assert.Contains(opened.Keys, key => key.Key == "j/k");

        opened.Walk(1, reclaiming: null);

        Assert.DoesNotContain(opened.Keys, key => key.Key == "1-0");
        Assert.DoesNotContain(opened.Keys, key => key.Key == "v");
    }

    /// <summary>
    ///     A picked reference is the level the reader is standing on, so its keys come first — and the poll's are back
    ///     the moment the reference is let go.
    /// </summary>
    [Fact]
    public async Task Keys_PutAPickedReferenceAheadOfThePoll()
    {
        var shell = new AShell
        {
            Timelines = FakeTimelineReader.Holding(APost.With(
                id: "220",
                content: "Which is it, #cats or #dogs?",
                poll: APost.APoll())),
        };

        var opened = await shell.Opened();

        opened.WalkReference(1);

        Assert.Contains(opened.Keys, key => key.Key == "←/→");
        Assert.DoesNotContain(opened.Keys, key => key.Key == "1-0");

        opened.Back();

        Assert.Contains(opened.Keys, key => key.Key == "1-0");
    }

    /// <summary>Nothing about a ballot is themed on its own: the boxes are the poll's role, as the contract says.</summary>
    [Fact]
    public async Task Toggle_DrawsTheBallotInThePollsOwnRole()
    {
        var opened = await Reading(Polled);

        opened.Toggle(1);

        var ticked = opened.Screen.Lines(61, AShell.Now).First(line => line.Text.Contains("[x]"));

        Assert.True(ticked.Has(Role.Poll));
    }

    /// <summary>
    ///     The question names the answer being voted for, in the poll's own words: what a vote can be wrong about is
    ///     which answer it is for, and the id of the post the poll is on answers a question nobody voting has.
    /// </summary>
    [Fact]
    public async Task AskToVote_AsksAboutTheAnswerRatherThanThePostItIsOn()
    {
        var opened = await Reading(Polled);

        opened.Toggle(1);
        opened.AskToVote();

        Assert.Equal("Vote for \"Dogs\"? This cannot be undone.", opened.Asking?.Question);
        Assert.DoesNotContain("220", opened.Asking?.Question);
    }

    /// <summary>
    ///     Several answers are counted rather than listed, so that the question cannot outgrow the row: the ballot is
    ///     on screen and every answer being agreed to is drawn <c>[x]</c> on it.
    /// </summary>
    [Fact]
    public async Task AskToVote_CountsTheAnswersWhereMoreThanOneIsTicked()
    {
        var opened = await Reading(APost.With(
            id: "220",
            poll: APost.APoll(
                options: [APost.AnAnswer("Cats", 4), APost.AnAnswer("Dogs", 6), APost.AnAnswer("Sheep", 0)],
                multipleChoice: true)));

        opened.Toggle(0);
        opened.Toggle(2);
        opened.AskToVote();

        Assert.Equal("Cast the 2 answers you ticked? This cannot be undone.", opened.Asking?.Question);
    }

    /// <summary>
    ///     The question takes the status row and the way to answer it takes what is left, so a question long enough to
    ///     push <c>y vote · esc keep</c> off the right is one nobody knows how to answer — however long the answer
    ///     somebody else wrote is.
    /// </summary>
    [Fact]
    public async Task AskToVote_KeepsTheWayToAnswerOnTheRowBesideTheLongestAnswer()
    {
        var opened = await Reading(APost.With(
            id: "220",
            poll: APost.APoll(options:
            [
                APost.AnAnswer("Cats", 4),
                APost.AnAnswer("An answer with rather more to say for itself than the others", 6),
            ])));

        opened.Toggle(1);
        opened.AskToVote();

        var row = ChromeLines.Status(opened.Keys, opened.Notice, opened.NoticeIsError, opened.Asking, 80).Text;

        Assert.Contains("y vote · esc keep", row);
        Assert.True(row.Length <= 80, row);
    }

    /// <summary>
    ///     A poll this profile has already voted in, or one that has closed, is a result to read rather than a
    ///     question to answer — so neither key is offered over it and neither does anything.
    /// </summary>
    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task APollThatTakesNoVoteOffersNeitherKey(bool voted, bool closed)
    {
        var opened = await Reading(APost.With(
            id: "220",
            poll: APost.APoll(voted: voted, closed: closed)));

        Assert.DoesNotContain(opened.Keys, key => key.Key == "1-0");
        Assert.DoesNotContain(opened.Keys, key => key.Key == "v");

        Assert.False(opened.Toggle(0));
        Assert.Empty(opened.Screen.Chosen);
    }

    /// <summary>
    ///     Which of the two reasons it is, is worth saying: the poll is on screen and the key is on the keyboard, so a
    ///     press answered with nothing at all reads as a shell that missed it.
    /// </summary>
    [Theory]
    [InlineData(true, false, "already voted")]
    [InlineData(false, true, "has closed")]
    public async Task AskToVote_SaysWhyAPollWillTakeNoVote(bool voted, bool closed, string said)
    {
        var shell = new AShell
        {
            Timelines = FakeTimelineReader.Holding(APost.With(id: "220", poll: APost.APoll(voted: voted, closed: closed))),
        };

        var opened = await shell.Opened();

        opened.AskToVote();

        Assert.Null(opened.Asking);
        Assert.Contains(said, opened.Notice);
        Assert.Empty(shell.Engagement.Votes);
    }

    /// <summary>
    ///     And once a vote lands, the poll it landed in is one of those: the answer the instance sent back says this
    ///     profile has voted, so the keys come off the row rather than inviting a second one it would refuse.
    /// </summary>
    [Fact]
    public async Task Answer_TakesTheVotingKeysOffTheRowOnceTheVoteHasLanded()
    {
        var shell = new AShell { Timelines = FakeTimelineReader.Holding(Polled) };

        shell.Engagement.Voted = APost.With(
            id: "220",
            account: "ben@hachyderm.io",
            poll: APost.APoll(voted: true));

        var opened = await shell.Opened();

        opened.Toggle(1);
        opened.AskToVote();
        await opened.Answer(agreed: true);
        shell.Host.Drain();

        Assert.DoesNotContain(opened.Keys, key => key.Key == "v");
    }

    /// <summary>
    ///     The status row holds a notice or the keymap and never both, so a remark left standing is every key the
    ///     screen answers to, hidden — including the <c>v</c> the remark itself just asked for.
    /// </summary>
    [Fact]
    public async Task Toggle_TakesAStaleRemarkOffTheRowSoTheKeysAreBackOnIt()
    {
        var opened = await Reading(Polled);

        opened.AskToVote();

        Assert.NotNull(opened.Notice);

        opened.Toggle(1);

        Assert.Null(opened.Notice);
    }

    /// <summary>A remark is about the post it was said over, so walking off that post takes it away too.</summary>
    [Fact]
    public async Task Walking_LeavesARemarkBehindWithThePostItWasSaidOver()
    {
        var shell = new AShell { Timelines = FakeTimelineReader.Holding(Polled, APost.With(id: "330")) };
        var opened = await shell.Opened();

        opened.AskToVote();

        Assert.NotNull(opened.Notice);

        opened.Walk(1, reclaiming: null);

        Assert.Null(opened.Notice);
    }

    /// <summary>A shell opened onto a feed holding one post, which is the post every one of these is about.</summary>
    private static async Task<Wooly.Tui.Shell.Shell> Reading(Post post) =>
        await new AShell { Timelines = FakeTimelineReader.Holding(post) }.Opened();
}
