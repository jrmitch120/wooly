using Wooly.Core.Discovery;

namespace Wooly.Tests.Core;

/// <summary>
///     <see cref="SuggestionReasonName" /> is the one place the five reasons are spelled — once for the wire, which is
///     how a source string becomes a reason, and once for the reader, which is the heading a section is drawn under.
///     Tested apart from the adapter because it is the piece both the port and the screen read, and neither of them is
///     allowed to spell a sixth.
/// </summary>
public class SuggestionReasonNameTests
{
    [Theory]
    [InlineData("friends_of_friends", SuggestionReason.FriendsOfFriends)]
    [InlineData("similar_to_recently_followed", SuggestionReason.SimilarToRecentlyFollowed)]
    [InlineData("featured", SuggestionReason.Featured)]
    [InlineData("most_interactions", SuggestionReason.MostInteractions)]
    [InlineData("most_followed", SuggestionReason.MostFollowed)]
    public void Parse_ReadsEverySourceMastodonDocuments(string source, SuggestionReason expected) =>
        Assert.Equal(expected, SuggestionReasonName.Parse(source));

    /// <summary>
    ///     Mastodon adds sources, and one this client has never heard of is not an error — it is a reason there is no
    ///     heading for. Answering nothing is what lets the person still be shown.
    /// </summary>
    [Theory]
    [InlineData("past_interactions")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Parse_AnswersNothingForASourceThisClientDoesNotName(string? source) =>
        Assert.Null(SuggestionReasonName.Parse(source));

    /// <summary>The wire spells these in lower case, and an instance that shouted one still means the same reason.</summary>
    [Fact]
    public void Parse_ReadsASourceWhateverCaseItArrivedIn() =>
        Assert.Equal(SuggestionReason.FriendsOfFriends, SuggestionReasonName.Parse("Friends_Of_Friends"));

    /// <summary>
    ///     The five headings, spelled here and nowhere else. Written out in the test as well as in the source, because
    ///     the point of this class is that the words themselves are the contract — a heading that changed silently is
    ///     the failure this catches.
    /// </summary>
    [Theory]
    [InlineData(SuggestionReason.FriendsOfFriends, "followed by people you follow")]
    [InlineData(SuggestionReason.SimilarToRecentlyFollowed, "like people you followed lately")]
    [InlineData(SuggestionReason.Featured, "featured by this instance")]
    [InlineData(SuggestionReason.MostInteractions, "talked with most here")]
    [InlineData(SuggestionReason.MostFollowed, "most followed here")]
    public void Of_SpellsTheHeadingASectionIsDrawnUnder(SuggestionReason reason, string expected) =>
        Assert.Equal(expected, SuggestionReasonName.Of(reason));

    /// <summary>
    ///     The declaration order is the rank a screen draws the sections in, so a reason added to the enum without a
    ///     place in the table would draw a section with no heading at all.
    /// </summary>
    [Fact]
    public void Of_SpellsEveryReasonTheEnumDeclares() =>
        Assert.All(Enum.GetValues<SuggestionReason>(), reason => Assert.NotEmpty(SuggestionReasonName.Of(reason)));

    /// <summary>A value that is no reason at all is a bug in this client, not something an instance sent.</summary>
    [Fact]
    public void Of_RefusesAValueThatIsNotAReason() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => SuggestionReasonName.Of((SuggestionReason)99));
}
