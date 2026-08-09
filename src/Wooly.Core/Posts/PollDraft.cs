namespace Wooly.Core.Posts;

/// <summary>
///     A poll to be attached to a post: the answers to choose between, how long it stays open, and whether more than
///     one answer may be chosen. Reached through <see cref="Of" /> rather than a constructor, so a poll with one answer
///     — which is not a question — cannot be built, let alone sent to an instance.
/// </summary>
public sealed record PollDraft
{
    /// <summary>
    ///     The fewest answers that make a poll. Mastodon's own minimum, and the same number a person would give: one
    ///     answer is a statement.
    /// </summary>
    public const int FewestAnswers = 2;

    private PollDraft(IReadOnlyList<string> answers, TimeSpan openFor, bool multipleChoice)
    {
        Answers = answers;
        OpenFor = openFor;
        MultipleChoice = multipleChoice;
    }

    /// <summary>The answers to choose between, in the order they should be shown.</summary>
    public IReadOnlyList<string> Answers { get; }

    /// <summary>How long the poll accepts votes for, measured from when the post is published.</summary>
    public TimeSpan OpenFor { get; }

    /// <summary>Whether a voter may choose more than one answer.</summary>
    public bool MultipleChoice { get; }

    /// <summary>A poll offering <paramref name="answers" />, open for <paramref name="openFor" />.</summary>
    /// <exception cref="ArgumentException">
    ///     The poll is not one an instance would accept (<see cref="Problem" />). A caller is expected to have rejected
    ///     that against what the user gave; reaching here with one is a defect, not user error.
    /// </exception>
    public static PollDraft Of(IReadOnlyList<string> answers, TimeSpan openFor, bool multipleChoice = false)
    {
        if (Problem(answers, openFor) is { } problem)
        {
            throw new ArgumentException(problem, nameof(answers));
        }

        return new PollDraft(answers, openFor, multipleChoice);
    }

    /// <summary>
    ///     What is wrong with a poll made of these values, or <see langword="null" /> if nothing is. The one place the
    ///     rule lives: the argument parser asks it so a user reads the answer where they typed the mistake, and
    ///     <see cref="Of" /> asks it again so a poll that exists is one an instance can be asked to open.
    /// </summary>
    /// <remarks>
    ///     Deliberately says nothing about the <em>most</em> answers allowed, or the longest a poll may stay open: both
    ///     are an instance's own settings, and a client that guessed at them would turn down polls the instance would
    ///     have taken. Those come back as the instance's own refusal, in its own words.
    /// </remarks>
    public static string? Problem(IReadOnlyList<string> answers, TimeSpan openFor)
    {
        if (answers.Count < FewestAnswers)
        {
            return $"A poll needs at least {FewestAnswers} answers to choose between.";
        }

        if (answers.Any(string.IsNullOrWhiteSpace))
        {
            return "A poll's answers each need something written in them.";
        }

        // Ordinal, because two answers differing only in case are two answers a voter cannot tell apart.
        if (answers.Distinct(StringComparer.Ordinal).Count() != answers.Count)
        {
            return "A poll's answers need to differ from one another.";
        }

        return openFor <= TimeSpan.Zero ? "A poll has to stay open for some length of time." : null;
    }
}
