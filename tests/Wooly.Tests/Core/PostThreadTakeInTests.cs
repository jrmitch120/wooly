using Wooly.Core.Posts;
using Wooly.Tests.Fakes;

namespace Wooly.Tests.Core;

/// <summary>
///     Where a reply just sent goes in a thread already held: where the instance would put it on the next read, so that
///     nothing moves when the thread is next asked for (#482). Depth-first, each reply followed by its own answers, and
///     oldest first among the answers to one post.
/// </summary>
public class PostThreadTakeInTests
{
    /// <summary>The post the thread is around, which none of the replies below is but every one of them answers in the end.</summary>
    private const string Head = "100";

    /// <summary>A post by <paramref name="account" /> answering <paramref name="answered" />.</summary>
    private static Post Answering(string id, string answered, string account = "maria@fosstodon.org") =>
        APost.With(id: id, account: account, inReplyTo: new PostReplyTarget { PostId = answered });

    private static PostThread Around(params Post[] replies) => new([APost.With(id: "90")], replies);

    private static IReadOnlyList<string> Ids(PostThread? thread) =>
        [.. Assert.IsType<PostThread>(thread).Replies.Select(reply => reply.Id)];

    /// <summary>An answer to the post itself is the newest answer to it, and goes after every reply already there.</summary>
    [Fact]
    public void TakeIn_PutsAnAnswerToThePostAfterEveryReply()
    {
        var thread = Around(Answering("110", Head), Answering("111", "110"), Answering("120", Head));

        var taken = thread.TakeIn(Head, Answering("130", Head));

        Assert.Equal(["110", "111", "120", "130"], Ids(taken));
        Assert.Equal(["90"], taken!.Ancestors.Select(ancestor => ancestor.Id));
    }

    /// <summary>An answer to a reply nobody has answered yet goes straight under it, ahead of the replies after it.</summary>
    [Fact]
    public void TakeIn_PutsAnAnswerToAnUnansweredReplyStraightUnderIt()
    {
        var thread = Around(Answering("110", Head), Answering("120", Head));

        Assert.Equal(["110", "130", "120"], Ids(thread.TakeIn(Head, Answering("130", "110"))));
    }

    /// <summary>
    ///     An answer to a reply that has answers already goes after the last of them, its own answers included — not
    ///     straight under the reply, where the next read would not put it.
    /// </summary>
    [Fact]
    public void TakeIn_PutsAnAnswerToAnAnsweredReplyAfterTheLastOfItsAnswers()
    {
        var thread = Around(
            Answering("110", Head),
            Answering("111", "110"),
            Answering("112", "111"),
            Answering("113", "110"),
            Answering("120", Head));

        Assert.Equal(["110", "111", "112", "113", "130", "120"], Ids(thread.TakeIn(Head, Answering("130", "110"))));
    }

    /// <summary>
    ///     An answer several levels down goes at the end of what answers the reply it answers, and before whatever
    ///     answers the replies above that one after it.
    /// </summary>
    [Fact]
    public void TakeIn_PutsAnAnswerSeveralLevelsDownAtTheEndOfItsOwnPart()
    {
        var thread = Around(
            Answering("110", Head),
            Answering("111", "110"),
            Answering("112", "111"),
            Answering("113", "112"),
            Answering("114", "111"),
            Answering("115", "110"),
            Answering("120", Head));

        Assert.Equal(
            ["110", "111", "112", "113", "130", "114", "115", "120"],
            Ids(thread.TakeIn(Head, Answering("130", "112"))));
    }

    /// <summary>
    ///     Ids order as the numbers they are rather than as the strings they travel as: a reply whose id is a digit
    ///     longer is the newer one, though it sorts first as text.
    /// </summary>
    [Fact]
    public void TakeIn_OrdersIdsAsNumbers()
    {
        var thread = Around(Answering("990", Head));

        Assert.Equal(["990", "1000"], Ids(thread.TakeIn(Head, Answering("1000", Head))));
    }

    /// <summary>
    ///     An answer to an ancestor is in another thread, which the next read of this one would not show — so it is
    ///     not this one's to take in, and nor is an answer to something it does not hold at all.
    /// </summary>
    [Fact]
    public void TakeIn_TakesNothingInThatAnswersSomethingTheThreadDoesNotHold()
    {
        var thread = Around(Answering("110", Head));

        Assert.Null(thread.TakeIn(Head, Answering("130", "90")));
        Assert.Null(thread.TakeIn(Head, Answering("130", "999")));
        Assert.Null(thread.TakeIn(Head, APost.With(id: "130")));
    }

    /// <summary>A reply the thread already holds is put where it goes rather than held twice.</summary>
    [Fact]
    public void TakeIn_HoldsAReplyOnce()
    {
        var thread = Around(Answering("110", Head), Answering("130", "110"), Answering("120", Head));

        Assert.Equal(["110", "130", "120"], Ids(thread.TakeIn(Head, Answering("130", "110"))));
    }
}
