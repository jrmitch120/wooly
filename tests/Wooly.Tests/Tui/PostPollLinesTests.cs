using Wooly.Core.Posts;
using Wooly.Tests.Fakes;
using Wooly.Tui.Rendering;
using Wooly.Tui.Screens;
using Wooly.Tui.Theme;

namespace Wooly.Tests.Tui;

/// <summary>
///     What a poll reads back as, on both the feed and the post screen — full detail on either, since a poll is
///     never the thing being skimmed past. Held against <see cref="PostLines" /> rather than a screen, the same
///     reason <see cref="PostBylineTests" /> is: the feed and the post screen share these rows, and a test per
///     screen would be two chances for them to drift apart.
/// </summary>
public class PostPollLinesTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 29, 12, 30, 0, TimeSpan.Zero);

    /// <summary>A post with nothing but a poll on it, so a row's text is unambiguously the poll's own.</summary>
    private static Post With(PostPoll poll) => APost.With(poll: poll);

    /// <param name="chosen">
    ///     Which answers the reader has toggled and not yet cast, or <see langword="null" /> for the poll being read
    ///     rather than voted in — which is what <see langword="default" /> says everywhere else.
    /// </param>
    private static IReadOnlyList<Line> Feed(Post post, int[]? chosen = null, int width = 61) =>
        PostLines.Feed(post, width, new Reading(Chosen: chosen?.ToHashSet()), Now);

    private static IReadOnlyList<Line> Whole(Post post, int width = 61) =>
        PostLines.Whole(post, width, default, Now);

    /// <summary>A post carrying no poll draws none of this at all.</summary>
    [Fact]
    public void Feed_DrawsNoPollRowsForAPostWithNone()
    {
        var lines = Feed(APost.With());

        Assert.DoesNotContain(lines, line => line.Has(Role.Poll));
    }

    /// <summary>
    ///     Each option's bar, its share and raw count, and a leading mark on the one this profile picked — all in
    ///     <see cref="Role.Poll" />, the role the contract already themed with nothing ever emitting it.
    /// </summary>
    [Fact]
    public void Feed_DrawsABarWithThePercentageAndRawCountForEachOption()
    {
        var lines = Feed(With(APost.APoll(
            options: [APost.AnAnswer("Cats", 4), APost.AnAnswer("Dogs", 6, picked: true)],
            votes: 10)));

        var cats = lines.First(line => line.Text.Contains("Cats", StringComparison.Ordinal));
        var dogs = lines.First(line => line.Text.Contains("Dogs", StringComparison.Ordinal));

        Assert.Equal("  ▓▓▓▓░░░░░░ 40% (4)  Cats", cats.Text);
        Assert.True(cats.Has(Role.Poll));

        Assert.Equal("✓ ▓▓▓▓▓▓░░░░ 60% (6)  Dogs", dogs.Text);
        Assert.True(dogs.Has(Role.Poll));
    }

    /// <summary>A genuinely unvoted option still draws a bar, at 0% — not the same thing as a withheld count.</summary>
    [Fact]
    public void Feed_DrawsAnEmptyBarAndZeroPercentForAGenuinelyUnvotedOption()
    {
        var lines = Feed(With(APost.APoll(
            options: [APost.AnAnswer("Cats", 0), APost.AnAnswer("Dogs", 6)],
            votes: 6)));

        Assert.Contains(lines, line => line.Text == "  ░░░░░░░░░░ 0% (0)  Cats");
    }

    /// <summary>
    ///     An instance withholds a per-option breakdown until this profile votes or the poll closes — a third state,
    ///     distinct from a genuine zero, that draws no bar at all rather than guess at one.
    /// </summary>
    [Fact]
    public void Feed_DrawsNoBarAtAllForAnOptionWhoseCountIsWithheld()
    {
        var lines = Feed(With(APost.APoll(
            options: [APost.AnAnswer("Cats", null), APost.AnAnswer("Dogs", null)],
            votes: 0)));

        var cats = lines.First(line => line.Text.Contains("Cats", StringComparison.Ordinal));
        var dogs = lines.First(line => line.Text.Contains("Dogs", StringComparison.Ordinal));

        Assert.Equal("  Cats", cats.Text);
        Assert.Equal("  Dogs", dogs.Text);
        Assert.DoesNotContain(lines, line => line.Text.Contains('▓') || line.Text.Contains('░'));
    }

    [Fact]
    public void Feed_SaysAPollHasClosedInPlaceOfWhenItCloses()
    {
        var lines = Feed(With(APost.APoll(closed: true)));

        var closed = lines.First(line => line.Text == "Closed");

        Assert.Equal(Role.Muted, closed.Role);
    }

    [Fact]
    public void Feed_SaysAnOpenPollsClosingTime()
    {
        var lines = Feed(With(APost.APoll(expiresAt: new DateTimeOffset(2026, 8, 10, 12, 0, 0, TimeSpan.Zero))));

        var closes = lines.First(line => line.Text.StartsWith("Closes ", StringComparison.Ordinal));

        Assert.Equal(Role.Muted, closes.Role);
    }

    [Fact]
    public void Feed_SaysNothingAboutClosingWhenThePollHasNoEndDate()
    {
        var lines = Feed(With(APost.APoll()));

        Assert.DoesNotContain(lines, line => line.Text.Contains("Closes", StringComparison.Ordinal));
        Assert.DoesNotContain(lines, line => line.Text.Contains("Closed", StringComparison.Ordinal));
    }

    [Fact]
    public void Feed_NotesInAMutedLineWhenAPollLetsAVoterChooseMoreThanOneAnswer()
    {
        var lines = Feed(With(APost.APoll(multipleChoice: true)));

        var note = lines.First(line => line.Text.Contains("choose as many", StringComparison.OrdinalIgnoreCase));

        Assert.Equal(Role.Muted, note.Role);
    }

    /// <summary>The vote count normally says only itself, however many accounts cast the votes.</summary>
    [Fact]
    public void Feed_SaysTheVoteCount()
    {
        var lines = Feed(With(APost.APoll(votes: 10)));

        var count = lines.First(line => line.Text.Contains("votes", StringComparison.Ordinal));

        Assert.Equal("10 votes", count.Text);
        Assert.Equal(Role.Muted, count.Role);
    }

    /// <summary>
    ///     Multiple choice lets one account cast several votes, so the count says both — but only once an instance
    ///     has actually reported how many accounts that was.
    /// </summary>
    [Fact]
    public void Feed_SaysVotesAndVotersForAMultipleChoicePollThatNamesBoth()
    {
        var lines = Feed(With(APost.APoll(votes: 16, voters: 7, multipleChoice: true)));

        Assert.Contains(lines, line => line.Text == "16 votes from 7 accounts");
    }

    /// <summary>A multiple-choice poll whose instance withheld the voter count still says just the vote count.</summary>
    [Fact]
    public void Feed_SaysOnlyTheVoteCountForMultipleChoiceWithNoVoterCount()
    {
        var lines = Feed(With(APost.APoll(votes: 16, voters: null, multipleChoice: true)));

        Assert.Contains(lines, line => line.Text == "16 votes");
        Assert.DoesNotContain(lines, line => line.Text.Contains("accounts", StringComparison.Ordinal));
    }

    /// <summary>The post screen draws the identical rows: full detail is full detail wherever it is read.</summary>
    [Fact]
    public void FeedAndWhole_DrawTheSamePollRows()
    {
        var post = With(APost.APoll(
            options: [APost.AnAnswer("Cats", 4), APost.AnAnswer("Dogs", 6, picked: true)],
            votes: 10,
            multipleChoice: true,
            voters: 8));

        var feed = Feed(post).Where(line => line.Has(Role.Poll) || line.Role == Role.Muted).Select(line => line.Text);
        var whole = Whole(post).Where(line => line.Has(Role.Poll) || line.Role == Role.Muted).Select(line => line.Text);

        Assert.Equal(feed, whole);
    }

    /// <summary>Nothing a poll draws runs past the room it was given, however long an option's text is.</summary>
    [Theory]
    [InlineData(20)]
    [InlineData(40)]
    [InlineData(61)]
    public void Feed_KeepsThePollInsideTheRoomItWasGiven(int width)
    {
        var lines = Feed(
            With(APost.APoll(
                options:
                [
                    APost.AnAnswer("An answer with rather more to say for itself than the others", 4),
                    APost.AnAnswer("Dogs", 6, picked: true),
                ],
                votes: 10,
                multipleChoice: true,
                voters: 8,
                expiresAt: new DateTimeOffset(2026, 8, 10, 12, 0, 0, TimeSpan.Zero))),
            width: width);

        Assert.All(lines, line => Assert.True(line.Width <= width, $"'{line.Text}' is {line.Width} columns"));
    }

    /// <summary>
    ///     A toggled vote turns the poll into a ballot: the answer chosen is boxed and ticked, and every other answer
    ///     is boxed and empty — a tick means nothing on its own, only against the boxes beside it (#87).
    /// </summary>
    [Fact]
    public void Feed_DrawsTheToggledAnswerAsATickedBoxAndTheRestAsEmptyOnes()
    {
        var lines = Feed(With(APost.APoll(
            options: [APost.AnAnswer("Cats", 4), APost.AnAnswer("Dogs", 6)],
            votes: 10)), chosen: [1]);

        Assert.Contains(lines, line => line.Text == "[ ] ▓▓▓▓░░░░░░ 40% (4)  Cats");
        Assert.Contains(lines, line => line.Text == "[x] ▓▓▓▓▓▓░░░░ 60% (6)  Dogs");
    }

    /// <summary>A poll that takes several answers boxes every one of them the reader has ticked.</summary>
    [Fact]
    public void Feed_TicksEveryAnswerAToggledMultipleChoiceVoteHolds()
    {
        var lines = Feed(
            With(APost.APoll(
                options: [APost.AnAnswer("Cats", 4), APost.AnAnswer("Dogs", 6), APost.AnAnswer("Sheep", 0)],
                votes: 10,
                multipleChoice: true)),
            chosen: [0, 2]);

        Assert.Contains(lines, line => line.Text.StartsWith("[x]", StringComparison.Ordinal) && line.Text.EndsWith("Cats", StringComparison.Ordinal));
        Assert.Contains(lines, line => line.Text.StartsWith("[ ]", StringComparison.Ordinal) && line.Text.EndsWith("Dogs", StringComparison.Ordinal));
        Assert.Contains(lines, line => line.Text.StartsWith("[x]", StringComparison.Ordinal) && line.Text.EndsWith("Sheep", StringComparison.Ordinal));
    }

    /// <summary>
    ///     The ballot stands in for the mark rather than beside it: what this profile voted for last time is not what
    ///     they are about to vote for now, and two ticks on one row would be the poll arguing with itself.
    /// </summary>
    [Fact]
    public void Feed_DrawsNoPickedMarkOnAnAnswerWhileAVoteIsToggled()
    {
        var lines = Feed(
            With(APost.APoll(options: [APost.AnAnswer("Cats", 4), APost.AnAnswer("Dogs", 6, picked: true)])),
            chosen: [0]);

        Assert.DoesNotContain(lines, line => line.Text.Contains('✓'));
    }

    /// <summary>
    ///     A poll nobody is voting in draws exactly what it drew before: an empty toggle is a reader reading, not a
    ///     reader half way through a ballot.
    /// </summary>
    [Fact]
    public void Feed_DrawsTheOrdinaryMarksWhereNothingIsToggled()
    {
        var post = With(APost.APoll(options: [APost.AnAnswer("Cats", 4), APost.AnAnswer("Dogs", 6, picked: true)]));

        Assert.Equal(
            Feed(post).Select(line => line.Text),
            Feed(post, chosen: []).Select(line => line.Text));
    }

    /// <summary>A ballot is boxes and text, and the boxes are the poll's own role — nothing new was themed for them.</summary>
    [Fact]
    public void Feed_DrawsABallotInThePollsOwnRole()
    {
        var lines = Feed(With(APost.APoll()), chosen: [0]);

        var ticked = lines.First(line => line.Text.StartsWith("[x]", StringComparison.Ordinal));

        Assert.True(ticked.Has(Role.Poll));
    }

    /// <summary>A ballot is two columns wider than the poll it replaces, and still fits the room it was given.</summary>
    [Theory]
    [InlineData(20)]
    [InlineData(40)]
    [InlineData(61)]
    public void Feed_KeepsABallotInsideTheRoomItWasGiven(int width)
    {
        var lines = Feed(
            With(APost.APoll(
                options:
                [
                    APost.AnAnswer("An answer with rather more to say for itself than the others", 4),
                    APost.AnAnswer("Dogs", 6),
                ],
                votes: 10)),
            chosen: [0],
            width: width);

        Assert.All(lines, line => Assert.True(line.Width <= width, $"'{line.Text}' is {line.Width} columns"));
    }
}
