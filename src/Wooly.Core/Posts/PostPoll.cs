namespace Wooly.Core.Posts;

/// <summary>
///     A poll read back off a post — the read-side counterpart to <see cref="PollDraft" />, matching
///     <see cref="MediaAttachment" />/<see cref="PostMedia" />'s mnemonic for the same split.
/// </summary>
public sealed record PostPoll
{
    /// <summary>
    ///     The instance's own id for the poll, which is not the id of the post carrying it and is what voting in it
    ///     takes (<see cref="IPostEngagement.Vote" />).
    /// </summary>
    public required string Id { get; init; }

    /// <summary>The answers to choose between, in the order the instance gave them.</summary>
    public required IReadOnlyList<PostPollOption> Options { get; init; }

    /// <summary>How many votes the poll has drawn in total, across every option.</summary>
    public required long Votes { get; init; }

    /// <summary>
    ///     How many accounts have voted, reported only for a poll where <see cref="MultipleChoice" /> lets an account's
    ///     vote count differ from <see cref="Votes" />; <see langword="null" /> otherwise.
    /// </summary>
    public long? Voters { get; init; }

    /// <summary>Whether a voter may choose more than one answer.</summary>
    public required bool MultipleChoice { get; init; }

    /// <summary>Whether the poll no longer accepts votes.</summary>
    public required bool Closed { get; init; }

    /// <summary>When the poll stops accepting votes, or <see langword="null" /> if the instance did not say.</summary>
    public DateTimeOffset? ExpiresAt { get; init; }

    /// <summary>Whether the profile reading the post has already voted in it.</summary>
    public required bool Voted { get; init; }

    /// <summary>
    ///     Whether this poll would still take a vote from the profile that read it: one that has not closed, and that
    ///     they have not already voted in.
    /// </summary>
    /// <remarks>
    ///     The two reasons a poll is finished with a reader are one question, because nothing that acts on the answer
    ///     tells them apart: a front end offering a vote offers it in a poll that takes one, and the difference between
    ///     "closed" and "already answered" is something to say rather than something to branch on.
    ///     <para>
    ///         Said off what the instance last sent rather than asked for, so it is exactly as fresh as the post
    ///         carrying it. It is what makes a key worth offering, not what makes a vote legal — the instance settles
    ///         that when one is cast, and still refuses one this says it would take if the poll shut in between
    ///         (ADR-0009).
    ///     </para>
    /// </remarks>
    public bool TakesAVote => !Closed && !Voted;
}
