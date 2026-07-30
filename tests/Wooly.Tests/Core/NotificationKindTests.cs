using Wooly.Core.Notifications;

namespace Wooly.Tests.Core;

/// <summary>
///     A kind is a word rather than a member of a closed set (ADR-0010), and these pin what that buys and what it costs.
///     Nothing else in the suite would notice if kinds stopped comparing by the word they carry — the report's table and
///     the JSON's <c>kind</c> both depend on it, and both would go on passing against a kind that was merely equal to
///     itself.
/// </summary>
public class NotificationKindTests
{
    [Fact]
    public void Kinds_AreNamedInThisProjectsVocabularyRatherThanTheApis()
    {
        Assert.Equal("boost", NotificationKind.Boost.Name);
        Assert.Equal("favorite", NotificationKind.Favorite.Name);
        Assert.Equal("mention", NotificationKind.Mention.Name);
        Assert.Equal("follow", NotificationKind.Follow.Name);
    }

    /// <summary>
    ///     The four are told apart by the word each carries, which is what lets a report look one up in a table rather
    ///     than reaching for a <c>switch</c> a kind from an instance could fall out of.
    /// </summary>
    [Fact]
    public void Kinds_AreTheSameKindWhenTheyAreTheSameWord()
    {
        Assert.Equal(NotificationKind.Mention, NotificationKind.Mention);
        Assert.NotEqual(NotificationKind.Boost, NotificationKind.Favorite);
    }

    /// <summary>
    ///     The consequence of comparing by the word, stated on purpose: an instance that reports one of this project's
    ///     own four words is taken to mean that kind. It is the answer that makes sense — a client that said "poll"
    ///     twice and meant two different things would be worse — but nothing in the type prevents the collision, so it
    ///     is written down here rather than discovered.
    /// </summary>
    [Fact]
    public void AKindReportedByAnInstanceIsTheNamedOneWhereTheWordsAgree()
    {
        Assert.Equal(NotificationKind.Mention, NotificationKind.Reported("mention"));
        Assert.NotEqual(NotificationKind.Mention, NotificationKind.Reported("poll"));
    }
}
