using Wooly.Core.Posts;

namespace Wooly.Tests.Core;

/// <summary>
///     What makes a poll a poll, tested as the pure rule it is, at the one place both the argument parser and the domain
///     ask it.
/// </summary>
public class PollDraftTests
{
    private static readonly TimeSpan ADay = TimeSpan.FromDays(1);

    [Fact]
    public void Problem_AcceptsAPollWithAnswersToChooseBetween() =>
        Assert.Null(PollDraft.Problem(["Cats", "Dogs"], ADay));

    /// <summary>One answer is not a question, and no answers is not a poll at all.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void Problem_RefusesAPollWithTooFewAnswers(int answers) =>
        Assert.NotNull(PollDraft.Problem(Enumerable.Range(1, answers).Select(n => $"Answer {n}").ToList(), ADay));

    /// <summary>
    ///     Says nothing about the most answers allowed: that is an instance's own setting, and a client guessing at it
    ///     would turn down polls the instance would have taken.
    /// </summary>
    [Fact]
    public void Problem_LeavesTheMostAnswersAllowedToTheInstance() =>
        Assert.Null(PollDraft.Problem(["One", "Two", "Three", "Four", "Five", "Six"], ADay));

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Problem_RefusesAnAnswerWithNothingWrittenInIt(string blank) =>
        Assert.NotNull(PollDraft.Problem(["Cats", blank], ADay));

    /// <summary>Two answers a voter cannot tell apart are one answer offered twice.</summary>
    [Fact]
    public void Problem_RefusesTheSameAnswerTwice() =>
        Assert.NotNull(PollDraft.Problem(["Cats", "Dogs", "Cats"], ADay));

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Problem_RefusesAPollThatIsNotOpenForAnyLengthOfTime(int hours) =>
        Assert.NotNull(PollDraft.Problem(["Cats", "Dogs"], TimeSpan.FromHours(hours)));

    /// <summary>
    ///     The domain's own guard. A caller is expected to have rejected the answers already, so reaching here with one
    ///     answer is a defect rather than something to report to a user — but it is emphatically not something to ask an
    ///     instance to open.
    /// </summary>
    [Fact]
    public void Of_RefusesToBuildAPollNobodyCouldVoteIn() =>
        Assert.Throws<ArgumentException>(() => PollDraft.Of(["Cats"], ADay));

    [Fact]
    public void Of_KeepsTheAnswersInTheOrderTheyWereGiven() =>
        Assert.Equal(["Cats", "Dogs"], PollDraft.Of(["Cats", "Dogs"], ADay).Answers);
}
